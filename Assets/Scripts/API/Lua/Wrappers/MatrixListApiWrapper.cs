using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A list of transformation matrices")]
    [MoonSharpUserData]
    public class MatrixListApiWrapper : IPathApiWrapper
    {
        [MoonSharpHidden]
        public List<Matrix4x4> _Matrices;

        [MoonSharpHidden]
        public ScriptCoordSpace _Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _Matrices.Select(TrTransform.FromMatrix4x4).ToList();

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => new() { AsSingleTrList() };

        public MatrixListApiWrapper()
        {
            _Matrices = new List<Matrix4x4>();
        }

        public MatrixListApiWrapper(List<MatrixApiWrapper> matrices)
        {
            _Matrices = matrices.Select(m => m._Matrix).ToList();
        }

        public MatrixListApiWrapper(List<Matrix4x4> matrices)
        {
            _Matrices = matrices;
        }

        [LuaDocsDescription("Returns the matrix at the specified index")]
        public MatrixApiWrapper this[int index]
        {
            get => new(_Matrices[index]);
            set => _Matrices[index] = value._Matrix;
        }

        [LuaDocsDescription("The number of matrices")]
        public int count => _Matrices?.Count ?? 0;
    }
}