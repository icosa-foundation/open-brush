// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using IsoMesh;
using UnityEngine;

namespace TiltBrush
{
    /// Converts guides into ordered components owned by an SdfStencil.
    internal static class SdfGuideConversion
    {
        // A plane needs finite volume to participate in the SDF. Keep it thin relative to its
        // smaller visible dimension while preventing it from disappearing at small sizes.
        private const float k_PlaneRelativeThickness = 0.01f;
        private const float k_MinPlaneThickness = 0.0025f;

        internal sealed class Result
        {
            internal readonly TrTransform Pose_GS;
            internal readonly IReadOnlyList<SdfStencil.ComponentDefinition> Components;

            internal Result(
                TrTransform pose_GS,
                IReadOnlyList<SdfStencil.ComponentDefinition> components)
            {
                Pose_GS = pose_GS;
                Components = components;
            }
        }

        internal static Result Build(
            IReadOnlyList<StencilWidget> guides, CanvasScript targetCanvas)
        {
            if (guides == null)
            {
                throw new ArgumentNullException(nameof(guides));
            }
            if (guides.Count == 0)
            {
                throw new ArgumentException(
                    "At least one guide is required for SDF conversion.", nameof(guides));
            }
            if (targetCanvas == null)
            {
                throw new ArgumentNullException(nameof(targetCanvas));
            }

            Vector3 center_GS = Vector3.zero;
            for (int i = 0; i < guides.Count; ++i)
            {
                if (guides[i] == null)
                {
                    throw new ArgumentException(
                        $"Guide {i} is null.", nameof(guides));
                }
                center_GS += guides[i].transform.position;
            }
            center_GS /= guides.Count;

            TrTransform sdfPose_GS = TrTransform.TRS(
                center_GS,
                guides[0].transform.rotation,
                targetCanvas.Pose.scale);
            var orderedGuides = new List<StencilWidget>(guides);
            int sdfCount = orderedGuides.Count(guide => guide is SdfStencil);
            if (sdfCount > 0)
            {
                // An existing SDF can be expanded without changing its scoped CSG expression only
                // when that expression starts the destination component list.
                SdfStencil existing = orderedGuides.OfType<SdfStencil>().First();
                orderedGuides.Remove(existing);
                orderedGuides.Insert(0, existing);
            }

            var definitions = new List<SdfStencil.ComponentDefinition>(guides.Count);
            bool expandedExistingSdf = false;
            foreach (StencilWidget guide in orderedGuides)
            {
                bool expandSdf = guide is SdfStencil && !expandedExistingSdf;
                AddComponentDefinitions(guide, sdfPose_GS, definitions, expandSdf);
                expandedExistingSdf |= expandSdf;
            }
            SdfStencil.ValidateComponentDefinitions(definitions);
            return new Result(sdfPose_GS, definitions);
        }

        private static void AddComponentDefinitions(
            StencilWidget guide, TrTransform sdfPose_GS,
            List<SdfStencil.ComponentDefinition> definitions, bool expandExistingSdf)
        {
            if (guide is SdfStencil sdfGuide)
            {
                if (!expandExistingSdf)
                {
                    if (!sdfGuide.TryGetGeneratedMesh(
                            out Mesh generatedMesh, out Transform meshTransform))
                    {
                        throw new InvalidOperationException(
                            "An existing SDF guide is still generating its scoped mesh operand.");
                    }
                    definitions.Add(CreateGeneratedMeshDefinition(
                        generatedMesh,
                        TrTransform.FromTransform(meshTransform),
                        sdfPose_GS));
                    return;
                }
                TrTransform sourcePose_GS = GuidePose(guide);
                foreach (SdfStencil.ComponentDefinition source in sdfGuide.GetComponentDefinitions())
                {
                    TrTransform transform =
                        sdfPose_GS.inverse * sourcePose_GS * source.Transform;
                    if (source.IsPrimitive)
                    {
                        SdfStencil.PrimitiveDefinition primitive = source.Primitive.Value;
                        definitions.Add(new SdfStencil.ComponentDefinition(
                            new SdfStencil.PrimitiveDefinition(
                                primitive.Type, primitive.Geometry, transform,
                                primitive.Operation, primitive.Blend, primitive.Flip)));
                    }
                    else
                    {
                        definitions.Add(new SdfStencil.ComponentDefinition(
                            source.MeshAsset, transform, source.Operation,
                            source.Blend, source.Flip));
                    }
                }
                return;
            }

            if (guide is ModelStencil modelGuide)
            {
                if (modelGuide.SdfMeshAsset == null)
                {
                    throw new InvalidOperationException(
                        "The model guide has not finished generating its SDF.");
                }
                definitions.Add(CreateMeshDefinition(
                    modelGuide.SdfMeshAsset,
                    TrTransform.FromTransform(modelGuide.transform),
                    sdfPose_GS));
                return;
            }

            if (guide is CustomStencil customGuide)
            {
                Mesh sourceMesh = customGuide.SourceMesh;
                ComputeShader computeShader =
                    WidgetManager.m_Instance.ModelStencilPrefab?.SdfComputeShader;
                if (sourceMesh == null || computeShader == null)
                {
                    throw new InvalidOperationException(
                        "The custom guide cannot generate a mesh SDF.");
                }
                int size = RuntimeSDFGenerator.GetRecommendedSDFSize(
                    sourceMesh.triangles.Length / 3);
                SDFMeshAsset asset = RuntimeSDFGenerator.GenerateSDF(
                    sourceMesh, size, 0.2f, computeShader);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        "SDF generation failed for the custom guide mesh.");
                }
                TrTransform sourcePose_GS = TrTransform.FromTransform(
                    customGuide.SourceMeshTransform);
                definitions.Add(CreateMeshDefinition(asset, sourcePose_GS, sdfPose_GS));
                return;
            }

