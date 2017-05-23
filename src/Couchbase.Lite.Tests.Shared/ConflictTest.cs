﻿//
//  ConflictTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class ConflictTest : TestCase
    {
#if !WINDOWS_UWP
        public ConflictTest(ITestOutputHelper output) : base(output)
#else
        public ConflictTest()
#endif
        {
            ConflictResolver = new DoNotResolve();
        }

        [Fact]
        public void TestConflict()
        {
            ConflictResolver = new TheirsWins();
            ReopenDB();
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scotty", "because the 'theirs' version should win");

            doc = new Document("doc2");
            ConflictResolver = new MergeThenTheirsWins();
            ReopenDB();
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            Db.Save(doc);

            // Force a conflict again
            var properties = doc.ToDictionary();
            properties["type"] = "bio";
            properties["gender"] = "male";
            SaveProperties(properties, doc.Id);

            // Save and make sure that the correct conflict resolver won
            doc.Set("type", "bio");
            doc.Set("age", 31);
            Db.Save(doc);

            doc["age"].ToLong().Should().Be(31L, "because 'age' was changed by 'mine' and not 'theirs'");
            doc["type"].ToString().Should().Be("bio", "because 'type' was changed by 'mine' and 'theirs' so 'theirs' should win");
            doc["gender"].ToString().Should().Be("male", "because 'gender' was changed by 'theirs' but not 'mine'");
            doc["name"].ToString().Should().Be("Scott", "because 'name' was unchanged");
        }

        [Fact]
        public void TestConflictResolverGivesUp()
        {
            ConflictResolver = new GiveUp();
            ReopenDB();
            var doc = SetupConflict();
            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Code.Should()
                .Be(StatusCode.Conflict, "because the conflict resolver gave up");
        }

        [Fact]
        public void TestDeletionConflict()
        {
            ConflictResolver = new DoNotResolve();
            ReopenDB();
            var doc = SetupConflict();
            Db.Delete(doc);
            doc.Exists.Should().BeTrue("because there was a conflict in place of thgie deletion");
            doc.IsDeleted.Should().BeFalse("because there was a conflict in place of the deletion");
            doc["name"].ToString().Should().Be("Scotty", "because that was the pre-deletion value");
        }

        [Fact]
        public void TestConflictMineIsDeeper()
        {
            ConflictResolver = null;
            ReopenDB();
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scott Pilgrim", "because the current in memory document has a longer history");
        }

        [Fact]
        public void TestConflictTheirsIsDeeper()
        {
            ConflictResolver = null;
            ReopenDB();
            var doc = SetupConflict();

            // Add another revision to the conflict, so it'll have a higher generation
            var properties = doc.ToDictionary();
            properties["name"] = "Scott of the Sahara";
            SaveProperties(properties, doc.Id);
            Db.Save(doc);

            doc["name"].ToString().Should().Be("Scott of the Sahara", "because the conflict has a longer history");
        }

        private Document SetupConflict()
        {
            var doc = new Document("doc1");
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            Db.Save(doc);

            // Force a conflict
            var properties = doc.ToDictionary();
            properties["name"] = "Scotty";
            SaveProperties(properties, doc.Id);

            doc.Set("name", "Scott Pilgrim");
            return doc;
        }

        private unsafe void SaveProperties(IDictionary<string, object> props, string docID)
        {
            Db.InBatch(() =>
            {
                var tricky =
                    (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db.c4db, docID, true, err));
                var put = new C4DocPutRequest {
                    docID = tricky->docID,
                    history = &tricky->revID,
                    historyCount = 1,
                    save = true
                };

                var body = Db.JsonSerializer.Serialize(props);
                put.body = body;

                LiteCoreBridge.Check(err =>
                {
                    var localPut = put;
                    var retVal = Native.c4doc_put(Db.c4db, &localPut, null, err);
                    Native.FLSliceResult_Free(body);
                    return retVal;
                });
            });
        }
    }

    internal class TheirsWins : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            return conflict.Target;
        }
    }

    internal class MergeThenTheirsWins : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            var resolved = new Document(conflict.CommonAncestor.ToDictionary());
            var changed = new HashSet<string>();
            foreach (var pair in conflict.Target) {
                resolved.Set(pair.Key, pair.Value);
                changed.Add(pair.Key);
            }

            foreach (var pair in conflict.Source) {
                if (!changed.Contains(pair.Key)) {
                    resolved.Set(pair.Key, pair.Value);
                }
            }

            return resolved;
        }
    }

    internal class GiveUp : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            return null;
        }
    }

    internal class DoNotResolve : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            throw new NotImplementedException();
        }
    }
}
