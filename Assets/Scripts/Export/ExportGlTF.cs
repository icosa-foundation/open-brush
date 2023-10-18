// Copyright 2020 The Tilt Brush Authors
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

using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Extensions;
using static TiltBrush.ExportUtils;

namespace TiltBrush
{

    // Exports scene to glTF format. Work in progress.
    public class ExportGlTF
    {
        public struct ExportResults
        {
            public bool success;
            public string[] exportedFiles;
        }

        // The ExportManifest is a record of what brushes are available for export and their associated
        // textures and material parameters.
        [Serializable]
        public class ExportManifest
        {
            public string tiltBrushVersion;
            public string tiltBrushBuildStamp;
            public Dictionary<Guid, ExportedBrush> brushes = new Dictionary<Guid, ExportedBrush>();
        }

        // As each GeometryPool is emitted, the glTF mesh is memoized and shared across all nodes which
        // reference the same pool.
        private Dictionary<GeometryPool, GLTFMesh> m_meshCache = new ();

        [Serializable]
        public class ExportedBrush
        {
            public Guid guid;
            public string name;
            /// All brush files are found in folderName: vertexShader, fragmentShader, and textures.
            public string folderName;
            public string shaderVersion;
            /// Versioned file name of vertex shader; relative to folderName
            public string vertexShader;
            /// Versioned file name of fragment shader; relative to folderName
            public string fragmentShader;
            public ExportableMaterialBlendMode blendMode;
            public bool enableCull;
            public Dictionary<string, string> textures = new Dictionary<string, string>();
            public Dictionary<string, Vector2> textureSizes = new Dictionary<string, Vector2>();
            public Dictionary<string, float> floatParams = new Dictionary<string, float>();
            public Dictionary<string, Vector3> vectorParams = new Dictionary<string, Vector3>();
            public Dictionary<string, Color> colorParams = new Dictionary<string, Color>();
        }

        private GLTFSceneExporter m_exporter;

        // This exports the scene into glTF. Brush strokes are exported in the style of the FBX exporter
        // by building individual meshes for the brush strokes, merging them by brush type, and exporting
        // the merged meshes. The merged meshes are split at 64k vert boundaries as required by Unity.
        // Also, scene lights are exported.
        // Pass:
        //   doExtras - true to add a bunch of poly-specific metadata to the scene
        //   selfContained - true to force a more-compatible gltf that doesn't have http:// URIs
        //     The drawback is that the result is messier and contains data that TBT does not need.
        public ExportResults ExportBrushStrokes(
            string outputFile, AxisConvention axes, bool binary, bool doExtras,
            bool includeLocalMediaContent, int gltfVersion,
            bool selfContained = false)
        {
            var payload = ExportCollector.GetExportPayload(
                axes,
                includeLocalMediaContent: includeLocalMediaContent,
                temporaryDirectory: Path.Combine(Application.temporaryCachePath, "exportgltf"));
            return ExportHelper(payload, outputFile, binary, doExtras: doExtras, gltfVersion: gltfVersion,
                allowHttpUri: !selfContained);
        }
#if false
  // This exports a game object into glTF. Brush strokes are exported in the style of the FBX
  // exporter by building individual meshes for the game object, merging them by brush type, and
  // exporting the merged meshes. The merged meshes are split at 64k vert boundaries as required by
  // Unity. Environment data can also be exported.
  public ExportResults ExportGameObject(GameObject gameObject, string outputFile,
                                        Environment env = null,
                                        bool binary = false) {

    var payload = ExportUtils.GetSceneStateForGameObjectForExport(
        gameObject,
        AxisConvention.kGltfAccordingToPoly,
        env);

    return ExportHelper(payload, outputFile, binary, doExtras: false);
  }
#endif
        private ExportResults ExportHelper(
            SceneStatePayload payload,
            string outputFile,
            bool binary,
            bool doExtras,
            int gltfVersion,
            bool allowHttpUri)
        {
            // TODO: Ownership of this temp directory is sloppy.
            // Payload and export share the same dir and we assume that the exporter:
            // 1. will not write files whose names conflict with payload's
            // 2. will clean up the entire directory when done
            // This works, as long as the payload isn't used for more than one export (it currently isn't)

            var exportOptions = new ExportOptions();
            var exporter = new GLTFSceneExporter(App.Scene.transform, exportOptions);

            // payload.temporaryDirectory, gltfVersion,
            // App.UserConfig.Flags.LargeMeshSupport)
            // exporter.AllowHttpUri = allowHttpUri;

            try
            {
                m_exporter = exporter;

                exporter.SaveGLB(Path.GetFileName(outputFile), Path.GetDirectoryName(outputFile));
                if (doExtras)
                {
                    SetExtras(exporter, payload);
                }

                if (payload.env.skyCubemap != null)
                {
                    // Add the skybox texture to the export.
                    string texturePath = ExportUtils.GetTexturePath(payload.env.skyCubemap);
                    string textureFilename = Path.GetFileName(texturePath);
                    var root = exporter.GetRoot();
                    ExportFileReference environmentSkybox =
                        ExportFileReference.CreateLocal(texturePath, textureFilename);
                    root.Extras["TB_EnvironmentSkybox"] = environmentSkybox.ToString();
                }

                WriteObjectsAndConnections(exporter, payload);

                exporter.SaveGLB(Path.GetFileName(outputFile), Path.GetDirectoryName(outputFile));
                string[] exportedFiles = { outputFile };
                return new ExportResults
                {
                    success = true,
                    exportedFiles = exportedFiles
                };
            }
            catch (InvalidOperationException e)
            {
                OutputWindowScript.Error("glTF export failed", e.Message);
                // TODO: anti-pattern. Let the exception bubble up so caller can log it properly
                // Actually, InvalidOperationException is now somewhat expected in experimental, since
                // the gltf exporter does not check IExportableMaterial.SupportsDetailedMaterialInfo.
                // But we still want the logging for standalone builds.
                Debug.LogException(e);
                return new ExportResults { success = false };
            }
            catch (IOException e)
            {
                OutputWindowScript.Error("glTF export failed", e.Message);
                return new ExportResults { success = false };
            }
            finally
            {
                payload.Destroy();
                // The lifetime of ExportGlTF, GlTF_ScriptableExporter, and GlTF_Globals instances
                // is identical. This is solely to be pedantic.
                m_exporter = null;
            }

        }

