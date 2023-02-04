// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal static class DisposeTester
    {
        /// <summary>
        /// Test that dispose doesn't crash or throw anything.
        ///
        /// This function is here just to allow the coverage tester to
        /// devirtualize the call to Dispose. Otherwise, it fails to notice
        /// some calls we're actually making.
        /// </summary>
        public static void TestDispose<T>(T disposable) where T: System.IDisposable
        {
            disposable.Dispose();
        }
    }
}
