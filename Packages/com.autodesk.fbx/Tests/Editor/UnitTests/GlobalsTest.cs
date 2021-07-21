// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Collections.Generic;
using System.Reflection;

namespace Autodesk.Fbx.UnitTests
{
    internal class GlobalsTest
    {
        const string kPINVOKE = "NativeMethods";
        static System.Type s_PINVOKEtype;
        static ConstructorInfo s_PINVOKEctor;
        static List<MethodInfo> s_UpcastFunctions = new List<MethodInfo>();

        static GlobalsTest()
        {
            /* We test the PINVOKE class by reflection since it's private to
             * its assembly. */
            var alltypes = typeof(Autodesk.Fbx.Globals).Assembly.GetTypes();
            foreach(var t in alltypes) {
                if (t.Namespace == "Autodesk.Fbx" && t.Name == kPINVOKE) {
                    s_PINVOKEtype = t;
                    break;
                }
            }
            Assert.IsNotNull(s_PINVOKEtype);

            s_PINVOKEctor = s_PINVOKEtype.GetConstructor(new System.Type[] {});

            foreach(var m in s_PINVOKEtype.GetMethods()) {
                if (m.Name.EndsWith("SWIGUpcast")) {
                    s_UpcastFunctions.Add(m);
                }
            }
        }

        bool ProgressCallback(float a, string b) { return true; }

        [Test]
        public void BasicTests ()
        {
            /* Try to create the Globals, which isn't
             * static, so the coverage tests want us to create them. */
            new Globals();

            /* Create the NativeMethods, which isn't static.
             * But it is protected, so we can't create it normally,
             * which is why we use reflection. */
            s_PINVOKEctor.Invoke(null);

            /* Don't actually invoke the SWIGUpcast functions. They're a
             * feature to handle multiple inheritance. But FBX SDK doesn't use
             * multiple inheritance anyway. */
        }

    }
}