        static string CommaFormattedFloatRGB(Color c)
        {
            return string.Format("{0}, {1}, {2}", c.r, c.g, c.b);
        }
        static string CommaFormattedVector3(Vector3 v)
        {
            return string.Format("{0}, {1}, {2}", v.x, v.y, v.z);
        }

        // Populates glTF metadata and scene extras fields.
        private void SetExtras(GLTFSceneExporter exporter, SceneStatePayload payload)
        {
            Color skyColorA = payload.env.skyColorA;
            Color skyColorB = payload.env.skyColorB;
            Vector3 skyGradientDir = payload.env.skyGradientDir;

            var root = exporter.GetRoot();

            // Scene-level extras:
            root.Extras["TB_EnvironmentGuid"] = payload.env.guid.ToString("D");
            root.Extras["TB_Environment"] = payload.env.description;
            root.Extras["TB_UseGradient"] = payload.env.useGradient ? "true" : "false";
            root.Extras["TB_SkyColorA"] = CommaFormattedFloatRGB(skyColorA);
            root.Extras["TB_SkyColorB"] = CommaFormattedFloatRGB(skyColorB);
            Matrix4x4 exportFromUnity = AxisConvention.GetFromUnity(payload.axes);
            root.Extras["TB_SkyGradientDirection"] = CommaFormattedVector3(
                exportFromUnity * skyGradientDir);
            root.Extras["TB_FogColor"] = CommaFormattedFloatRGB(payload.env.fogColor);
            root.Extras["TB_FogDensity"] = payload.env.fogDensity.ToString();

            // TODO: remove when Poly starts using the new color data
            root.Extras["TB_SkyColorHorizon"] = CommaFormattedFloatRGB(skyColorA);
            root.Extras["TB_SkyColorZenith"] = CommaFormattedFloatRGB(skyColorB);
        }

        // Returns a Node; null means "there is no node for this group".
        public Node GetGroupNode(uint groupId)
        {
            Node node;
            try
            {
                node = m_exporter.GetRoot().Nodes.Find(x => x.Name == $"group_{groupId}");
            }
            catch (InvalidOperationException e)
            {
                node = new Node();

            }
            return node;
        }

