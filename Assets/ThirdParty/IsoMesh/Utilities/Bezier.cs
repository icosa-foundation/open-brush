using UnityEngine;

namespace Raincoat
{
	public static class Bezier
	{
		/// <summary>
		/// The control point mode determines how the handles of a particular curve of a spline change to reflect
		/// each other.
		/// </summary>
		public enum ControlPointMode
		{
			/// <summary>
			/// Each control point can be manipulated independently. Allows for very sharp turns.
			/// </summary>
			Free,
			/// <summary>
			/// Control points must be colinear, keeping the curve smooth.
			/// </summary>
			Aligned,
			/// <summary>
			/// Control points must be both colinear and also each the same distance from the point
			/// they're controlling. Makes the curve segment symmetrical.
			/// </summary>
			Mirrored,
			/// <summary>
			/// Control points must always be in a line.
			/// </summary>
			Linear
		}

		public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
		{
			t = Mathf.Clamp01(t);
			float oneMinusT = 1f - t;
			return oneMinusT * oneMinusT * p0 + 2f * oneMinusT * t * p1 + t * t * p2;
		}

		public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, float t)
		{
			return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
		}

		public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			t = Mathf.Clamp01(t);
			float oneMinusT = 1f - t;
			return oneMinusT * oneMinusT * oneMinusT * p0 + 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t * p3;
		}

		public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			t = Mathf.Clamp01(t);
			float oneMinusT = 1f - t;
			return 3f * oneMinusT * oneMinusT * (p1 - p0) + 6f * oneMinusT * t * (p2 - p1) + 3f * t * t * (p3 - p2);
		}

		public static void GetPointAndFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, out Vector3 point, out Vector3 derivative)
        {
			t = Mathf.Clamp01(t);
			float oneMinusT = 1f - t;

			point = oneMinusT * oneMinusT * oneMinusT * p0 + 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t * p3;
			derivative = 3f * oneMinusT * oneMinusT * (p1 - p0) + 6f * oneMinusT * t * (p2 - p1) + 3f * t * t * (p3 - p2);
		}
	}
}
