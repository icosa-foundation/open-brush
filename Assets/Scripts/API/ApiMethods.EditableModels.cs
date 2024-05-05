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
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        private static void _PolyFromPath(List<Vector3> path, TrTransform tr, Color color)
        {
            var face = new List<List<int>> { Enumerable.Range(0, path.Count).ToList() };
            var recipe = new PolyRecipe
            {
                Vertices = path,
                Faces = face,
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.GeometryData
            };
            var poly = new PolyMesh(path, face);
            poly.InitTags(color);
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, tr);
        }

        private static void _ApplyOp(int index, PreviewPolyhedron.OpDefinition opDefinition)
        {
            var widget = _GetModelIdByIndex(index);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new EditableModelAddModifierCommand(widget, opDefinition)
            );
        }

        private static EditableModelWidget _GetModelIdByIndex(int index)
        {
            EditableModelWidget widget = GetActiveEditableModel(index);
            return widget;
        }

        [ApiEndpoint(
            "editablemodel.import",
            "Imports a model as editable; given a url, a filename in Media Library\\Models or Google Poly ID",
            "Andy\\Andy.obj"
        )]
        public static ModelWidget ImportEditableModel(string location)
        {
            return _ImportModel(location, true);
        }

        [ApiEndpoint(
            "model.webimport",
            "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)",
            "Andy\\Andy.obj"
        )]
        [ApiEndpoint(
            "import.webmodel",
            "Same as model.webimport (backwards compatibility for poly.pizza)",
            "Andy\\Andy.obj"
        )]
        public static void ImportWebModel(string url)
        {
            Uri uri;
            try { uri = new Uri(url); }
            catch (UriFormatException)
            {
                return;
            }
            var ext = uri.Segments.Last().Split('.').Last();

            // Is it a valid 3d model extension?
            if (ext != "off" && ext != "obj" && ext != "gltf" && ext != "glb" && ext != "fbx" && ext != "svg")
            {
                return;
            }
            var destinationPath = Path.Combine("Models", uri.Host);
            string filename = _DownloadMediaFileFromUrl(uri, destinationPath);
            ImportModel(Path.Combine(uri.Host, filename));
        }

        [ApiEndpoint(
            "model.import",
            "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)",
            "Andy\\Andy.obj"
        )]
        public static ModelWidget ImportModel(string location)
        {
            return _ImportModel(location, false);
        }

        public static ModelWidget _ImportModel(string location, bool editable)
        {
            if (location.StartsWith("poly:"))
            {
                location = location.Substring(5);
                ApiManager.Instance.LoadPolyModel(location);
                return null; // TODO
            }

            // Normalize path slashes
            location = location.Replace(@"\\", "/");
            location = location.Replace(@"//", "/");
            location = location.Replace(@"\", "/");

            var parts = location.Split("#");

            // At this point we've got a relative path to a file in Models
            string relativePath = parts[0];
            string subtree = null;
            if (parts.Length > 1)
            {
                subtree = location.Substring(relativePath.Length + 1);
            }
            var tr = _CurrentTransform().TransformBy(Coords.CanvasPose);
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
                    return null;
                }

                WidgetManager.m_Instance.WidgetsDormant = false;
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                SelectionManager.m_Instance.RemoveFromSelection(false);
                return widget;
            }
            else
            {
                AsyncHelpers.RunSync(() => model.LoadModelAsync());
                model.EnsureCollectorExists();
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.ModelWidgetPrefab, tr, null, forceTransform: true
                );
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
                ModelWidget widget = createCommand.Widget as ModelWidget;
                if (widget != null)
                {
                    widget.Model = model;
                    widget.Subtree = subtree;
                    widget.SyncHierarchyToSubtree();
                    widget.Show(true);
                    widget.AddSceneLightGizmos();
                    createCommand.SetWidgetCost(widget.GetTiltMeterCost());
                }
                else
                {
                    Debug.LogWarning("Failed to create EditableModelWidget");
                    return null;
                }

                WidgetManager.m_Instance.WidgetsDormant = false;
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                SelectionManager.m_Instance.RemoveFromSelection(false);
                return widget;
            }
        }


        private static EditableModelWidget GetActiveEditableModel(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveEditableModelWidgets);
            return WidgetManager.m_Instance.ActiveEditableModelWidgets[index].WidgetScript;
        }

        [ApiEndpoint("editablemodel.stroke.edges", "Create brush strokes for all the edges on an editable model")]
        public static void StrokeEdges(int index)
        {
            var tr = _CurrentTransform();

            var widget = _GetModelIdByIndex(index);
            var poly = widget.m_PolyMesh;
            var alltrs = new List<List<TrTransform>>(poly.Halfedges.Count);

            foreach (var halfedge in poly.Halfedges)
            {
                var orientation = Quaternion.FromToRotation(Vector3.up, halfedge.Vertex.Normal);
                float lineLength = 0;
                var faceVerts = new List<Vector3> { halfedge.Vertex.Position, halfedge.Prev.Vertex.Position };
                faceVerts.Add(faceVerts[0]);
                var trs = new List<TrTransform>(faceVerts.Count);
                for (var i = 0; i < faceVerts.Count; i++)
                {
                    var vert = faceVerts[i];
                    trs.Add(TrTransform.TR(vert, orientation));
                }
            }
            DrawStrokes.DrawNestedTrList(alltrs, tr);
        }

        [ApiEndpoint("editablemodel.stroke.faces", "Create brush strokes for all the Faces on an editable model")]
        public static void StrokeFaces(int index)
        {
            var tr = _CurrentTransform();

            var positions = new List<List<Vector3>>();
            var rotations = new List<List<Quaternion>>();

            var widget = _GetModelIdByIndex(index);
            var poly = widget.m_PolyMesh;
            var alltrs = new List<List<TrTransform>>(poly.Halfedges.Count);

            foreach (var face in poly.Faces)
            {
                var orientation = Quaternion.FromToRotation(Vector3.up, face.Normal);
                float lineLength = 0;
                var faceVerts = face.GetVertices();
                faceVerts.Add(faceVerts[0]);
                var trs = new List<TrTransform>(faceVerts.Count);
                for (var i = 0; i < faceVerts.Count; i++)
                {
                    var vert = faceVerts[i];
                    trs.Add(TrTransform.TR(tr.rotation * vert.Position, orientation));
                }
            }
            DrawStrokes.DrawNestedTrList(alltrs, tr);
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
            var polyRecipe = new PolyRecipe
            {
                Vertices = verts,
                Faces = faces,
                ColorMethod = ColorMethods.ByTags
            };
            poly.MergeCoplanarFaces(smoothing);
            poly.InitTags(stroke.m_Color);
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, polyRecipe, _CurrentTransform());
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
        private static void _ModelFromImage(Texture2D image, float clip = 0.5f)
        {
            var type = GridEnums.GridTypes.Square;
            var shape = GridEnums.GridShapes.Plane;
            var poly = Grids.Build(type, shape, image.width, image.height);
            var pixels = image.GetPixels();
            var faceTags = new List<HashSet<string>>();
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
                    var tag = new HashSet<string> { $"#{ColorUtility.ToHtmlStringRGB(pixelColor)}" };
                    faceTags.Add(tag);
                }
            }
            var filter = new Filter(
                p => clippedFaces.Contains(p.index),
                p => false // Never called
            );
            poly = poly.FaceRemove(new OpParams(filter));
            poly.FaceTags = faceTags;
            var recipe = new PolyRecipe
            {
                Faces = poly.ListFacesByVertexIndices().ToList(),
                Vertices = poly.Vertices.Select(v => v.Position).ToList(),
                GeneratorType = GeneratorTypes.GeometryData,
                ColorMethod = ColorMethods.ByTags,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.createfrom.camerapath", "Generates a filled path from a camera path")]
        public static void CreateFromCameraPath(int index, int segments)
        {
            var widget = _GetActiveCameraPath(index);
            var cameraPath = widget.Path;
            var path = new List<Vector3>();
            var numKnots = cameraPath.PositionKnots.Count;
            for (float i = 0; i < 1f; i += 1f / segments)
            {
                path.Add(cameraPath.GetPosition(new PathT(i * numKnots)));
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

            for (float i = 0; i < 1f; i += 1f / segments)
            {
                verts.Add(cameraPathA.GetPosition(new PathT(i * numKnotsA)));
                verts.Add(cameraPathB.GetPosition(new PathT(i * numKnotsB)));
            }
            var faces = PolyMesh.GenerateQuadStripIndices(verts.Count());
            var poly = new PolyMesh(verts, faces);
            poly.InitTags(App.BrushColor.CurrentColor);
            var recipe = new PolyRecipe
            {
                Faces = poly.ListFacesByVertexIndices().ToList(),
                Vertices = poly.Vertices.Select(v => v.Position).ToList(),
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.GeometryData
            };

            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
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
            var poly = Shapes.Polygon(sides);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.Shapes,
                ShapeType = ShapeTypes.Polygon,
                Param1Int = sides
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.off", "Generates a off from POST data")]
        public static void CreateOff(string offData)
        {
            var poly = new PolyMesh(new StringReader(offData));
            var recipe = new PolyRecipe
            {
                Faces = poly.ListFacesByVertexIndices().ToList(),
                Vertices = poly.Vertices.Select(v => v.Position).ToList(),
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.GeometryData
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
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
            var recipe = new PolyRecipe
            {
                Faces = poly.ListFacesByVertexIndices().ToList(),
                Vertices = poly.Vertices.Select(v => v.Position).ToList(),
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.GeometryData
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.grid", "Generates a grid")]
        public static void CreateGrid(int width, int depth, string type = null, string shape = null)
        {
            GridEnums.GridTypes gridType;
            GridEnums.GridShapes gridShape;

            if (string.IsNullOrEmpty(type))
            {
                gridType = GridEnums.GridTypes.Square;
            }
            else
            {
                type = type.Replace(",", "_").ToUpper();
                gridType = (GridEnums.GridTypes)Enum.Parse(typeof(GridEnums.GridTypes), type);
            }

            if (string.IsNullOrEmpty(shape))
            {
                gridShape = GridEnums.GridShapes.Plane;
            }
            else
            {
                type = type.Replace(",", "_").ToUpper();
                gridShape = (GridEnums.GridShapes)Enum.Parse(typeof(GridEnums.GridShapes), shape);
            }

            var poly = Grids.Build(gridType, gridShape, width, depth);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.RegularGrids,
                GridType = (GridEnums.GridTypes)Enum.Parse(typeof(GridEnums.GridTypes), type),
                GridShape = (GridEnums.GridShapes)Enum.Parse(typeof(GridEnums.GridShapes), shape),
                Param1Int = width,
                Param2Int = depth,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.box", "Generates a box")]
        public static void CreateBox(int width, int height, int depth)
        {
            var type = VariousSolidTypes.Box;
            var poly = VariousSolids.Box(width, height, depth);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Various,
                VariousSolidsType = VariousSolidTypes.Box,
                Param1Int = width,
                Param2Int = height,
                Param3Int = depth,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.sphere", "Generates a sphere")]
        public static void CreateSphere(int width, int height)
        {
            var type = VariousSolidTypes.UvSphere;
            var poly = VariousSolids.UvSphere(width, height);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Various,
                VariousSolidsType = VariousSolidTypes.UvSphere,
                Param1Int = width,
                Param2Int = height,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.hemisphere", "Generates a hemisphere")]
        public static void CreateHemiphere(int width, int height)
        {
            var type = VariousSolidTypes.UvHemisphere;
            var poly = VariousSolids.UvHemisphere(width, height);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Various,
                VariousSolidsType = VariousSolidTypes.UvHemisphere,
                Param1Int = width,
                Param2Int = height,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.polyhedron", "Generates a uniform polyhedron")]
        public static void CreatePolyhedron(string type)
        {
            var wythoff = new WythoffPoly(type);
            var poly = wythoff.Build();
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Uniform,
                // TODO More tolerant parsing of names
                UniformPolyType = Enum.Parse<UniformTypes>(type.Replace(" ", "_").Replace("%20", "_"))
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        // TODO
        // [ApiEndpoint("editablemodel.create.johnsonsolid", "Generates a Johnson Solid")]
        // public static void CreateJohnsonSolid(string type)
        // {
        //     var poly = JohnsonSolids.Build(type);
        //     var parameters = new Dictionary<string, object>
        //     {
        //         {"type", type},
        //     };
        //     EditableModelManager.m_Instance.GeneratePolyMesh(poly, _CurrentTransform(), ColorMethods.ByTags, GeneratorTypes.Johnson, null, 0, parameters);
        // }

        [ApiEndpoint("editablemodel.create.watermansolid", "Generates a Waterman Solid")]
        public static void CreateWatermanSolid(int root, int c)
        {
            var poly = WatermanPoly.Build(1f, root, c, true);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Waterman,
                Param1Int = root,
                Param2Int = c,
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("editablemodel.create.rotationalsolid", "Generates a Rotational Solid (Prism, Pyramid etc")]
        public static void CreateRotationalSolid(string type, int sides, float height1, float height2)
        {
            if (!Enum.TryParse(type, true, out PolyMesh.Operation solidType)) return;
            var poly = RadialSolids.Build((RadialSolids.RadialPolyType)solidType, sides);
            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByRole,
                GeneratorType = GeneratorTypes.Radial,
                RadialPolyType = Enum.Parse<RadialSolids.RadialPolyType>(type),
                Param1Int = sides,
                Param2Float = height1,
                Param3Float = height2
            };
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, recipe, _CurrentTransform());
        }

        [ApiEndpoint("guide.createfrom.editablemodel", "Creates a guide from an editable model")]
        public static void CustomGuideFromEditableModel(int index)
        {
            EditableModelWidget modelWidget = GetActiveEditableModel(index);
            var tr = _CurrentTransform();
            var stencilWidget = EditableModelManager.AddCustomGuide(modelWidget.m_PolyMesh, tr);
            stencilWidget.SetSignedWidgetSize(modelWidget.GetSignedWidgetSize());
        }

        // [ApiEndpoint("editablemodel.modify.color", "Changes the color of an editable model")]
        // public static void ModifyModelColor(int index, Vector3 rgb)
        // {
        //     // TODO
        //     var opDefinition = new PreviewPolyhedron.OpDefinition();
        //     _ApplyOp(index, opDefinition);
        // }

        // [ApiEndpoint("editablemodel.modify.conway", "Apply a Conway operator to a model")]
        // public static void ModifyModelConway(int index, string operation, float param1 = float.NaN, float param2 = float.NaN)
        // {
        //     if (!Enum.TryParse(operation, true, out PolyMesh.Operation op)) return;
        //     var parameters = new Dictionary<string, object>
        //     {
        //         {"type", op},
        //         {"param1", param1},
        //         {"param2", param2},
        //     };
        //     _ApplyOp(index, parameters);
        // }

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
                var startV = verts.Count;
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

            var recipe = new PolyRecipe
            {
                ColorMethod = ColorMethods.ByTags,
                GeneratorType = GeneratorTypes.GeometryData,
                Vertices = verts,
                Faces = faces,
            };

            PolyhydraPanel polyPanel = (PolyhydraPanel)PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Polyhydra);
            recipe.Colors = (Color[])polyPanel.DefaultColorPalette.Clone();
            recipe.Operators = new List<PreviewPolyhedron.OpDefinition>();

            var poly = new PolyMesh(verts, faces);
            poly.MergeCoplanarFaces(smoothing);
            poly.InitTags(App.BrushColor.CurrentColor);
            EditableModelManager.m_Instance.GeneratePolyMesh(
                poly,
                recipe,
                _CurrentTransform()
            );
        }
    }
}
