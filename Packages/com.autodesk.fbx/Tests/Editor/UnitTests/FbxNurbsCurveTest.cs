using NUnit.Framework;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxNurbsCurveTest : FbxGeometryTestBase<FbxNurbsCurve>
    {
        [Test]
        public void TestBasics()
        {
            base.TestBasics(CreateObject("nurbscurve"), FbxNodeAttribute.EType.eNurbsCurve);

            using (FbxNurbsCurve curve = CreateObject("nurbscurve"))
            {
                // returns -1 if the curve has not been initialized.
                Assert.That(curve.GetSpanCount(), Is.EqualTo(-1));

                curve.SetOrder(4);
                curve.SetDimension(FbxNurbsCurve.EDimension.e3D);

                curve.InitControlPoints(7, FbxNurbsCurve.EType.eOpen);
                curve.SetControlPointAt(new FbxVector4(-104.648651123047, -55.7837829589844, 0, 1), 0);
                curve.SetControlPointAt(new FbxVector4(-109.405403137207, 22.9189186096191, 0, 1), 1);
                curve.SetControlPointAt(new FbxVector4(-73.5135116577148, 76.5405426025391, 0, 1), 2);
                curve.SetControlPointAt(new FbxVector4(21.1891899108887, 91.2432403564453, 0, 1), 3);
                curve.SetControlPointAt(new FbxVector4(83.0270309448242, 62.7027015686035, 0, 1), 4);
                curve.SetControlPointAt(new FbxVector4(109.405403137207, 18.1621627807617, 0, 1), 5);
                curve.SetControlPointAt(new FbxVector4(31.5675678253174, -91.2432403564453, 0, 1), 6);

                var knotCount = curve.GetKnotCount();
                Assert.AreEqual(curve.GetKnotCount(), 11);

                Assert.That(() => curve.SetKnotVectorAt(-1, 0), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => curve.SetKnotVectorAt(11, 0), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
                curve.SetKnotVectorAt(0, 0);
                curve.SetKnotVectorAt(1, 0);
                curve.SetKnotVectorAt(2, 0);
                curve.SetKnotVectorAt(3, 0);
                curve.SetKnotVectorAt(4, 0.25);
                curve.SetKnotVectorAt(5, 0.5);
                curve.SetKnotVectorAt(6, 0.75);
                curve.SetKnotVectorAt(7, 1);
                curve.SetKnotVectorAt(8, 1);
                curve.SetKnotVectorAt(9, 1);
                curve.SetKnotVectorAt(10, 1);

                Assert.That(() => curve.GetKnotVectorAt(-1), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => curve.GetKnotVectorAt(11), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(curve.GetKnotVectorAt(0), Is.EqualTo(0));
                Assert.That(curve.GetKnotVectorAt(4), Is.EqualTo(0.25));
                Assert.That(curve.GetKnotVectorAt(6), Is.EqualTo(0.75));
                Assert.That(curve.GetKnotVectorAt(10), Is.EqualTo(1));

                curve.SetStep(-1);
                curve.SetStep(int.MaxValue);
                curve.SetStep(2);
                Assert.That(curve.GetStep(), Is.EqualTo(2));

                Assert.That(curve.IsRational(), Is.False);

                // Calculation is as follows:
                // S = Number of spans N = Number of CVs M = Order of the curve.
                // S = N - M + 1;
                // N includes the duplicate CVs for closed and periodic curves.
                Assert.That(curve.GetSpanCount(), Is.EqualTo(4));

                Assert.That(curve.IsBezier(), Is.False);
                Assert.That(curve.IsPolyline(), Is.False);

                Assert.AreEqual(curve.GetOrder(), 4);
                Assert.AreEqual(curve.GetDimension(), FbxNurbsCurve.EDimension.e3D);
                Assert.AreEqual(curve.GetControlPointsCount(), 7);
            }
        }
    }
}
