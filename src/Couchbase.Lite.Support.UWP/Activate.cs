﻿//
//  Activate.cs
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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Storage;
using Couchbase.Lite.Logging;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The UWP support class
    /// </summary>
    public static class UWP
    {
        /// <summary>
        /// Activates the support classes for UWP
        /// </summary>
        public static void Activate()
        {
            InjectableCollection.RegisterImplementation<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver());
            InjectableCollection.RegisterImplementation<ILogger>(() => new UwpDefaultLogger());
            var assembly = typeof(UWP).GetTypeInfo().Assembly;
            Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "x86"));
            Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "x64"));
            
            foreach (var filename in new[] {"LiteCore", "sqlite3"}) {
                var x86Path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "x86", $"{filename}.dll");
                if (!File.Exists(x86Path)) {
                    using (var x86Out = File.OpenWrite(x86Path))
                    using (var x86In = assembly.GetManifestResourceStream($"{filename}_x86")) {
                        x86In.CopyTo(x86Out);
                    }
                }

                var x64Path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "x64", $"{filename}.dll");
                if (!File.Exists(x64Path)) {
                    using (var x86Out = File.OpenWrite(x64Path))
                    using (var x86In = assembly.GetManifestResourceStream($"{filename}_x64")) {
                        x86In.CopyTo(x86Out);
                    }
                }
            }

            var architecture = IntPtr.Size == 4
                ? "x86"
                : "x64";
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, architecture, "LiteCore.dll");
            const uint loadWithAlteredSearchPath = 8;
            var ptr = LoadLibraryEx(path, IntPtr.Zero, loadWithAlteredSearchPath);
            if (ptr != IntPtr.Zero) {
                return;
            }

            Debug.WriteLine("Could not load LiteCore.dll!  Nothing is going to work!");
            throw new LiteCoreException(new C4Error(LiteCoreError.UnexpectedError));
        }

        [DllImport("kernel32")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
    }
}