        // Doesn't do material export; for that see ExportMeshPayload
        private Node ExportMeshPayload_NoMaterial(
            BaseMeshPayload mesh,
            Node parent,
            Matrix4x4? localXf = null)
        {

            string meshNameAndId = mesh.legacyUniqueName;
            GeometryPool pool = mesh.geometry;
            Matrix4x4 xf = localXf ?? mesh.xform;

            // Create a Node and (usually) a Mesh, both named after meshNameAndId.
            // This is safe because the namespaces for Node and Mesh are distinct.
            // If we have already seen the GeometryPool, the Mesh will be reused.
            // In this (less common) case, the Node and Mesh will have different names.

            // We don't actually ever use the "VERTEXID" attribute, even in gltf1.
            // It's time to cut it away.
            // Also, in gltf2, it needs to be called _VERTEXID anyway since it's a custom attribute

            // AndyB: TODO
            // GlTF_VertexLayout gltfLayout = new GlTF_VertexLayout(G, pool.Layout);

            int numTris = pool.NumTriIndices / 3;
            if (numTris < 1) {
                return null;
            }

            // NumTris += numTris;

            GLTFMesh gltfMesh;

            // Share meshes for any repeated geometry pool.
            if (!m_meshCache.TryGetValue(pool, out gltfMesh)) {
                gltfMesh = new GLTFMesh();
                gltfMesh.Name = meshNameAndId;
                m_meshCache.Add(pool, gltfMesh);

                // Populate mesh data only once.
                AddMeshDependencies(meshNameAndId, mesh.exportableMaterial, gltfMesh);
                Populate(gltfMesh, pool);
                m_exporter.GetRoot().Meshes.Add(gltfMesh);
            }

            // The mesh may or may not be shared, but every mesh will have a distinct node to allow them
            // to have unique transforms.
            var nodes = m_exporter.GetRoot().Nodes;
            Node node;
            try
            {
                node = nodes.First(n => n.Name == meshNameAndId);
            }
            catch (InvalidOperationException e)
            {
                node = new Node();
                node.Name = meshNameAndId;
                node.Matrix = xf.ToGltfMatrix4x4Convert();
            }
            var uniqueMesh = new List<GLTFSceneExporter.UniquePrimitive>
            {
                new(){ Mesh = mesh.geometry.GetBackingMesh() }
            };
            var meshId = m_exporter.ExportMesh(mesh.geometryName, uniqueMesh);
            node.Mesh = meshId;
            return node;
        }

        public void Populate(GLTFMesh mesh, GeometryPool pool)
        {
            pool.EnsureGeometryResident();

            // if (mesh.Primitives.Count > 0) {
            //     // Vertex data is shared among all the primitives in the mesh, so only do [0]
            //     mesh.Primitives[0].Attributes.Populate(pool);
            //     mesh.Primitives[0].Populate(pool, largeIndices);
            //
            //     // I guess someone might want to map Unity submeshes -> gltf primitives.
            //     // - First you'd want to make sure that consuming tools won't freak out about that,
            //     //   since it doesn't seem to be the intended use for the mesh/primitive distinction.
            //     //   See https://github.com/KhronosGroup/glTF/issues/1278
            //     // - Then you'd want Populate() to take multiple GeometryPools, one per MeshSubset.
            //     // - Then you'd want those GeometryPools to indicate somehow whether their underlying
            //     //   vertex data is or can be shared -- maybe do this in GeometryPool.FromMesh()
            //     //   by having them point to the same Lists.
            //     // - Then you'd want to make GlTF_attributes.Populate() smart enough to understand that
            //     //   sharing (ie, memoizing on the List<Vector3> pointer)
            //     // None of that is implemented, which is okay since our current gltf generation
            //     // code doesn't add more than one GlTF_Primitive per GlTF_Mesh.
            //     if (mesh.Primitives.Count > 1) {
            //         Debug.LogError("More than one primitive per mesh is unimplemented and unsupported");
            //     }
            // }

            // The mesh data is only ever needed once (because it only goes into the .bin
            // file once), but ExportMeshGeomPool still uses bits of data like pool.NumTris
            // so we can't destroy it.
            //
            // We could MakeNotResident(filename) again, but that's wasteful and I'd need to
            // add an API to get the cache filename. So this "no coming back" API seems like
            // the most expedient solution.
            // TODO: replace this hack with something better? eg, a way to reload from
            // file without destroying the file?
            // pool.Destroy();
            pool.MakeGeometryPermanentlyNotResident();
        }

        // Returns the gltf mesh that corresponds to the payload, or null.
        // Currently, null is only returned if the payload's 'geometry pool is empty.
        // Pass a localXf to override the default, which is to use base.xform
        private Node ExportMeshPayload(
            SceneStatePayload payload,
            BaseMeshPayload meshPayload,
            Node parent,
            Matrix4x4? localXf = null)
        {

            // Node node = GetGroupNode(meshPayload.group);
            // GLTFMesh mesh = node.Mesh.Value;
            // var prims = exporter.GetPrimitivesForMesh(meshPayload.geometry.GetBackingMesh());
            // mesh.Primitives.AddRange(prims.Select(p => p.prim));
            // exporter.GetRoot().Nodes.Add(node);



            var node = ExportMeshPayload_NoMaterial(meshPayload, parent, localXf);

            if (node != null) {
                // IExportableMaterial exportableMaterial = meshPayload.exportableMaterial;
                // List<GLTFMaterial> materials = m_exporter.GetRoot().Materials;
                //
                // if (!materials.Contains(exportableMaterial))
                // {
                //     var prims = node.Mesh?.Value.Primitives;
                //     var attrs = (prims != null && prims.Count > 0) ? prims[0].Attributes : null;
                //     if (attrs != null) {
                //         ExportMaterial(payload, meshPayload.MeshNamespace, exportableMaterial, attrs);
                //         // Debug.Assert(m_exporter.GetRoot().Materials.Exists(exportableMaterial.name));name
                //     }
                // }
            }
            return node;
        }

