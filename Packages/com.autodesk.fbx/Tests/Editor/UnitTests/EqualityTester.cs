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
    internal static class EqualityTester<T>
    {
        // T.Equals(T), T.Equals(base(T), ...
        static List<System.Reflection.MethodInfo> s_Equals = new List<System.Reflection.MethodInfo>();

        // operator== (T, T), operator== (base(T), base(T), ...
        static List<System.Reflection.MethodInfo> s_op_Equality = new List<System.Reflection.MethodInfo>();

        // operator!= (T, T), operator== (base(T), base(T), ...
        static List<System.Reflection.MethodInfo> s_op_Inequality = new List<System.Reflection.MethodInfo>();

        static EqualityTester() {
            // For T and its base classes B1, B2, ...
            // get the following functions so we can test equality:
            // bool Equals(U)
            // static bool operator == (U, U)
            // static bool operator != (U, U)
            var U = typeof(T);
            do {
                // Get all the methods, look for Equals(U), op_Equality(U,U), and op_Inequality(U,U)
                var methods = U.GetMethods();
                foreach(var method in methods) {
                    if (method.Name == "Equals") {
                        var parms = method.GetParameters();
                        if (parms.Length == 1 && parms[0].ParameterType == U) {
                            s_Equals.Add(method);
                        }
                    } else if (method.Name == "op_Equality") {
                        var parms = method.GetParameters();
                        if (parms.Length == 2 && parms[0].ParameterType == U && parms[1].ParameterType == U) {
                            s_op_Equality.Add(method);
                        }
                    } else if (method.Name == "op_Inequality") {
                        var parms = method.GetParameters();
                        if (parms.Length == 2 && parms[0].ParameterType == U && parms[1].ParameterType == U) {
                            s_op_Inequality.Add(method);
                        }
                    }
                }

                // Repeat on the base type, if there is one.
                U = U.BaseType;
            } while (U != null);
        }

        /* Instances of this class definitely don't cast to T. */
        class WrongClass { };

        /*
         * Test all the equality and hashing functions on type T.
         *
         * 'a' is an arbitrary non-null instance of class T.
         *
         * 'b' should be a different non-null instance.
         *
         * 'acopy' should be equal, but not reference-equal, to 'a' (unless the
         * notion of equality for this type is reference equality)
         */
        public static void TestEquality(T a, T b, T acopy) {
            // Test Equals(object) on a.
            Assert.IsTrue(a.Equals((object) acopy));
            Assert.IsFalse(a.Equals((object) b));
            Assert.IsFalse(a.Equals((object) new WrongClass()));

            // Test all the Equals functions on a.
            // a.Equals(a) is true
            // a.Equals(b) is false
            // a.Equals(null) is false and doesn't throw an exception
            foreach(var equals in s_Equals) {
                Assert.IsTrue(Invoker.Invoke<bool>(equals, a, a));
                Assert.IsTrue(Invoker.Invoke<bool>(equals, a, acopy));
                Assert.IsFalse(Invoker.Invoke<bool>(equals, a, b));
                Assert.IsFalse(Invoker.Invoke<bool>(equals, a, null));
            }

            // test operator== in various cases including null handling
            foreach(var equals in s_op_Equality) {
                Assert.IsTrue(Invoker.InvokeStatic<bool>(equals, a, a));
                Assert.IsTrue(Invoker.InvokeStatic<bool>(equals, a, acopy));
                Assert.IsFalse(Invoker.InvokeStatic<bool>(equals, a, b));
                Assert.IsFalse(Invoker.InvokeStatic<bool>(equals, a, null));
                Assert.IsFalse(Invoker.InvokeStatic<bool>(equals, null, b));
                Assert.IsTrue(Invoker.InvokeStatic<bool>(equals, null, null));
            }

            // test operator!= in the same cases; should always return ! the answer
            foreach(var equals in s_op_Inequality) {
                Assert.IsTrue(!Invoker.InvokeStatic<bool>(equals, a, a));
                Assert.IsTrue(!Invoker.InvokeStatic<bool>(equals, a, acopy));
                Assert.IsFalse(!Invoker.InvokeStatic<bool>(equals, a, b));
                Assert.IsFalse(!Invoker.InvokeStatic<bool>(equals, a, null));
                Assert.IsFalse(!Invoker.InvokeStatic<bool>(equals, null, b));
                Assert.IsTrue(!Invoker.InvokeStatic<bool>(equals, null, null));
            }

            // test hashing. This is very minimal: just testing that two
            // instances that test equal have equal hash code.
            Assert.AreEqual(a.GetHashCode(), acopy.GetHashCode());
        }
    }
}
