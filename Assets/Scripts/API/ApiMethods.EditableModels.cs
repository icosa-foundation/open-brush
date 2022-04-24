// Copyright 2022 The Open Brush Authors
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
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ObjLoader.Loader.Loaders;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("editablemodel.import", "Imports a model as editable; given a url, a filename in Media Library\\Models or Google Poly ID")]
        public static void ImportEditableModel(string location)
        {
            _ImportModel(location, true);
        }
        
        private static void _ImportModel(string location, bool editable)
        {
            const string modelsFolder = "Models";

            if (location.StartsWith("poly:"))
            {
                location = location.Substring(5);
                ApiManager.Instance.LoadPolyModel(location);
                return;
            }

            if (location.StartsWith("http://") || location.StartsWith("https://"))
            {
                // You can't rely on urls ending with a file extension
                // But try and fall back to assuming web models will be gltf/glb
                // TODO Try deriving from MIME types
                if (location.EndsWith(".off") || location.EndsWith(".obj"))
                {
                    location = _DownloadMediaFileFromUrl(location, modelsFolder);
                }
                else
                {
                    Uri uri = new Uri(location);
                    ApiManager.Instance.LoadPolyModel(uri);
                }
            }

            // At this point we've got a relative path to a file in Models
            string relativePath = location;
            var tr = _CurrentTransform();
            var model = new Model(Model.Location.File(relativePath));
            if (editable)
            {
                model.LoadEditableModel();
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.EditableModelWidgetPrefab, tr);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
                var widget = createCommand.Widget as EditableModelWidget;
                if (widget != null)
                {
                    widget.Model = model;
                    widget.Show(true);
                    createCommand.SetWidgetCost(widget.GetTiltMeterCost());
                }
                else
                {
                    Debug.LogWarning("Failed to create EditableModelWidget");
                    return;
                }

                WidgetManager.m_Instance.WidgetsDormant = false;
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                SelectionManager.m_Instance.RemoveFromSelection(false);
            }
            else
            {
                model.LoadModel();
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.ModelWidgetPrefab, tr);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
                ModelWidget widget = createCommand.Widget as ModelWidget;
                if (widget != null)
                {
                    widget.Model = model;
                    widget.Show(true);
                    createCommand.SetWidgetCost(widget.GetTiltMeterCost());
                }
                else
                {
                    Debug.LogWarning("Failed to create EditableModelWidget");
                    return;
                }

                WidgetManager.m_Instance.WidgetsDormant = false;
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                SelectionManager.m_Instance.RemoveFromSelection(false);
            }
        }
        
        
        private static EditableModelWidget GetActiveEditableModel(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveEditableModelWidgets);
            // Debug.Log($"index: {index} of {WidgetManager.m_Instance.ActiveEditableModelWidgets.Count}");
            // // Debug.Log($"{WidgetManager.m_Instance.ActiveEditableModelWidgets.First()}");
            // Debug.Log($"{WidgetManager.m_Instance.ActiveEditableModelWidgets[0]}");
            return WidgetManager.m_Instance.ActiveEditableModelWidgets[index].WidgetScript;
        }
        
        [ApiEndpoint("editablemodel.stroke.edges", "Create brush strokes for all the edges on an editable model")]
        public static void StrokeEdges(int index)
        {
            var tr = _CurrentTransform();

            var positions = new List<List<Vector3>>();
            var rotations = new List<List<Quaternion>>();

            var widget = _GetModelIdByIndex(index);
            var id = widget.GetId();
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);

            foreach (var halfedge in poly.Halfedges)
            {
                var orientation = Quaternion.FromToRotation(Vector3.up, halfedge.Vertex.Normal);
                float lineLength = 0;
                var faceVerts = new List<Vector3> { halfedge.Vertex.Position, halfedge.Prev.Vertex.Position };
                faceVerts.Add(faceVerts[0]);
                positions.Add(faceVerts);
                rotations.Add(Enumerable.Repeat(orientation, faceVerts.Count).ToList());
            }
            DrawStrokes.MultiPositionPathsToStrokes(positions, rotations, null, tr.translation);
        }
        
        [ApiEndpoint("editablemodel.stroke.faces", "Create brush strokes for all the Faces on an editable model")]
        public static void StrokeFaces(int index)
        {
            var tr = _CurrentTransform();

            var positions = new List<List<Vector3>>();
            var rotations = new List<List<Quaternion>>();

            var widget = _GetModelIdByIndex(index);
            var id = widget.GetId();
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);

            foreach (var face in poly.Faces)
            {
                var orientation = Quaternion.FromToRotation(Vector3.up, face.Normal);
                float lineLength = 0;
                var faceVerts = face.GetVertices();
                faceVerts.Add(faceVerts[0]);
                positions.Add(faceVerts.Select(v => tr.rotation * v.Position).ToList());
                rotations.Add(Enumerable.Repeat(orientation, faceVerts.Count).ToList());
            }
            DrawStrokes.MultiPositionPathsToStrokes(positions, rotations, null, tr.translation);
        }
        
        [ApiEndpoint("editablemodel.createfrom.strokepath", "Creates a new editable model from a brush stroke's path")]
        public static void ModelFromStrokePoints(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            var path = stroke.m_ControlPoints.Select(cp => cp.m_Pos).ToList();
            _PolyFromPath(path, _CurrentTransform(), stroke.m_Color);
        }
        
        [ApiEndpoint("editablemodel.createfrom.strokemesh", "Creates a new editable model from a brush stroke's mesh")]
        public static void ModelFromStrokeMesh(int index, float smoothing = 0.01f)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            BatchSubset subset = stroke.m_BatchSubset;
            var pool = subset.m_ParentBatch.Geometry;
            var faces = new List<List<int>>();

            var startV = subset.m_StartVertIndex;
            var verts = pool.m_Vertices.GetRange(startV, subset.m_VertLength);

            for (var i = subset.m_iTriIndex; i < subset.m_iTriIndex + (subset.m_nTriIndex); i += 3)
            {
                faces.Add(
                    new List<int>
                    {
                        pool.m_Tris[i] - startV,
                        pool.m_Tris[i + 1] - startV,
                        pool.m_Tris[i + 2] - startV
                    }
                );
            }
            var poly = new PolyMesh(verts, faces);
            poly.MergeCoplanarFaces(smoothing);
            poly.InitTags(stroke.m_Color);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"StrokeMesh");
        }
        
        [ApiEndpoint("editablemodel.createfrom.imagewidget", "Creates a new editable model from an image widget")]
        public static void ModelFromImageWidget(int index, float clip)
        {
            var imageWidget = _GetActiveImage(index);
            var image = imageWidget.ReferenceImage.FullSize;
            _ModelFromImage(image, clip);
        }
        
        [ApiEndpoint("editablemodel.createfrom.imagefile", "Creates a new editable model from a local image or a url")]
        public static void ModelFromImageFile(string location, float clip)
        {
            var referenceImage = _LoadReferenceImage(location);
            _ModelFromImage(referenceImage.FullSize, clip);
        }
        
        // Should not be used on anything other than small images.
        // TODO What kind of limits should we enforce here?
        // How big is too big?
        private static void _ModelFromImage(Texture2D image, float clip=0.5f)
        {
            var type = GridEnums.GridTypes.K_4_4_4_4;
            var shape = GridEnums.GridShapes.Plane;
            var poly = Grids.Build(type, shape, image.width, image.height);
            var pixels = image.GetPixels();
            var faceTags = new List<HashSet<Tuple<string, PolyMesh.TagType>>>();
            var clippedFaces = new HashSet<int>();
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixelColor = pixels[i];
                if (pixelColor.a < clip)
                {
                    clippedFaces.Add(i);
                }
                else
                {
                    var tag = new HashSet<Tuple<string, PolyMesh.TagType>>
                    {
                        new Tuple<string, PolyMesh.TagType>(
                            $"#{ColorUtility.ToHtmlStringRGB(pixelColor)}",
                            PolyMesh.TagType.Extrovert
                        )
                    };
                    faceTags.Add(tag);
                }
            }
            poly = poly.FaceRemove(new OpParams(new Filter(p => {
                return clippedFaces.Contains(p.index);
            })));
            poly.FaceTags = faceTags;
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"Grid");
        }
        
        [ApiEndpoint("editablemodel.createfrom.camerapath", "Generates a filled path from a camera path")]
        public static void CreateFromCameraPath(int index, int segments)
        {
            var widget = _GetActiveCameraPath(index);
            var cameraPath = widget.Path;
            var path = new List<Vector3>();
            var numKnots = cameraPath.PositionKnots.Count;
            for (float i=0; i < 1f; i+=1f/segments)
            {
                path.Add(cameraPath.GetPosition(new PathT(i*numKnots)));
            }

            // var path = cameraPath.PositionKnots.Select(k => k.KnotXf.position).ToList();
            _PolyFromPath(path, _CurrentTransform(), App.BrushColor.CurrentColor);
        }
        
        [ApiEndpoint("editablemodel.createfrom.camerapaths", "Generates a surface from two camera paths")]
        public static void CreateFromCameraPaths(int indexA, int indexB, int segments)
        {
            var widgetA = _GetActiveCameraPath(indexA);
            var cameraPathA = widgetA.Path;

            var widgetB = _GetActiveCameraPath(indexB);
            var cameraPathB = widgetB.Path;
            var verts = new List<Vector3>();
            var numKnotsA = cameraPathA.PositionKnots.Count;
            var numKnotsB = cameraPathB.PositionKnots.Count;

            for (float i=0; i < 1f; i+=1f/segments)
            {
                verts.Add(cameraPathA.GetPosition(new PathT(i*numKnotsA)));
                verts.Add(cameraPathB.GetPosition(new PathT(i*numKnotsB)));
            }
            var faces = PolyMesh.GenerateQuadStripIndices(verts.Count());
            var poly = new PolyMesh(verts, faces);
            poly.InitTags(App.BrushColor.CurrentColor);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"ConvertedCameraPath");
        }
        
        [ApiEndpoint("editablemodel.create.path", "Generates a filled path")]
        public static void CreatePath(string jsonString)
        {
            var tr = _CurrentTransform();
            var path = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]")
                .Select(x => _RotatePointAroundPivot(new Vector3(x[0], x[1], x[2]), tr.translation, tr.rotation))
                .ToList();
            _PolyFromPath(path, _CurrentTransform(), App.BrushColor.CurrentColor);
        }
      
        
        [ApiEndpoint("editablemodel.create.polygon", "Generates a regular polygon")]
        public static void CreatePolygon(int sides)
        {
            var poly = Shapes.MakePolygon(sides);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"{sides}-sided Polygon");
        }

        [ApiEndpoint("editablemodel.create.off", "Generates a off from POST data")]
        public static void CreateOff(string offData)
        {
            var poly = new PolyMesh(new StringReader(offData));
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags, "generated off file");
        }
        
        [ApiEndpoint("editablemodel.create.obj", "Generates a obj from POST data")]
        public static void CreateObj(string objData)
        {
        
            var objLoaderFactory = new ObjLoaderFactory();
            var objLoader = objLoaderFactory.Create();

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(objData);
            writer.Flush();
            stream.Position = 0;
            var result = objLoader.Load(stream);
            var verts = result.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z));
            var faceIndices = result
                .Groups
                .SelectMany(g => g.Faces)
                .Select(f => f._vertices.Select(v => v.VertexIndex - 1));
            var poly = new PolyMesh(verts, faceIndices);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByRole, "generated obj file");
        }

        [ApiEndpoint("editablemodel.create.grid", "Generates a grid")]
        public static void CreateGrid(int widthSegs, int depthSegs)
        {
            var type = GridEnums.GridTypes.K_4_4_4_4;
            var shape = GridEnums.GridShapes.Plane;
            var poly = Grids.Build(type, shape, widthSegs, depthSegs);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"Grid");
        }
        
        [ApiEndpoint("guide.createfrom.editablemodel", "Creates a guide from an editable model")]
        public static void CustomGuideFromEditableModel(int index)
        {
            EditableModelWidget modelWidget = GetActiveEditableModel(index);

            var tr = _CurrentTransform();
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(StencilType.Custom), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var stencilWidget = createCommand.Widget as StencilWidget;
            var poly = EditableModelManager.m_Instance.GetPolyMesh(modelWidget);
            poly = poly.ConvexHull();
            Mesh mesh = poly.BuildUnityMesh(colorMethod: PolyMesh.ColorMethods.ByRole);
            var collider = stencilWidget.GetComponentInChildren<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.GetComponentInChildren<MeshFilter>().mesh = mesh;
            stencilWidget.SetSignedWidgetSize(modelWidget.GetSignedWidgetSize());
        }
        
        [ApiEndpoint("editablemodel.modify.color", "Changes the color of an editable model")]
        public static void ModifyModelColor(int index, Vector3 rgb)
        {
            var widget = _GetModelIdByIndex(index);
            var id = widget.GetId();
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);
            poly.InitTags(new Color(rgb.x, rgb.y, rgb.z));
            EditableModelManager.m_Instance.RegenerateMesh(widget, poly);
        }
        
        [ApiEndpoint("editablemodel.modify.addnoise", "Moves the points of a model by adding noise in the chosen direction")]
        public static void ModifyModelAddNoise(int index, string direction, float strength = 5, float xscale = .1f, float yscale = .1f)
        {
            var widget = _GetModelIdByIndex(index);
            var id = widget.GetId();
            if (!Enum.TryParse(direction, true, out PolyMesh.Axes axis)) return;
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);
            poly.ApplyNoise(axis, strength, xscale, yscale, Random.value, Random.value);
            EditableModelManager.m_Instance.RegenerateMesh(widget, poly);
        }
        
        [ApiEndpoint("editablemodel.modify.conway", "Apply a Conway operator to a model")]
        public static void ModifyModelConway(int index, string operation, float param1 = float.NaN, float param2 = float.NaN)
        {
            var widget = _GetModelIdByIndex(index);
            if (!Enum.TryParse(operation, true, out PolyMesh.Operation op)) return;
            _ApplyOp(widget, op, param1, param2);
        }
        
        [ApiEndpoint("editablemodel.createfrom.model", "Creates a new editable model from an existing model")]
        // TODO transfer color and/or textures
        public static void ConvertModelToEditable(int index, float smoothing = 0.01f)
        {
            ModelWidget widget = _GetActiveModel(index);
            widget.enabled = false;
            var meshes = widget.Model.GetMeshes();
            var faces = new List<List<int>>();
            var verts = new List<Vector3>();

            foreach (var mf in meshes)
            {
                var startV = verts.Count - 1;
                verts.AddRange(mf.mesh.vertices);

                var tris = mf.mesh.triangles;
                for (var i = 0; i < tris.Length; i += 3)
                {
                    faces.Add(
                        new List<int>
                        {
                            tris[i] + startV,
                            tris[i + 1] + startV,
                            tris[i + 2] + startV
                        }
                    );
                }
            }

            var poly = new PolyMesh(verts, faces);
            poly.MergeCoplanarFaces(smoothing);
            poly.InitTags(App.BrushColor.CurrentColor);
            _GeneratePolyMesh(poly, _CurrentTransform(), PolyMesh.ColorMethods.ByTags,"ConvertedModel");
        }
    }
}
