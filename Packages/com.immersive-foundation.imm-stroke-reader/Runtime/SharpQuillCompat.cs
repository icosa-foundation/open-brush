using System;
using System;
using System.Collections.Generic;
using ImmPlayer;

namespace ImmStrokeReader.SharpQuill
{
    // This is a minimal, SharpQuill-like data model for stroke drawing.
    // It intentionally does not depend on the real SharpQuill C# types so it can live in a reusable UPM package.

    public static class QuillSequenceReader
    {
        private static bool s_LayerTransformApiAvailable = true;

        public static Sequence ReadImm(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                string buildId = ImmPlayer.ImmStrokeReader.GetBuildId();
                if (!string.IsNullOrEmpty(buildId))
                {
                    UnityEngine.Debug.Log($"ImmStrokeReader: {buildId}");
                }
            }
            catch (EntryPointNotFoundException)
            {
                UnityEngine.Debug.LogWarning("ImmStrokeReader: build id symbol not found (old dylib loaded?)");
            }
            catch (DllNotFoundException)
            {
                UnityEngine.Debug.LogError("ImmStrokeReader: native plugin not found");
            }

            if (!ImmPlayer.ImmStrokeReader.StrokeReader_IsInitialized())
            {
                if (ImmPlayer.ImmStrokeReader.StrokeReader_Init(null) != 0)
                {
                    return null;
                }
            }

            int docId = ImmPlayer.ImmStrokeReader.StrokeReader_LoadFromFile(path);
            if (docId < 0)
            {
                return null;
            }

            Sequence seq = new Sequence();
            seq.RootLayer = new LayerGroup();

            int layerCount = ImmPlayer.ImmStrokeReader.StrokeReader_GetLayerCount(docId);
            for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
            {
                if (!ImmPlayer.ImmStrokeReader.StrokeReader_GetLayerInfo(docId, layerIdx, out StrokeLayerInfo info))
                {
                    continue;
                }

                // We currently expose strokes as paint layers only.
                LayerPaint layer = new LayerPaint
                {
                    Name = info.name,
                    Transform = GetLayerTransform(docId, layerIdx)
                };

                int drawingCount = ImmPlayer.ImmStrokeReader.StrokeReader_GetDrawingCount(docId, layerIdx);
                for (int drawingIdx = 0; drawingIdx < drawingCount; drawingIdx++)
                {
                    Drawing drawing = new Drawing();
                    int strokeCount = ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokeCount(docId, layerIdx, drawingIdx);
                    for (int strokeIdx = 0; strokeIdx < strokeCount; strokeIdx++)
                    {
                        if (!ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokeInfo(docId, layerIdx, drawingIdx, strokeIdx, out StrokeInfo strokeInfo))
                        {
                            continue;
                        }

                        StrokePoint[] points = new StrokePoint[strokeInfo.numPoints];
                        if (!ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokePoints(docId, layerIdx, drawingIdx, strokeIdx, points, strokeInfo.numPoints))
                        {
                            continue;
                        }

                        Stroke stroke = new Stroke
                        {
                            Id = (uint)strokeIdx,
                            BrushType = (BrushType)(short)strokeInfo.brushType,
                            DisableRotationalOpacity = true,
                            BoundingBox = new BoundingBox(strokeInfo.bboxMinX, strokeInfo.bboxMinY, strokeInfo.bboxMinZ,
                                strokeInfo.bboxMaxX, strokeInfo.bboxMaxY, strokeInfo.bboxMaxZ)
                        };

                        for (int p = 0; p < points.Length; p++)
                        {
                            StrokePoint pt = points[p];
                            stroke.Vertices.Add(new Vertex(
                                new Vector3(pt.px, pt.py, pt.pz),
                                new Vector3(pt.nx, pt.ny, pt.nz),
                                new Vector3(pt.dx, pt.dy, pt.dz),
                                new Color(pt.r, pt.g, pt.b),
                                pt.alpha,
                                pt.width));
                        }

                        drawing.Data.Strokes.Add(stroke);
                    }

                    layer.Drawings.Add(drawing);
                    layer.Frames.Add(layer.Drawings.Count - 1);
                }

                seq.RootLayer.Children.Add(layer);
            }

            ImmPlayer.ImmStrokeReader.StrokeReader_Unload(docId);
            return seq;
        }

