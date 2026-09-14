// MIT License
//
// Copyright (c) 2020 fuqunaga
// https://github.com/fuqunaga/Grid3D
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using TiltBrush;
using UnityEngine;

[ExecuteInEditMode]
public class SnapGrid3D : MonoBehaviour
{
    public static class ShaderParam
    {
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int Pointer = Shader.PropertyToID("_Pointer_GS");
        public static readonly int CanvasOrigin = Shader.PropertyToID("_CanvasOrigin_GS");
        public static readonly int GridCount = Shader.PropertyToID("_GridCount");
        public static readonly int GridInterval = Shader.PropertyToID("_GridInterval");
        public static readonly int LineWidth = Shader.PropertyToID("_LineWidth");
        public static readonly int LineLength = Shader.PropertyToID("_LineLength");
        public static readonly int CanvasToWorldMatrix = Shader.PropertyToID("_CanvasToWorldMatrix");
        public static readonly int WorldToCanvasMatrix = Shader.PropertyToID("_WorldToCanvasMatrix");
        public static readonly int CanvasScale = Shader.PropertyToID("_CanvasScale");
    }

    public Material material;
    public Color color;
    public Vector3Int gridCount = Vector3Int.one * 5;
    public float gridInterval = 2f;
    public float lineLength = 0.3f;
    public float lineWidth = 0.01f;

    private Transform toolTransform;
    private Transform canvasTransform;
    private MaterialPropertyBlock materialProperties;

    private void LateUpdate()
    {
        var currentTool = SketchSurfacePanel.m_Instance.ActiveTool;
        if (currentTool is StrokeModificationTool)
        {
            toolTransform = (currentTool as StrokeModificationTool).m_ToolTransform;
        }
        else if (currentTool is FreePaintTool)
        {
            toolTransform = PointerManager.m_Instance.MainPointer.transform;
        }
        else
        {
            // Not sure what to fall back to for general cases.
            toolTransform = currentTool.transform;
        }

        canvasTransform = App.Scene.ActiveCanvas.transform;

        materialProperties ??= new MaterialPropertyBlock();
        materialProperties.SetMatrix(ShaderParam.CanvasToWorldMatrix, canvasTransform.localToWorldMatrix);
        materialProperties.SetMatrix(ShaderParam.WorldToCanvasMatrix, canvasTransform.worldToLocalMatrix);
        materialProperties.SetVector(ShaderParam.Pointer, toolTransform.position);
        materialProperties.SetVector(ShaderParam.CanvasOrigin, canvasTransform.position);
        materialProperties.SetColor(ShaderParam.Color, color);
        materialProperties.SetVector(ShaderParam.GridCount, (Vector3)gridCount);
        materialProperties.SetFloat(ShaderParam.GridInterval, gridInterval);
        materialProperties.SetFloat(ShaderParam.LineWidth, lineWidth);
        materialProperties.SetFloat(ShaderParam.LineLength, lineLength);
        materialProperties.SetFloat(ShaderParam.CanvasScale,
            canvasTransform.lossyScale.x); // Presumed always uniform

        var lineVertexCount = 6 * 2;
        var starVertexCount = lineVertexCount * 3;
        var vertexCount = gridCount.x * gridCount.y * gridCount.z * starVertexCount;

        // RenderPrimitives submits the grid through the active render pipeline. OnRenderObject and
        // DrawProceduralNow rely on Camera.current, which is not set reliably while URP renders.
        float canvasScale = Mathf.Abs(canvasTransform.lossyScale.x);
        float maxGridCount = Mathf.Max(gridCount.x, Mathf.Max(gridCount.y, gridCount.z));
        float boundsSize = gridInterval * (maxGridCount + lineLength + 2f) * canvasScale;
        var renderParams = new RenderParams(material)
        {
            layer = gameObject.layer,
            matProps = materialProperties,
            worldBounds = new Bounds(toolTransform.position, Vector3.one * boundsSize),
        };
        Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, vertexCount);
    }
}
