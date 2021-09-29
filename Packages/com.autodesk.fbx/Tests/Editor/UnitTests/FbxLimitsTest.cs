// ***********************************************************************
// Copyright (c) 2018 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxLimitsTest : TestBase<FbxLimits>
    {
        // There's lots of flags with get/set to test. Do it with lambdas.
        delegate bool GetActive();
        delegate void SetActive(bool active);
        void AssertActiveFlag(GetActive getActive, SetActive setActive)
        {
            Assert.IsFalse(getActive());
            setActive(true);
            Assert.IsTrue(getActive());
        }

        [Test]
        public void TestBasics ()
        {
            var limits = new FbxLimits();

            AssertActiveFlag(limits.GetActive, limits.SetActive);

            AssertActiveFlag(limits.GetMinXActive, limits.SetMinXActive);
            AssertActiveFlag(limits.GetMinYActive, limits.SetMinYActive);
            AssertActiveFlag(limits.GetMinZActive, limits.SetMinZActive);
            limits.SetMin(new FbxDouble3(1, 2, 3));
            Assert.That(limits.GetMin(), Is.EqualTo(new FbxDouble3(1, 2, 3)));

            AssertActiveFlag(limits.GetMaxXActive, limits.SetMaxXActive);
            AssertActiveFlag(limits.GetMaxYActive, limits.SetMaxYActive);
            AssertActiveFlag(limits.GetMaxZActive, limits.SetMaxZActive);
            limits.SetMax(new FbxDouble3(4, 5, 6));
            Assert.That(limits.GetMax(), Is.EqualTo(new FbxDouble3(4, 5, 6)));

            Assert.That(limits.Apply(new FbxDouble3(7, 8, 9)), Is.EqualTo(new FbxDouble3(4, 5, 6)));

            limits.SetMinActive(false, true, false);
            Assert.IsFalse(limits.GetMinXActive());
            Assert.IsTrue(limits.GetMinYActive());
            limits.SetMaxActive(false, false, false);
            Assert.IsFalse(limits.GetMaxXActive());
            Assert.IsTrue(limits.GetAnyMinMaxActive());

            limits.Dispose();
        }
    }
}