        private static Transform GetLayerTransform(int docId, int layerIdx)
        {
            if (!s_LayerTransformApiAvailable)
            {
                return Transform.Identity;
            }

            try
            {
                if (ImmPlayer.ImmStrokeReader.StrokeReader_GetLayerTransform(docId, layerIdx, out var local, out var world))
                {
                    return ConvertTransform(world);
                }
            }
            catch (EntryPointNotFoundException)
            {
                s_LayerTransformApiAvailable = false;
            }

            return Transform.Identity;
        }

        private static Transform ConvertTransform(ImmPlayer.StrokeLayerTransform native)
        {
            float rotX = native.rotX;
            float rotY = native.rotY;
            float rotZ = native.rotZ;
            float rotW = native.rotW;
            if (rotX == 0f && rotY == 0f && rotZ == 0f && rotW == 0f)
            {
                rotW = 1f;
            }

            float scale = native.scale;
            if (scale == 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                scale = 1.0f;
            }

            return new Transform
            {
                Rotation = new Quaternion(rotX, rotY, rotZ, rotW),
                Scale = scale,
                Flip = FlipToString(native.flip),
                Translation = new Vector3(native.transX, native.transY, native.transZ)
            };
        }

        private static string FlipToString(int flip)
        {
            switch (flip)
            {
                case 1: return "X";
                case 2: return "Y";
                case 3: return "Z";
                default: return "N";
            }
        }
    }

    public class Sequence
    {
        public LayerGroup RootLayer { get; set; }
    }

    public enum LayerType : short
    {
        Group = 0,
        Paint = 1
    }

    public abstract class Layer
    {
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public bool Locked { get; set; }
        public bool Collapsed { get; set; }
        public bool BBoxVisible { get; set; }
        public float Opacity { get; set; } = 1.0f;
        public abstract LayerType Type { get; }
        public Transform Transform { get; set; } = Transform.Identity;
        public Transform Pivot { get; set; } = Transform.Identity;
    }

    public class LayerGroup : Layer
    {
        public override LayerType Type { get { return LayerType.Group; } }
        public List<Layer> Children { get; set; } = new List<Layer>();
    }

    public class LayerPaint : Layer
    {
        public override LayerType Type { get { return LayerType.Paint; } }
        public int Framerate { get; set; } = 24;
        public int MaxRepeatCount { get; set; }
        public List<Drawing> Drawings { get; set; } = new List<Drawing>();
        public List<int> Frames { get; set; } = new List<int>();
    }

    public class Drawing
    {
        public BoundingBox BoundingBox { get; set; }
        public long DataFileOffset { get; set; }
        public DrawingData Data { get; set; } = new DrawingData();
    }

    public class DrawingData
    {
        public List<Stroke> Strokes { get; set; } = new List<Stroke>();
    }

    public class Stroke
    {
        public uint Id { get; set; }
        public int u2 { get; set; }
        public BoundingBox BoundingBox { get; set; } = new BoundingBox();
        public BrushType BrushType { get; set; } = BrushType.Ribbon;
        public bool DisableRotationalOpacity { get; set; } = true;
        public byte u3 { get; set; }
        public List<Vertex> Vertices { get; set; } = new List<Vertex>();
    }

    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Color Color;
        public float Opacity;
        public float Width;

        public Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Color color, float opacity, float width)
        {
            Position = position;
            Normal = normal;
            Tangent = tangent;
            Color = color;
            Opacity = opacity;
            Width = width;
        }
    }

    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;
        public float x { get => X; set => X = value; }
        public float y { get => Y; set => Y = value; }
        public float z { get => Z; set => Z = value; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct Quaternion
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static Quaternion Identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion identity => Identity;

        public float x { get => X; set => X = value; }
        public float y { get => Y; set => Y = value; }
        public float z { get => Z; set => Z = value; }
        public float w { get => W; set => W = value; }

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public struct Transform
    {
        public Quaternion Rotation;
        public float Scale;
        public string Flip;
        public Vector3 Translation;

        public static Transform Identity => new Transform
        {
            Rotation = Quaternion.Identity,
            Scale = 1.0f,
            Flip = "N",
            Translation = new Vector3(0, 0, 0)
        };
    }

    public struct Color
    {
        public float R;
        public float G;
        public float B;

        public Color(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundingBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            Min = new Vector3(minX, minY, minZ);
            Max = new Vector3(maxX, maxY, maxZ);
        }
    }

    public enum BrushType : short
    {
        Ribbon = 1,
        Cylinder = 2,
        Ellipse = 3,
        Cube = 4
    }
}
