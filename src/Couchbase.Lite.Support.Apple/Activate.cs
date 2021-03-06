﻿// 
//  Activate.cs
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
using System.IO;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using Foundation;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Support classes for Xamarin iOS
    /// </summary>
    public static class iOS
	{
	    #region Variables

	    private static AtomicBool _Activated = false;

	    #endregion

	    #region Public Methods

	    /// <summary>
		/// Activates the Xamarin iOS specific support classes
		/// </summary>
		public static void Activate()
		{
            if(_Activated.Set(true)) {
                return;
            }
            
			Console.WriteLine("Loading support items");
            Service.AutoRegister(typeof(iOS).Assembly);

			Console.WriteLine("Loading libLiteCore.dylib");
			var dylibPath = Path.Combine(NSBundle.MainBundle.BundlePath, "libLiteCore.dylib");
			if (!File.Exists(dylibPath))
			{
				Console.WriteLine("Failed to find libLiteCore.dylib, nothing is going to work!");
			}

			var loaded = ObjCRuntime.Dlfcn.dlopen(dylibPath, 0);
			if (loaded == IntPtr.Zero)
			{
				Console.WriteLine("Failed to load libLiteCore.dylib, nothing is going to work!");
				var error = ObjCRuntime.Dlfcn.dlerror();
				if (String.IsNullOrEmpty(error))
				{
					Console.WriteLine("dlerror() was empty; most likely missing architecture");
				}
				else
				{
					Console.WriteLine($"Error: {error}");
				}
			}
		}

	    /// <summary>
		/// Enables text based logging for debugging purposes.  Log statements will
		/// be written to NSLog
		/// </summary>
		public static void EnableTextLogging()
		{
			Log.EnableTextLogging(new iOSDefaultLogger());
		}

	    #endregion
	}
}