        // Adds to gltfMesh the glTF dependencies (primitive, material, technique, program, shaders)
        // required by unityMesh, using matObjName for naming the various material-related glTF
        // components. This does not add any geometry from the mesh (that's done separately using
        // GlTF_Mesh.Populate()).
        //
        // This does not create the material either. It adds a reference to a material that
        // presumably will be created very soon (if it hasn't previously been created).
        private void AddMeshDependencies(
            string meshName, IExportableMaterial exportableMaterial, GLTFMesh gltfMesh)
        {
            var primitive = new MeshPrimitive();

            var indexSize = GLTFComponentType.UnsignedInt;
            var accessors = m_exporter.GetRoot().Accessors;
            var indexAccessor = new Accessor();
            // indexAccessor.Name = Accessor.GetNameFromObject(meshName, "indices_0");
            indexAccessor.Name = meshName;
            indexAccessor.Type = GLTFAccessorAttributeType.SCALAR;
            indexAccessor.ComponentType = indexSize;
            // indexAccessor.isNonVertexAttributeAccessor = true;
            accessors.Add(indexAccessor);

            // AndyB: TODO
            // primitive.Indices = indexAccessor.;
            if (gltfMesh.Primitives.Count > 0) {
                Debug.LogError("More than one primitive per mesh is unimplemented and unsupported");
            }
            gltfMesh.Primitives.Add(primitive);

            // This needs to be a forward-reference (ie, by name) because G.materials[exportableMaterial]
            // may not have been created yet.
            // AndyB: TODO
            //primitive.Material = GlTF_Material.GetNameFromObject(exportableMaterial);
        }


