// Copyright 2024 The Open Brush Authors
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
//
// Parts are based on:
// https://github.com/darkfall/MagicaVoxelUnity
// Copyright (c) 2015 Ruiwei Bu

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoxReader;
using VoxReader.Interfaces;

namespace TiltBrush
{
    public class VoxImporter
    {
        private readonly Material m_standardMaterial;
        private readonly string m_path;
        private readonly string m_dir;
        private readonly string m_sourceName;
        private readonly byte[] m_voxData;
        private readonly List<string> m_warnings = new List<string>();
        private readonly ImportMaterialCollector m_collector;
        private readonly VoxMeshBuilder m_meshBuilder;
        private readonly MeshMode m_defaultMeshMode;

        // Mesh generation mode
        public enum MeshMode
        {
            Optimized,      // Greedy meshing with face culling
            SeparateCubes   // Individual cube per voxel
        }

        public VoxImporter(string path, MeshMode meshMode = MeshMode.Optimized)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            m_path = path;
            m_dir = Path.GetDirectoryName(path);
            m_sourceName = path;
            m_voxData = null;
            m_collector = new ImportMaterialCollector(m_dir, m_path);
            m_standardMaterial = ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial;
            m_defaultMeshMode = meshMode;
            m_meshBuilder = new VoxMeshBuilder();
        }

        public VoxImporter(byte[] voxData, MeshMode meshMode = MeshMode.Optimized, string sourceName = null)
        {
            if (voxData == null)
            {
                throw new ArgumentNullException(nameof(voxData));
            }

            m_path = null;
            m_dir = string.Empty;
            m_sourceName = string.IsNullOrEmpty(sourceName) ? "in-memory.vox" : sourceName;
            m_voxData = voxData;
            m_collector = new ImportMaterialCollector(m_dir, m_sourceName);
            m_standardMaterial = ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial;
            m_defaultMeshMode = meshMode;
            m_meshBuilder = new VoxMeshBuilder();
        }

        public VoxImporter(ReadOnlyMemory<byte> voxData, MeshMode meshMode = MeshMode.Optimized, string sourceName = null)
            : this(voxData.ToArray(), meshMode, sourceName)
        {
        }

        public VoxImporter(Stream voxStream, MeshMode meshMode = MeshMode.Optimized, string sourceName = null)
            : this(ReadAllBytes(voxStream), meshMode, sourceName)
        {
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import()
        {
            return Import(null);
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import(VoxImportOptions options)
        {
            try
            {
                options ??= new VoxImportOptions
                {
                    MeshMode = m_defaultMeshMode,
                    GenerateCollider = true
                };

                // Read the .vox file using VoxReader library
                IVoxFile voxFile = LoadVoxFile();

                // Create parent GameObject
                string rootName = string.IsNullOrEmpty(options.RootObjectName)
                    ? GetRootObjectName()
                    : options.RootObjectName;
                GameObject parent = new GameObject(rootName);

                // Process each model in the vox file
                IModel[] models = voxFile.Models;

                if (models.Length == 0)
                {
                    m_warnings.Add("VOX file contains no models");
                    return (parent, m_warnings.Distinct().ToList(), m_collector);
                }

                for (int i = 0; i < models.Length; i++)
                {
                    IModel model = models[i];

                    if (model.Voxels.Length == 0)
                    {
                        m_warnings.Add($"Model {i} ({model.Name}) contains no voxels");
                        continue;
                    }

                    GameObject modelObject = new GameObject($"Model_{i}_{model.Name}");
                    modelObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    modelObject.transform.SetParent(parent.transform, false);

                    // Generate mesh based on mode
                    Mesh mesh = options.MeshMode == MeshMode.Optimized
                        ? GenerateOptimizedMesh(model)
                        : GenerateSeparateCubesMesh(model);

                    if (mesh != null)
                    {
                        var mf = modelObject.AddComponent<MeshFilter>();
                        mf.mesh = mesh;

                        var mr = modelObject.AddComponent<MeshRenderer>();
                        mr.material = options.MaterialOverride ?? m_standardMaterial;

                        if (options.GenerateCollider)
                        {
                            var collider = modelObject.AddComponent<BoxCollider>();
                            collider.size = mesh.bounds.size;
                            collider.center = mesh.bounds.center;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"VOX model {i} ({model.Name}): Mesh generation failed");
                    }
                }

                return (parent, m_warnings.Distinct().ToList(), m_collector);
            }
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to import VOX file: {ex.Message}");
                Debug.LogException(ex);
                GameObject errorObject = new GameObject("VOX_Import_Error");
                return (errorObject, m_warnings.Distinct().ToList(), m_collector);
            }
        }

        private IVoxFile LoadVoxFile()
        {
            return m_voxData != null
                ? VoxReader.VoxReader.Read(m_voxData)
                : VoxReader.VoxReader.Read(m_path);
        }

        private string GetRootObjectName()
        {
            string baseName = Path.GetFileNameWithoutExtension(m_sourceName);
            return string.IsNullOrEmpty(baseName) ? "VOX_Import" : baseName;
        }

        private static byte[] ReadAllBytes(Stream source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            using (var memoryStream = new MemoryStream())
            {
                source.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private Mesh GenerateOptimizedMesh(IModel model)
        {
            try
            {
                return m_meshBuilder.GenerateOptimizedMesh(model);
            }
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to generate optimized mesh for model {model.Name}: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }

        private Mesh GenerateSeparateCubesMesh(IModel model)
        {
            try
            {
                return m_meshBuilder.GenerateSeparateCubesMesh(model);
            }
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to generate separate cubes mesh for model {model.Name}: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }
    }
}
