// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;

using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal abstract class TestBase<T>
    {
        /*
         * Helper to test a property getter without a compiler warning.
         * Use this like:
         *      TestGetter(tex.Alpha);
         *
         * That will call get_Alpha under the hood, verifying that the getter
         * actually works. You can't just write
         *      tex.Alpha;
         * because then you get a warning or error that your statement is
         * invalid.
         */
        public static void TestGetter<U>(U item) { /* we tested the getter by passing the argument! */ }
    }
}
