// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxTimeTest : TestBase<FbxTime>
    {
        [Test]
        public void TestComparison ()
        {
            var a = FbxTime.FromSecondDouble(5);
            var b = FbxTime.FromSecondDouble(6);
            var acopy = FbxTime.FromSecondDouble(5);

            // Test equality.
            EqualityTester<FbxTime>.TestEquality(a, b, acopy);

            // Test inequality.
            Assert.IsTrue(a.CompareTo(b) < 0);
            Assert.IsTrue(b.CompareTo(a) > 0);
            Assert.IsTrue(a.CompareTo(acopy) == 0);
            Assert.IsTrue(a.CompareTo((object)null) > 0);
            Assert.That(() => a.CompareTo("a string"), Throws.Exception.TypeOf<System.ArgumentException>());
            Assert.IsTrue(a < b);
            Assert.IsTrue(a <= b);
            Assert.IsFalse(a >= b);
            Assert.IsFalse(a > b);
            Assert.IsTrue((FbxTime)null < b);
            Assert.IsFalse(a < (FbxTime)null);
            Assert.IsFalse((FbxTime)null < (FbxTime)null);
        }

        [Test]
        public void TestBasics ()
        {
            // try the static functions
            var mode = FbxTime.GetGlobalTimeMode();
            FbxTime.SetGlobalTimeMode(FbxTime.EMode.ePAL);
            Assert.AreEqual(FbxTime.EMode.ePAL, FbxTime.GetGlobalTimeMode());
            FbxTime.SetGlobalTimeMode(mode);

            var protocol = FbxTime.GetGlobalTimeProtocol();
            FbxTime.SetGlobalTimeProtocol(FbxTime.EProtocol.eSMPTE);
            Assert.AreEqual(FbxTime.EProtocol.eSMPTE, FbxTime.GetGlobalTimeProtocol());
            FbxTime.SetGlobalTimeProtocol(protocol);

            Assert.AreEqual(24, FbxTime.GetFrameRate(FbxTime.EMode.eFrames24));
            Assert.AreEqual(FbxTime.EMode.eFrames24, FbxTime.ConvertFrameRateToTimeMode(24));
            Assert.AreEqual(FbxTime.EMode.eFrames24, FbxTime.ConvertFrameRateToTimeMode(24.01, 0.1));
            Assert.AreEqual(FbxTime.EMode.eDefaultMode, FbxTime.ConvertFrameRateToTimeMode(24.1, 0.01));

            TestGetter(FbxTime.GetOneFrameValue());
            TestGetter(FbxTime.GetOneFrameValue(FbxTime.EMode.ePAL));

            Assert.IsFalse(FbxTime.IsDropFrame());
            Assert.IsTrue(FbxTime.IsDropFrame(FbxTime.EMode.eNTSCDropFrame));

            // just make sure it doesn't crash
            new FbxTime();

            // test dispose
            DisposeTester.TestDispose(new FbxTime());
            using (new FbxTime ()) {}

            // try the extension constructors
            Assert.AreEqual(5, FbxTime.FromRaw(5).GetRaw());
            Assert.AreEqual(5, FbxTime.FromMilliSeconds(5000).GetSecondDouble());
            Assert.AreEqual(5, FbxTime.FromSecondDouble(5).GetSecondDouble());
            Assert.AreEqual(126210.02, FbxTime.FromTime(pSecond:7, pHour:1, pMinute:10, pResidual:2).GetFrameCountPrecise());
            Assert.AreEqual(5, FbxTime.FromFrame(5).GetFrameCountPrecise());
            Assert.AreEqual(5.125, FbxTime.FromFramePrecise(5.125).GetFrameCountPrecise());
            Assert.AreEqual(5, FbxTime.FromRaw(5).GetRaw());
            Assert.AreEqual(126211.2, FbxTime.FromFrame(105176, FbxTime.EMode.ePAL).GetFrameCountPrecise());
            Assert.AreEqual(126211.8, FbxTime.FromFramePrecise(105176.5, FbxTime.EMode.ePAL).GetFrameCountPrecise());
            Assert.AreEqual(126211.8, FbxTime.FromTimeString("105176.5", FbxTime.EMode.ePAL).GetFrameCountPrecise());

            // try breaking a time down
            var t = FbxTime.FromTime(pSecond:7, pHour:1, pMinute:10, pField:4, pResidual:2);
            Assert.AreEqual(1, t.GetHourCount());
            Assert.AreEqual(70, t.GetMinuteCount());
            Assert.AreEqual(4207, t.GetSecondCount());
            Assert.AreEqual(4207067, t.GetMilliSeconds());
            Assert.AreEqual(126212, t.GetFrameCount());
            Assert.AreEqual(105176, t.GetFrameCount(FbxTime.EMode.ePAL));
            Assert.AreEqual(252424.04, t.GetFrameCountPrecise(FbxTime.EMode.eFrames60));
            Assert.AreEqual(252424, t.GetFieldCount());
            Assert.AreEqual(210353, t.GetFieldCount(FbxTime.EMode.ePAL));
            Assert.AreEqual(2, t.GetResidual());
            Assert.AreEqual(68, t.GetResidual(FbxTime.EMode.ePAL));
            Assert.AreEqual(':', t.GetFrameSeparator());
            Assert.AreEqual(':', t.GetFrameSeparator(FbxTime.EMode.ePAL));

            int h, m, s, frame, field, residual;
            t.GetTime(out h, out m, out s, out frame, out field, out residual);
            Assert.AreEqual(1, h);
            Assert.AreEqual(10, m);
            Assert.AreEqual(2, frame);
            Assert.AreEqual(0, field);
            Assert.AreEqual(2, residual);

            t.GetTime(out h, out m, out s, out frame, out field, out residual, FbxTime.EMode.ePAL);
            Assert.AreEqual(1, h);
            Assert.AreEqual(10, m);
            Assert.AreEqual(1, frame);
            Assert.AreEqual(1, field);
            Assert.AreEqual(68, residual);

            Assert.AreEqual("126212*", t.GetTimeString());
            Assert.AreEqual("126212*", t.GetTimeString(FbxTime.EElement.eSeconds));
            Assert.AreEqual("001:10:07", t.GetTimeString(pEnd: FbxTime.EElement.eSeconds, pTimeFormat: FbxTime.EProtocol.eSMPTE));

            Assert.AreEqual("126212", t.GetFramedTime().GetTimeString());
            Assert.AreEqual("126212", t.GetFramedTime(false).GetTimeString());
        }
    }

    internal class FbxTimeSpanTest : TestBase<FbxTimeSpan>
    {
        [Test]
        public void TestBasics ()
        {
            // just make sure it doesn't crash
            new FbxTimeSpan();
            new FbxTimeSpan (FbxTime.FromFrame(1), FbxTime.FromFrame(2));

            // test dispose
            DisposeTester.TestDispose(new FbxTimeSpan());
            using (new FbxTimeSpan (FbxTime.FromFrame(1), FbxTime.FromFrame(2))) { }

            Assert.That (() => { new FbxTimeSpan(null, null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // test Set/Get
            FbxTimeSpan timeSpan = new FbxTimeSpan();
            timeSpan.Set (FbxTime.FromFrame(2), FbxTime.FromFrame(3));
            Assert.AreEqual(FbxTime.FromFrame(2), timeSpan.GetStart());
            Assert.AreEqual(FbxTime.FromFrame(3), timeSpan.GetStop());
            Assert.That (() => { timeSpan.Set(null, null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            timeSpan.SetStart(FbxTime.FromFrame(1));
            Assert.AreEqual(FbxTime.FromFrame(1), timeSpan.GetStart());
            timeSpan.SetStop(FbxTime.FromFrame(4));
            Assert.AreEqual(FbxTime.FromFrame(4), timeSpan.GetStop());

            // test other functions
            Assert.AreEqual(FbxTime.FromFrame(3), timeSpan.GetDuration());
            Assert.AreEqual(FbxTime.FromFrame(3), timeSpan.GetSignedDuration());
            Assert.AreEqual(1, timeSpan.GetDirection());
            Assert.IsTrue(timeSpan.IsInside(FbxTime.FromFrame(2)));

            var timeSpan2 = new FbxTimeSpan(FbxTime.FromFrame(2), FbxTime.FromFrame(10));
            Assert.AreEqual(new FbxTimeSpan(FbxTime.FromFrame(2), FbxTime.FromFrame(4)), timeSpan.Intersect(timeSpan2));

            timeSpan.UnionAssignment(timeSpan2);
            Assert.AreEqual(new FbxTimeSpan(FbxTime.FromFrame(1), FbxTime.FromFrame(10)), timeSpan);

            new FbxTimeSpan(FbxTime.FromFrame(0), FbxTime.FromFrame(1)).UnionAssignment(timeSpan2, 1);
        }
    }
}
