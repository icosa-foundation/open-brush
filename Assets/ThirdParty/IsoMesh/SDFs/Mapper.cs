using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    /// <summary>
    /// This class is a direct port of the SDF GPU code, and lets you do query the distance field on the CPU side. It's best for small queries
    /// which don't need to be parallelized, such as individual raycasts.
    /// </summary>
    public class Mapper
    {
        private SDFGroup.Settings m_settings;
        private IList<SDFGPUData> m_sdfData;
        private IList<SDFMaterialGPU> m_sdfMaterials;
        private IList<float> m_sdfMeshSamples;
        private IList<float> m_sdfMeshPackedUVs;

        // for some reason, the minimum central differences epsilon is larger on the cpu than gpu
        private const float MIN_NORMAL_SMOOTHING_CPU = 0.002f;

        /// <summary>
        /// Get the signed distance to the object represented by the given data object.
        /// </summary>
        private float SDF(Vector3 p, SDFGPUData data)
        {
            if (data.IsMesh)
            {
                Vector3 vec = GetDirectionToMesh(p, data, out float distSign, out Vector3 transformedP);

                return vec.magnitude * distSign * data.Flip;
            }
            else
            {
                p = data.Transform.MultiplyPoint(p);

                switch ((SDFPrimitiveType)(data.Type - 1))
                {
                    case SDFPrimitiveType.Sphere:
                        return MapSphere(p, data.Data.x) * data.Flip;
                    case SDFPrimitiveType.Torus:
                        return MapTorus(p, data.Data) * data.Flip;
                    case SDFPrimitiveType.Cuboid:
                        return MapRoundedBox(p, data.Data, data.Data.w) * data.Flip;
                    case SDFPrimitiveType.Cylinder:
                        return MapCylinder(p, data.Data.x, data.Data.y) * data.Flip;
                    default:
                        return MapBoxFrame(p, data.Data, data.Data.w) * data.Flip;
                }
            }
        }


        private float SDF_Colour(Vector3 p, SDFGPUData data, SDFMaterialGPU material, out Vector4 colour)
        {
            colour = material.Colour;
            return SDF(p, data);
        }

        /// <summary>
        /// Returns the signed distance to the field as a whole.
        /// </summary>
        public float Map(Vector3 p)
        {
            float minDist = 10000000f;

            for (int i = 0; i < m_sdfData.Count; i++)
            {
                SDFGPUData data = m_sdfData[i];

                if (data.IsOperation)
                {
                    p = ElongateSpace(p, data.Data, data.Transform);
                }
                else
                {
                    if (data.CombineType == 0)
                        minDist = SmoothUnion(minDist, SDF(p, data), data.Smoothing);
                    else if (data.CombineType == 1)
                        minDist = SmoothSubtract(SDF(p, data), minDist, data.Smoothing);
                    else
                        minDist = SmoothIntersect(SDF(p, data), minDist, data.Smoothing);
                }
            }

            return minDist;
        }

        float dist_blend_weight(float distA, float distB, float strength)
        {
            float m = 1.0f / Mathf.Max(0.0001f, distA);
            float n = 1.0f / Mathf.Max(0.0001f, distB);
            m = Mathf.Pow(m, strength);
            n = Mathf.Pow(n, strength);
            return Mathf.Clamp01(n / (m + n));
        }

        public Vector4 MapColour(Vector3 p)
        {
            return Vector4.zero;

            //if (m_sdfMaterials.IsNullOrEmpty())
            //    return Vector4.zero;

            ////const float smallNumber = 0.0000001f;
            ////const float bigNumber = 1000000f;

            ////float inverseDistanceSum = 0;
            //float distanceSum = Map(p);
            //Vector3 tempP = p;

            //Color finalColour = (Vector4)m_sdfMaterials[0].Colour;


            //for (int i = 1; i < m_sdfData.Count; i++)
            //{
            //    SDFGPUData data = m_sdfData[i];
            //    SDFMaterialGPU material = m_sdfMaterials[i];

            //    //if (data.IsOperation)
            //    //    tempP = ElongateSpace(tempP, XYZ(data.Data), data.Transform);
            //    //else if (data.CombineType == 0)

            //    //inverseDistanceSum += (1f / Mathf.Clamp(SDF(tempP, data), smallNumber, bigNumber));
            //    float dist = SDF(tempP, data);

            //    float tMat = dist_blend_weight(dist, distanceSum, 1.5f);
            //    tMat = Mathf.Max(0f, Mathf.Min(1.0f, 1.0f - distanceSum / Mathf.Max(0.0001f, 0.25f * data.Smoothing)));
            //    tMat -= 0.5f;
            //    tMat = 0.5f + 0.5f * Mathf.Sign(tMat) * (1.0f - Mathf.Pow(Mathf.Abs(1.0f - Mathf.Abs(2f * tMat)), Mathf.Pow(1f + material.MaterialSmoothing, 5f)));

            //    finalColour = Color.Lerp(finalColour, (Vector4)m_sdfMaterials[i].Colour, tMat);
            //}

            



            ////Vector4 finalColour = Vector4.zero;
            ////tempP = p;

            ////for (int i = 0; i < m_sdfData.Count; i++)
            ////{
            ////    SDFGPUData data = m_sdfData[i];
            ////    SDFMaterialGPU material = m_sdfMaterials[i];

            ////    if (data.IsOperation)
            ////    {
            ////        tempP = ElongateSpace(tempP, XYZ(data.Data), data.Transform);
            ////    }
            ////    else if (data.CombineType == 0)
            ////    {
            ////        float dist = SDF_Colour(tempP, data, material, out Vector4 col);

            ////        //col = Utils.Remap(0f, 1f, 100f, 200f, Saturate(col));
                    
            ////        float inverseDist = 1f / Mathf.Clamp(dist, smallNumber, bigNumber);
            ////        float weight = inverseDist / inverseDistanceSum;
                    
            ////        Utils.Label(p, $"Dist: {dist}, inverseDist: {inverseDist}, weight: {weight}", line: i, col: col);

            ////        finalColour += col * weight;
            ////    }
            ////}

            //////finalColour = Utils.Remap(100f, 200f, 0f, 1f, finalColour);

            ////finalColour = Saturate(finalColour);

            ////Utils.Label(p, "Colour", line: m_sdfData.Count, col: finalColour);
            
            //return finalColour;
        }

        /// <summary>
        /// Returns a normalized gradient value approximated by tetrahedral central differences. Useful for approximating a surface normal.
        /// </summary>
        public Vector3 MapNormal(Vector3 p, float smoothing = -1f) =>
            MapGradient(p, smoothing).normalized;

        /// <summary>
        /// Returns the gradient of the signed distance field at the given point. Gradient point away from surface and their magnitude is
        /// indicative of the rate of change of the field. This value can be normalized to approximate a surface normal.
        /// </summary>
        public Vector3 MapGradient(Vector3 p, float smoothing = -1f)
        {
            float normalSmoothing = smoothing < 0f ? m_settings.NormalSmoothing : smoothing;
            normalSmoothing = Mathf.Max(normalSmoothing, MIN_NORMAL_SMOOTHING_CPU);

            Vector2 e = new Vector2(normalSmoothing, -normalSmoothing);

            return (
                XYY(e) * Map(p + XYY(e)) +
                YYX(e) * Map(p + YYX(e)) +
                YXY(e) * Map(p + YXY(e)) +
                XXX(e) * Map(p + XXX(e)));
        }

        /// <summary>
        /// Raymarch the field. Returns whether the surface was hit, as well as the position and normal of that surface.
        /// </summary>
        public bool Raymarch(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out Vector3 hitNormal, float maxDistance = 350f)
        {
            const int maxIterations = 256;
            const float surfaceDistance = 0.001f;

            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            float distanceToSurface = 0f;
            float rayTravelDistance = 0f;

            // March the distance field until a surface is hit.
            for (int i = 0; i < maxIterations; i++)
            {
                Vector3 p = origin + direction * rayTravelDistance;

                distanceToSurface = Map(p);
                rayTravelDistance += distanceToSurface;

                if (distanceToSurface < surfaceDistance || rayTravelDistance > maxDistance)
                    break;
            }

            if (distanceToSurface < surfaceDistance)
            {
                hitPoint = origin + direction * rayTravelDistance;
                hitNormal = MapNormal(hitPoint);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Set the global settings of the field.
        /// </summary>
        public void SetSettings(SDFGroup.Settings settings) =>
            m_settings = settings;

        /// <summary>
        /// Set the information relating to all sdf objects, such as their positions and rotations.
        /// </summary>
        public void SetData(IList<SDFGPUData> data, IList<SDFMaterialGPU> materials)
        {
            m_sdfData = data;
            m_sdfMaterials = materials;
        }

        /// <summary>
        /// Set the data relating to meshes specifically, including their sampled distances and UVs.
        /// </summary>
        public void SetMeshData(IList<float> meshSamples, IList<float> meshPackedUVs)
        {
            m_sdfMeshSamples = meshSamples;
            m_sdfMeshPackedUVs = meshPackedUVs;
        }

        #region SDF Functions

        public static Vector3 GetNearestPointOnBox(Vector3 p, Vector3 b, Matrix4x4 worldToLocal) =>
            worldToLocal.inverse.MultiplyPoint(GetNearestPointOnBox(worldToLocal.MultiplyPoint(p), b));

        public static Vector3 GetNearestPointOnBox(Vector3 p, Vector3 b) =>
            p + GetBoxGradient(p, b).normalized * -MapBox(p, b);

        private static Vector3 GetBoxGradient(Vector3 p, Vector3 b, Matrix4x4 worldToLocal) =>
            GetBoxGradient(worldToLocal.MultiplyPoint(p), b);

        private static Vector3 GetBoxGradient(Vector3 p, Vector3 b)
        {
            Vector3 d = Abs(p) - b;
            Vector3 s = Sign(p);
            float g = Mathf.Max(d.x, Mathf.Max(d.y, d.z));

            Vector3 derp;

            if (g > 0f)
            {
                derp = Max(d, 0f).normalized;
            }
            else
            {
                derp = Mul(Step(YZX(d), d), Step(ZXY(d), d));
            }

            return Mul(s, derp);
        }

        public static bool IsInBox(Vector3 p, Vector3 b) =>
            MapBox(p, b) < 0f;

        public static bool IsInBox(Vector3 p, Vector3 b, Matrix4x4 worldToLocal) =>
            MapBox(worldToLocal.MultiplyPoint(p), b) < 0f;

        public static float MapBox(Vector3 p, Vector3 b, Matrix4x4 worldToLocal) =>
            MapBox(worldToLocal.MultiplyPoint(p), b);

        public static float MapBox(Vector3 p, Vector3 b)
        {
            Vector3 q = Abs(p) - b;
            return (Max(q, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        public static float MapRoundedBox(Vector3 p, Vector3 b, float r) => MapBox(p, b) - r;

        public static float MapBoxFrame(Vector3 p, Vector3 b, float e)
        {
            p = Abs(p) - b;

            Vector3 eVec = Vector3.one * e;
            Vector3 q = Abs(p + eVec) - eVec;

            float one = Max(new Vector3(p.x, q.y, q.z), 0f).magnitude + Mathf.Min(Mathf.Max(p.x, Mathf.Max(q.y, q.z)), 0f);
            float two = Max(new Vector3(q.x, p.y, q.z), 0f).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(p.y, q.z)), 0f);
            float three = Max(new Vector3(q.x, q.y, p.z), 0f).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, p.z)), 0f);

            return Mathf.Min(one, two, three);
        }

        public static float MapCylinder(Vector3 p, float h, float r)
        {
            Vector2 d = Abs(new Vector2((XZ(p).magnitude), p.y)) - new Vector2(h, r);
            return Mathf.Min(Mathf.Max(d.x, d.y), 0f) + Max(d, 0f).magnitude;
        }

        public static float MapTorus(Vector3 p, Vector2 t)
        {
            Vector2 q = new Vector2(XZ(p).magnitude - t.x, p.y);
            return q.magnitude - t.y;
        }

        public static float MapSphere(Vector3 p, float radius) =>
            p.magnitude - radius;

        // polynomial smooth min (k = 0.1);
        private static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * (1f / 4f);
        }

        private static float SmoothSubtract(float d1, float d2, float k)
        {
            float h = Mathf.Clamp(0.5f - 0.5f * (d2 + d1) / k, 0f, 1f);
            return Mathf.Lerp(d2, -d1, h) + k * h * (1f - h);
        }

        private static float SmoothIntersect(float d1, float d2, float k)
        {
            float h = Mathf.Clamp(0.5f - 0.5f * (d2 - d1) / k, 0f, 1f);
            return Mathf.Lerp(d2, d1, h) + k * h * (1f - h);
        }

        private static Vector3 ElongateSpace(Vector3 p, Vector3 h, Matrix4x4 transform)
        {
            Vector3 translation = transform.ExtractTranslation();
            p = transform.MultiplyPoint(p);
            p = p - Clamp(p + translation, -h, h);
            return transform.inverse.MultiplyPoint(p);
        }


        #endregion

        #region Mesh Functions

        // these functions are all specifically to do with SDFMesh objects only

        // given a point, return the coords of the cell it's in, and the fractional component for interpolation
        private static void GetNearestCoordinates(Vector3 p, SDFGPUData data, out Vector3 coords, out Vector3 fracs, float boundsOffset = 0f)
        {
            p = ClampAndNormalizeToVolume(p, data, boundsOffset);
            int cellsPerSide = data.Size - 1;

            // sometimes i'm not good at coming up with names :U
            Vector3 floored = Floor(p * cellsPerSide);
            coords = Min(floored, cellsPerSide - 1);

            fracs = Frac(p * cellsPerSide);
        }

        private float SampleAssetInterpolated(Vector3 p, SDFGPUData data, float boundsOffset = 0f)
        {
            GetNearestCoordinates(p, data, out Vector3 coords, out Vector3 fracs, boundsOffset);

            int x = (int)coords.x;
            int y = (int)coords.y;
            int z = (int)coords.z;

            float sampleA = GetMeshSignedDistance(x, y, z, data);
            float sampleB = GetMeshSignedDistance(x + 1, y, z, data);
            float sampleC = GetMeshSignedDistance(x, y + 1, z, data);
            float sampleD = GetMeshSignedDistance(x + 1, y + 1, z, data);
            float sampleE = GetMeshSignedDistance(x, y, z + 1, data);
            float sampleF = GetMeshSignedDistance(x + 1, y, z + 1, data);
            float sampleG = GetMeshSignedDistance(x, y + 1, z + 1, data);
            float sampleH = GetMeshSignedDistance(x + 1, y + 1, z + 1, data);

            return Utils.TrilinearInterpolate(fracs, sampleA, sampleB, sampleC, sampleD, sampleE, sampleF, sampleG, sampleH);
        }

        private Vector3 ComputeMeshGradient(Vector3 p, SDFGPUData data, float epsilon, float boundsOffset = 0f)
        {
            // sample the map 4 times to calculate the gradient at that point, then normalize it
            Vector2 e = new Vector2(epsilon, -epsilon);

            return (
                XYY(e) * SampleAssetInterpolated(p + XYY(e), data, boundsOffset) +
                YYX(e) * SampleAssetInterpolated(p + YYX(e), data, boundsOffset) +
                YXY(e) * SampleAssetInterpolated(p + YXY(e), data, boundsOffset) +
                XXX(e) * SampleAssetInterpolated(p + XXX(e), data, boundsOffset)).normalized;
        }

        // returns the vector pointing to the surface of the mesh representation, as well as the sign
        // (negative for inside, positive for outside)
        // this can be used to recreate a signed distance field
        private Vector3 GetDirectionToMesh(Vector3 p, SDFGPUData data, out float distSign, out Vector3 transformedP)
        {
            transformedP = data.Transform.MultiplyPoint(p);

            const float epsilon = 0.75f;
            const float pushIntoBounds = 0.04f;

            // get the distance either at p, or at the point on the bounds nearest p
            float sample = SampleAssetInterpolated(transformedP, data);

            Vector3 closestPoint = GetClosestPointToVolume(transformedP, data, pushIntoBounds);

            Vector3 vecInBounds = (-(ComputeMeshGradient(closestPoint, data, epsilon, pushIntoBounds)).normalized * sample);
            Vector3 vecToBounds = (closestPoint - transformedP);
            Vector3 finalVec = vecToBounds + vecInBounds;

            distSign = Mathf.Sign(sample);

            return finalVec;
        }

        private float GetMeshSignedDistance(int x, int y, int z, SDFGPUData data)
        {
            int index = CellCoordinateToIndex(x, y, z, data);
            return m_sdfMeshSamples[index];
        }

        // clamp the input point to an axis aligned bounding cube of the given bounds
        // optionally can provide an offset which pushes the bounds in or out.
        // this is used to get the position on the bounding cube nearest the given point as 
        // part of the sdf to mesh calculation. the additional push param is used to ensure we have enough
        // samples around our point that we can get a gradient
        private static Vector3 GetClosestPointToVolume(Vector3 p, SDFGPUData data, float boundsOffset = 0f)
        {
            Vector3 minBounds = data.MinBounds;
            Vector3 maxBounds = data.MaxBounds;
            return new Vector3(
                    Mathf.Clamp(p.x, minBounds.x + boundsOffset, maxBounds.x - boundsOffset),
                    Mathf.Clamp(p.y, minBounds.y + boundsOffset, maxBounds.y - boundsOffset),
                    Mathf.Clamp(p.z, minBounds.z + boundsOffset, maxBounds.z - boundsOffset)
                    );
        }

        // ensure the given point is inside the volume, and then smush into the the [0, 1] range
        private static Vector3 ClampAndNormalizeToVolume(Vector3 p, SDFGPUData data, float boundsOffset = 0f)
        {
            // clamp so we're inside the volume
            p = GetClosestPointToVolume(p, data, boundsOffset);

            Vector3 minBounds = data.MinBounds;
            Vector3 maxBounds = data.MaxBounds;

            return new Vector3(
            Mathf.InverseLerp(minBounds.x + boundsOffset, maxBounds.x - boundsOffset, p.x),
            Mathf.InverseLerp(minBounds.y + boundsOffset, maxBounds.y - boundsOffset, p.y),
            Mathf.InverseLerp(minBounds.z + boundsOffset, maxBounds.z - boundsOffset, p.z)
            );
        }

        #endregion

        #region Helper Functions

        // these functions mostly just exist to either replicate some hlsl functionality such as swizzling,
        // or make it easier to convert cell coordinates to world space positions or 1d array indices

        private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector3 XYZ(Vector4 v) => new Vector3(v.x, v.y, v.z);
        private static Vector3 XYY(Vector2 v) => new Vector3(v.x, v.y, v.y);
        private static Vector3 YYX(Vector2 v) => new Vector3(v.y, v.y, v.x);
        private static Vector3 YXY(Vector2 v) => new Vector3(v.y, v.x, v.y);
        private static Vector3 XXX(Vector2 v) => new Vector3(v.x, v.x, v.x);
        private static Vector3 YZX(Vector3 v) => new Vector3(v.y, v.z, v.x);
        private static Vector3 ZXY(Vector3 v) => new Vector3(v.z, v.x, v.y);

        private static Vector3 Mul(Vector3 a, Vector3 b) =>
            new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        private static float Step(float edge, float x) =>
            x < edge ? 0f : 1f;

        private static Vector3 Step(Vector3 val, Vector3 threshold) =>
            new Vector3(Step(val.x, threshold.x), Step(val.y, threshold.y), Step(val.z, threshold.z));

        private static Vector3 Clamp(Vector3 input, float min, float max) =>
            new Vector3(Mathf.Clamp(input.x, min, max), Mathf.Clamp(input.y, min, max), Mathf.Clamp(input.z, min, max));

        private static Vector3 Clamp(Vector3 input, Vector3 min, Vector3 max) =>
            new Vector3(Mathf.Clamp(input.x, min.x, max.x), Mathf.Clamp(input.y, min.y, max.y), Mathf.Clamp(input.z, min.z, max.z));

        private static Vector3 Sign(Vector3 input) =>
            new Vector3(input.x <= 0f ? -1f : 1f, input.y <= 0f ? -1f : 1f, input.z <= 0f ? -1f : 1f);

        private static Vector3 Abs(Vector3 input) =>
            new Vector3(Mathf.Abs(input.x), Mathf.Abs(input.y), Mathf.Abs(input.z));

        private static Vector2 Abs(Vector2 input) =>
            new Vector2(Mathf.Abs(input.x), Mathf.Abs(input.y));

        private static Vector3 Frac(Vector3 input) =>
            new Vector3(input.x - Mathf.Floor(input.x), input.y - Mathf.Floor(input.y), input.z - Mathf.Floor(input.z));

        private static Vector3 Floor(Vector3 input) =>
            new Vector3(Mathf.Floor(input.x), Mathf.Floor(input.y), Mathf.Floor(input.z));

        private static Vector3 Min(Vector3 input1, Vector3 input2) =>
            new Vector3(Mathf.Min(input1.x, input2.x), Mathf.Min(input1.y, input2.y), Mathf.Min(input1.z, input2.z));

        private static Vector3 Max(Vector3 input1, Vector3 input2) =>
            new Vector3(Mathf.Max(input1.x, input2.x), Mathf.Max(input1.y, input2.y), Mathf.Max(input1.z, input2.z));

        private static Vector3 Min(Vector3 input1, int input2) =>
            new Vector3(Mathf.Min(input1.x, input2), Mathf.Min(input1.y, input2), Mathf.Min(input1.z, input2));

        private static Vector3 Max(Vector3 input1, int input2) =>
            new Vector3(Mathf.Max(input1.x, input2), Mathf.Max(input1.y, input2), Mathf.Max(input1.z, input2));

        private static Vector3 Min(Vector3 input1, float input2) =>
            new Vector3(Mathf.Min(input1.x, input2), Mathf.Min(input1.y, input2), Mathf.Min(input1.z, input2));

        private static Vector3 Max(Vector3 input1, float input2) =>
            new Vector3(Mathf.Max(input1.x, input2), Mathf.Max(input1.y, input2), Mathf.Max(input1.z, input2));

        private static Vector2 Saturate(Vector2 input) =>
            new Vector2(Mathf.Clamp01(input.x), Mathf.Clamp01(input.y));

        private static Vector3 Saturate(Vector3 input) =>
            new Vector3(Mathf.Clamp01(input.x), Mathf.Clamp01(input.y), Mathf.Clamp01(input.z));

        private static Vector4 Saturate(Vector4 input) =>
            new Vector4(Mathf.Clamp01(input.x), Mathf.Clamp01(input.y), Mathf.Clamp01(input.z), Mathf.Clamp01(input.w));

        private static Vector3 CellCoordinateToVertex(int x, int y, int z, SDFGPUData data)
        {
            float gridSize = data.Size - 1f;
            Vector3 minBounds = data.MinBounds;
            Vector3 maxBounds = data.MaxBounds;
            float xPos = Mathf.Lerp(minBounds.x, maxBounds.x, x / gridSize);
            float yPos = Mathf.Lerp(minBounds.y, maxBounds.y, y / gridSize);
            float zPos = Mathf.Lerp(minBounds.z, maxBounds.z, z / gridSize);

            return new Vector3(xPos, yPos, zPos);
        }

        private static int CellCoordinateToIndex(int x, int y, int z, SDFGPUData data)
        {
            int size = data.Size;
            return data.SampleStartIndex + (x + y * size + z * size * size);
        }

        #endregion
    }
}