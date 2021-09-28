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

    private Transform pointer;
    private Transform canvas;
    private bool initialized;
    private void Start()
    {
        if (PointerManager.m_Instance == null) return;
        pointer = PointerManager.m_Instance.MainPointer.transform;
        canvas = App.Scene.ActiveCanvas.transform;
        initialized = true;
    }

    private void OnRenderObject()
    {
        if (!initialized) return;
        if ((Camera.current.cullingMask & (1 << gameObject.layer)) != 0)
        {
            var lineVertexCount = 6 * 2;
            var starVertexCount = lineVertexCount * 3;

            var vertexCount = gridCount.x * gridCount.y * gridCount.z * starVertexCount;

            material.SetMatrix(ShaderParam.CanvasToWorldMatrix, canvas.localToWorldMatrix);
            material.SetMatrix(ShaderParam.WorldToCanvasMatrix, canvas.worldToLocalMatrix);
            material.SetVector(ShaderParam.Pointer, pointer.position);
            material.SetVector(ShaderParam.CanvasOrigin, canvas.position);
            material.SetColor(ShaderParam.Color, color);
            material.SetVector(ShaderParam.GridCount, (Vector3)gridCount);
            material.SetFloat(ShaderParam.GridInterval, gridInterval);
            material.SetFloat(ShaderParam.LineWidth, lineWidth);
            material.SetFloat(ShaderParam.LineLength, lineLength);
            material.SetFloat(ShaderParam.CanvasScale, canvas.lossyScale.x); // Presumed always uniform
            material.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, vertexCount);
        }
    }
}
