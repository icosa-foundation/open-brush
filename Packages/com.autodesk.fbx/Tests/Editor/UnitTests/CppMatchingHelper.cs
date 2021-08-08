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
    static class CppMatchingHelper
    {
        public delegate T Factory<T>(double[] expected);
        public delegate T TestCommand<T>(T a, T b);
        public delegate bool AreSimilar<T>(T a, T b);

        /**
         * Help tests like FbxVector4Test verify that the FbxSharp
         * implementation of an arithmetic type matches the C++ FBX SDK
         * implementation.
         *
         * E.g. test that the C# and C++ FbxVector4 implementations match.
         *
         * The scheme is that the build system compiles and runs a C++ test
         * program (e.g. Vectors.cpp), which outputs lines in a format:
         *    context:command:double,double,double...
         *
         * Context and command are strings with no colons in them.
         *
         * Each double is encoded as
         *    description/exact
         * where 'description' is a human-readable double (e.g. 1.002452) and
         * exact is a 64-bit hex string that has the exact bits of a double.
         *
         * Each line is the result of running a unary or binary operation (the
         * command) on two variables 'a' and 'b'.  The command "a" should be
         * issued first, followed by the command "b". These initialize the two
         * variables.
         *
         * Callers must provide:
         * - the file that the C++ test program creates
         * - the 'context' that matches; we ignore lines for other contexts
         * - a factory to convert an array of doubles to the type you are
         *   testing (e.g. FbxVector4)
         * - a map from 'command' values to lambda functions that implement that command
         *    (except for commands "a" and "b")
         * - optionally, a map from 'command' values to lambda functions that
         *   compare results (to allow inexact comparison, e.g. for differences
         *   in rounding).
         *
         * The C++ test must call every command.
         */
        public static void MatchingTest<T>(
                string filename,
                string test_context,
                Factory<T> factory,
                Dictionary<string, TestCommand<T>> commands,
                Dictionary<string, AreSimilar<T>> custom_comparators = null
                ) where T : new()
        {
            var a = new T();
            var b = new T();
            var commands_used = new HashSet<string>();

            using (var file = new System.IO.StreamReader(filename)) {
                string line;
                while ( null != (line = file.ReadLine()) ) {
                    string context;
                    string command;
                    double [] expectedArray;
                    ParseLine(line, out context, out command, out expectedArray);
                    if (context != test_context) {
                        continue;
                    }

                    var expected = factory(expectedArray);

                    // Perform the command, depending what it is.
                    T actualValue;
                    if (command == "a") {
                        actualValue = a = expected;
                    } else if (command == "b") {
                        actualValue = b = expected;
                    } else {
                        commands_used.Add(command);
                        Assert.IsTrue(commands.ContainsKey(command), "unknown command " + command);
                        actualValue = commands[command](a, b);
                    }

                    // Make sure we got the expected result.
                    if (custom_comparators != null && custom_comparators.ContainsKey(command)) {
                        var comp = custom_comparators[command];
                        Assert.IsTrue(comp(expected, actualValue), command);
                    } else {
                        Assert.AreEqual(expected, actualValue, command);
                    }
                }
            }

            // Make sure we actually called all those commands.
            Assert.That(commands_used, Is.EquivalentTo(commands.Keys));
        }

        // Parse one line in the file.
        static void ParseLine(string line,
                out string out_context,
                out string out_command,
                out double [] out_expected)
        {
            // Parse the whole colon-separated line:
            //    file.cpp:5:a + 2:6.71089e+07/0x419000000c000000,6.7...
            var items = line.Split(':');
            Assert.AreEqual(items.Length, 3);

            out_context = items[0];
            out_command = items[1];

            // now parse the comma-separated doubles:
            // 6.71089e+07/0x419000000c000000,6.71089e+07/0x4190000010000000,...
            var doubles = items[2];
            items = doubles.Split(',');

            out_expected = new double[items.Length];
            for(int i = 0, n = items.Length; i < n; ++i) {
                // parse one double: 6.71089e+07/0x419000000c000000
                // we ignore the printed double, just take its exact 64-bit representation.
                var pair = items[i].Split('/');
                Assert.AreEqual(2, pair.Length);
                var asInt = System.Convert.ToInt64(pair[1], 16);
                var asDouble = System.BitConverter.Int64BitsToDouble(asInt);
                out_expected[i] = asDouble;
            }
        }

    }
}