            definitions.Add(new SdfStencil.ComponentDefinition(
                ToPrimitiveDefinition(guide, sdfPose_GS)));
        }

        private static SdfStencil.ComponentDefinition CreateGeneratedMeshDefinition(
            Mesh sourceMesh, TrTransform sourcePose_GS, TrTransform sdfPose_GS)
        {
            ComputeShader computeShader =
                WidgetManager.m_Instance.ModelStencilPrefab?.SdfComputeShader;
            if (computeShader == null)
            {
                throw new InvalidOperationException(
                    "The SDF mesh generator compute shader is unavailable.");
            }
            int size = RuntimeSDFGenerator.GetRecommendedSDFSize(
                sourceMesh.triangles.Length / 3);
            SDFMeshAsset asset = RuntimeSDFGenerator.GenerateSDF(
                sourceMesh, size, 0.2f, computeShader);
            if (asset == null)
            {
                throw new InvalidOperationException("Scoped SDF mesh generation failed.");
            }
            return CreateMeshDefinition(asset, sourcePose_GS, sdfPose_GS);
        }

        private static SdfStencil.ComponentDefinition CreateMeshDefinition(
            SDFMeshAsset asset, TrTransform sourcePose_GS, TrTransform sdfPose_GS)
        {
            return new SdfStencil.ComponentDefinition(
                asset,
                sdfPose_GS.inverse * sourcePose_GS,
                SDFCombineType.SmoothUnion,
                0f,
                false);
        }

        internal static SdfStencil.PrimitiveDefinition ToPrimitiveDefinition(
            StencilWidget guide, TrTransform sdfPose_GS)
        {
            if (guide == null)
            {
                throw new ArgumentNullException(nameof(guide));
            }
            if (guide.Canvas == null)
            {
                throw new ArgumentException(
                    "A guide must belong to a canvas before conversion.", nameof(guide));
            }

            Vector3 extents = Abs(guide.Extents);
            SDFPrimitiveType primitiveType;
            Vector4 geometry;

            switch (guide.Type)
            {
                case StencilType.Sphere:
                    primitiveType = SDFPrimitiveType.Sphere;
                    geometry = new Vector4(extents.x * 0.5f, 0f, 0f, 0f);
                    break;
                case StencilType.Cube:
                    primitiveType = SDFPrimitiveType.Cuboid;
                    geometry = new Vector4(
                        extents.x * 0.5f, extents.y * 0.5f, extents.z * 0.5f, 0f);
                    break;
                case StencilType.Capsule:
                    primitiveType = SDFPrimitiveType.Capsule;
                    float radius = Mathf.Min(extents.x, extents.z) * 0.5f;
                    float halfSegment = Mathf.Max(0f, extents.y * 0.5f - radius);
                    geometry = new Vector4(radius, halfSegment, 0f, 0f);
                    break;
                case StencilType.Ellipsoid:
                    primitiveType = SDFPrimitiveType.Ellipsoid;
                    geometry = new Vector4(
                        extents.x * 0.5f, extents.y * 0.5f, extents.z * 0.5f, 0f);
                    break;
                case StencilType.Plane:
                    primitiveType = SDFPrimitiveType.Cuboid;
                    float thickness = Mathf.Max(
                        k_MinPlaneThickness,
                        Mathf.Min(extents.x, extents.y) * k_PlaneRelativeThickness);
                    geometry = new Vector4(
                        extents.x * 0.5f, extents.y * 0.5f, thickness * 0.5f, 0f);
                    break;
                case StencilType.Cylinder:
                    primitiveType = SDFPrimitiveType.Cylinder;
                    geometry = new Vector4(
                        Mathf.Min(extents.x, extents.z) * 0.5f, extents.y * 0.5f, 0f, 0f);
                    break;
                case StencilType.Cone:
                    primitiveType = SDFPrimitiveType.Cone;
                    geometry = new Vector4(
                        Mathf.Min(extents.x, extents.z) * 0.5f, extents.y * 0.5f, 0f, 0f);
                    break;
                case StencilType.Pyramid:
                    primitiveType = SDFPrimitiveType.Pyramid;
                    geometry = SdfPrimitiveStencil.GeometryForExtents(primitiveType, extents);
                    break;
                case StencilType.Torus:
                    primitiveType = SDFPrimitiveType.Torus;
                    geometry = SdfPrimitiveStencil.GeometryForExtents(primitiveType, extents);
                    break;
                case StencilType.BoxFrame:
                    primitiveType = SDFPrimitiveType.BoxFrame;
                    geometry = SdfPrimitiveStencil.GeometryForExtents(primitiveType, extents);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Guide type {guide.Type} does not yet have an SDF conversion.");
            }

            TrTransform guidePose_GS = GuidePose(guide);
            TrTransform localTransform = sdfPose_GS.inverse * guidePose_GS;

            return new SdfStencil.PrimitiveDefinition(
                primitiveType,
                geometry,
                localTransform,
                SDFCombineType.SmoothUnion,
                0f,
                false);
        }

        private static TrTransform GuidePose(StencilWidget guide)
        {
            return TrTransform.TRS(
                guide.transform.position,
                guide.transform.rotation,
                guide.Canvas.Pose.scale);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