        // Pass:
        //   meshNamespace - A string used as the "namespace" of the mesh that owns this material.
        //     Useful for uniquifying names (texture file names, material names, ...) in a
        //     human-friendly way.
        //   hack - attributes of some mesh that uses this material
        private void ExportMaterial(
            SceneStatePayload payload,
            string meshNamespace,
            IExportableMaterial exportableMaterial,
            Dictionary<string, AccessorId> hack)
        {
            throw new NotImplementedException();

            // //
            // // Set culling and blending modes.
            // //
            // m_exporter.DeclareExtensionUsage("KHR_techniques_webgl");
            // m_exporter.GetRoot().AddExtension("KHR_techniques_webgl");
            // GlTF_Technique.States states = new GlTF_Technique.States();
            // m_exporter.GetRoot().techqu
            // m_techniqueStates[exportableMaterial] = states;
            // // Everyone gets depth test
            // states.enable = new[] { GlTF_Technique.Enable.DEPTH_TEST }.ToList();
            //
            // if (exportableMaterial.EnableCull)
            // {
            //     states.enable.Add(GlTF_Technique.Enable.CULL_FACE);
            // }
            //
            // if (exportableMaterial.BlendMode == ExportableMaterialBlendMode.AdditiveBlend)
            // {
            //     states.enable.Add(GlTF_Technique.Enable.BLEND);
            //     // Blend array format: [srcRGB, dstRGB, srcAlpha, dstAlpha]
            //     states.functions["blendFuncSeparate"] =
            //         new GlTF_Technique.Value(G, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // Additive.
            //     states.functions["depthMask"] = new GlTF_Technique.Value(G, false);   // No depth write.
            //     // Note: If we switch bloom to use LDR color, adding the alpha channels won't do.
            //     // GL_MIN would be a good choice for alpha, but it's unsupported by glTF 1.0.
            // }
            // else if (exportableMaterial.BlendMode == ExportableMaterialBlendMode.AlphaBlend)
            // {
            //     states.enable.Add(GlTF_Technique.Enable.BLEND);
            //     // Blend array format: [srcRGB, dstRGB, srcAlpha, dstAlpha]
            //     // These enum values correspond to: [ONE, ONE_MINUS_SRC_ALPHA, ONE, ONE_MINUS_SRC_ALPHA]
            //     states.functions["blendFuncSeparate"] =
            //         new GlTF_Technique.Value(G, new Vector4(1.0f, 771.0f, 1.0f, 771.0f)); // Blend.
            //     states.functions["depthMask"] = new GlTF_Technique.Value(G, true);
            // }
            // else
            // {
            //     // Standard z-buffering: Enable depth write.
            //     states.functions["depthMask"] = new GlTF_Technique.Value(G, true);
            // }
            //
            // // First add the material, then export any per-material attributes, such as shader uniforms.
            // AddMaterialWithDependencies(exportableMaterial, meshNamespace, hack);
            //
            // // Add lighting for this material.
            // AddLights(exportableMaterial, payload);
            //
            // //
            // // Export shader/material parameters.
            // //
            //
            // foreach (var kvp in exportableMaterial.FloatParams)
            // {
            //     ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
            // }
            // foreach (var kvp in exportableMaterial.ColorParams)
            // {
            //     ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
            // }
            // foreach (var kvp in exportableMaterial.VectorParams)
            // {
            //     ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
            // }
            // foreach (var kvp in exportableMaterial.TextureSizes)
            // {
            //     float width = kvp.Value.x;
            //     float height = kvp.Value.y;
            //     ExportShaderUniform(exportableMaterial, kvp.Key + "_TexelSize",
            //         new Vector4(1 / width, 1 / height, width, height));
            // }
            //
            // //
            // // Export textures.
            // //
            // foreach (var kvp in exportableMaterial.TextureUris)
            // {
            //     string textureName = kvp.Key;
            //     string textureUri = kvp.Value;
            //
            //     ExportFileReference fileRef;
            //     if (ExportFileReference.IsHttp(textureUri))
            //     {
            //         // Typically this happens for textures used by BrushDescriptor materials
            //         fileRef = CreateExportFileReferenceFromHttp(textureUri);
            //     }
            //     else
            //     {
            //         fileRef = ExportFileReference.GetOrCreateSafeLocal(
            //             G.m_disambiguationContext, textureUri, exportableMaterial.UriBase,
            //             $"{meshNamespace}_{Path.GetFileName(textureUri)}");
            //     }
            //
            //     AddTextureToMaterial(exportableMaterial, textureName, fileRef);
            // }
        }

        private void WriteObjectsAndConnections(GLTFSceneExporter exporter,
                                                SceneStatePayload payload)
        {
            foreach (BrushMeshPayload meshPayload in payload.groups.SelectMany(g => g.brushMeshes))
            {
                ExportMeshPayload(payload, meshPayload, GetGroupNode(meshPayload.group), localXf: meshPayload.xform);
            }

            foreach (var sameInstance in payload.modelMeshes.GroupBy(m => (m.model, m.modelId)))
            {
                var modelMeshPayloads = sameInstance.ToList();
                if (modelMeshPayloads.Count == 0) { continue; }

                // All of these pieces will come from the same Widget and therefore will have
                // the same group id, root transform, etc
                var first = modelMeshPayloads[0];
                Node groupNode = GetGroupNode(first.group);

                // Non-Poly exports get a multi-level structure for meshes: transform node on top,
                // all the contents as direct children.
                string rootNodeName = $"model_{first.model.GetExportName()}_{first.modelId}";
                if (modelMeshPayloads.Count == 1 && first.localXform.isIdentity)
                {
                    // Condense the two levels into one; give the top-level node the same name
                    // it would have had had it been multi-level.
                    Node newNode = ExportMeshPayload(payload, first, groupNode);
                    // newNode.PresentationNameOverride = rootNodeName;
                    newNode.Name = rootNodeName;
                }
                else
                {
                    // The new code's been tested with Poly and works fine, but out of
                    // an abundance of caution, keep Poly unchanged
                    foreach (var modelMeshPayload in modelMeshPayloads)
                    {
                        ExportMeshPayload(payload, modelMeshPayload, groupNode);
                    }
                }
            }

            foreach (ImageQuadPayload meshPayload in payload.imageQuads)
            {
                ExportMeshPayload(payload, meshPayload, GetGroupNode(meshPayload.group));
            }

            foreach (var (xformPayload, i) in payload.referenceThings.WithIndex())
            {
                string uniqueName = $"empty_{xformPayload.name}_{i}";
                var node = new Node();
                node.Name = uniqueName;
                node.Matrix = xformPayload.xform.ToGltfMatrix4x4Convert();
                // parent = exporter.GetRoot()
                // node.PresentationNameOverride = $"empty_{xformPayload.name}";
            }
        }
    }

} // namespace TiltBrush
