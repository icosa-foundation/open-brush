// ***********************************************************************
// Copyright (c) 2021 Unity Technologies. All rights reserved.  
//
// Licensed under the ##LICENSENAME##. 
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using Autodesk.Fbx;
using System.IO;
using System.Collections.Generic;

namespace Autodesk.Fbx.LinuxTest
{
    /// <summary>
    /// On linux, in v4.0.1, we temporarily have no tests because of an issue
    /// with the CI machines not having libstdc++ required for FBX SDK.
    ///
    /// But we need at least one test. So here goes.
    /// </summary>
    internal class EmptyTest
    {
        [Test]
        public static void Pass()
        {
        }
    }
}
