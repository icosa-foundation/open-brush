using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A 4x4 matrix representing a 3D transformation")]
    [MoonSharpUserData]
    public class MatrixApiWrapper
    {
        public Matrix4x4 _Matrix;

        public MatrixApiWrapper(float m00, float m01, float m02, float m03,
                                float m10, float m11, float m12, float m13,
                                float m20, float m21, float m22, float m23,
                                float m30, float m31, float m32, float m33)
        {
            _Matrix = new Matrix4x4()
            {
                m00 = m00, m01 = m01, m02 = m02, m03 = m03,
                m10 = m10, m11 = m11, m12 = m12, m13 = m13,
                m20 = m20, m21 = m21, m22 = m22, m23 = m23,
                m30 = m30, m31 = m31, m32 = m32, m33 = m33
            };
        }

        public MatrixApiWrapper(Matrix4x4 matrix)
        {
            _Matrix = matrix;
        }

        [LuaDocsDescription("Creates a new 4x4 matrix")]
        [LuaDocsExample("newVector = Matrix:New(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 ,0, 0, 0, 0, 1)")]
        [LuaDocsParameter("x", "The x coordinate")]
        public static MatrixApiWrapper New(float m00, float m01, float m02, float m03,
                                           float m10, float m11, float m12, float m13,
                                           float m20, float m21, float m22, float m23,
                                           float m30, float m31, float m32, float m33)

        {
            var instance = new MatrixApiWrapper(m00, m01, m02, m03,
                m10, m11, m12, m13,
                m20, m21, m22, m23,
                m30, m31, m32, m33);
            return instance;
        }

        public override string ToString()
        {
            return $"Matrix({_Matrix.m00}, {_Matrix.m01}, {_Matrix.m02}, {_Matrix.m03}, " +
                $"{_Matrix.m10}, {_Matrix.m11}, {_Matrix.m12}, {_Matrix.m13}, " +
                $"{_Matrix.m20}, {_Matrix.m21}, {_Matrix.m22}, {_Matrix.m23}, " +
                $"{_Matrix.m30}, {_Matrix.m31}, {_Matrix.m32}, {_Matrix.m33})";
        }

        [LuaDocsDescription("The component at the specified index")]
        public float this[int index]
        {
            get => _Matrix[index];
            set => _Matrix[index] = value;
        }

        [LuaDocsDescription("The component at the specified row and column")]
        public float this[int row, int column]
        {
            get => _Matrix[row, column];
            set => _Matrix[row, column] = value;
        }

        [LuaDocsDescription("Returns the identity matrix")]
        public static MatrixApiWrapper identity => new(Matrix4x4.identity);

        [LuaDocsDescription("Returns a matrix with all elements set to zero")]
        public static MatrixApiWrapper zero => new(Matrix4x4.zero);

        [LuaDocsDescription("The determinant of the matrix")]
        public float determinant => _Matrix.determinant;

        [LuaDocsDescription("The inverse of this matrix")]
        public MatrixApiWrapper inverse => new(_Matrix.inverse);

        [LuaDocsDescription("Checks whether this is an identity matrix")]
        public bool isIdentity => _Matrix.isIdentity;

        [LuaDocsDescription("Attempts to get a scale value from the matrix")]
        public Vector3ApiWrapper scale
        {
            get => new(_Matrix.lossyScale);
            set => _Matrix.SetTRS(
                _Matrix.GetPosition(),
                _Matrix.rotation,
                value._Vector3
            );
        }

        [LuaDocsDescription("Attempts to get a rotation from this matrix")]
        public RotationApiWrapper rotation
        {
            get => new(_Matrix.rotation);
            set => _Matrix.SetTRS(
                _Matrix.GetPosition(),
                value._Quaternion,
                _Matrix.lossyScale
            );
        }

        [LuaDocsDescription("Returns the transpose of this matrix")]
        public MatrixApiWrapper transpose => new(_Matrix.transpose);

        [LuaDocsDescription("The position vector of this matrix")]
        public Vector3ApiWrapper position
        {
            get => new(_Matrix.GetPosition());
            set => _Matrix.SetTRS(
                value._Vector3,
                _Matrix.rotation,
                _Matrix.lossyScale
            );
        }

        [LuaDocsDescription("Checks if this matrix is a valid transform matrix")]
        public bool isValidTRS => _Matrix.ValidTRS();

        [LuaDocsDescription("Transforms a position by this matrix")]
        [LuaDocsParameter("point", "The position to transform")]
        [LuaDocsExample("myVector = myMatrix:MultiplyPoint(myVector)")]
        [LuaDocsReturnValue("The transformed position")]
        public Vector3ApiWrapper MultiplyPoint(Vector3ApiWrapper point)
        {
            return new Vector3ApiWrapper(_Matrix.MultiplyPoint(point._Vector3));
        }

        [LuaDocsDescription("Transforms a position by this matrix (Faster than MultiplyPoint but only supports TRS matrices)")]
        [LuaDocsParameter("point", "The position to transform")]
        [LuaDocsExample("myVector = myMatrix:MultiplyPoint3x4(myVector)")]
        [LuaDocsReturnValue("The transformed position")]
        public Vector3ApiWrapper MultiplyPoint3x4(Vector3ApiWrapper point)
        {
            return new Vector3ApiWrapper(_Matrix.MultiplyPoint3x4(point._Vector3));
        }

        [LuaDocsDescription("Transforms a direction by this matrix")]
        [LuaDocsParameter("vector", "The direction vector to transform")]
        [LuaDocsExample("myVector = myMatrix:MultiplyVector(myVector)")]
        [LuaDocsReturnValue("The transformed direction vector")]
        public Vector3ApiWrapper MultiplyVector(Vector3ApiWrapper vector)
        {
            return new Vector3ApiWrapper(_Matrix.MultiplyVector(vector._Vector3));
        }

        [LuaDocsDescription("Get a column from the matrix")]
        [LuaDocsParameter("index", "The index of the column to return (0 is the first column)")]
        [LuaDocsExample("myVector = myMatrix:GetColumn(0)")]
        public Vector4ApiWrapper GetColumn(int index)
        {
            return new Vector4ApiWrapper(_Matrix.GetColumn(index));
        }

        [LuaDocsDescription("Sets a column of the matrix")]
        [LuaDocsParameter("index", "The index of the column to set (0 is the first column)")]
        [LuaDocsParameter("value", "The value to set")]
        [LuaDocsExample("myMatrix:SetColumn(0, myVector)")]
        public void SetColumn(int index, Vector4ApiWrapper value)
        {
            _Matrix.SetColumn(index, value._Vector4);
        }

        [LuaDocsDescription("Returns a row from the matrix")]
        [LuaDocsParameter("index", "The index of the row to return (0 is the first row)")]
        [LuaDocsExample("myVector = myMatrix:GetRow(0)")]
        public Vector4ApiWrapper GetRow(int index)
        {
            return new Vector4ApiWrapper(_Matrix.GetRow(index));
        }

        [LuaDocsDescription("Sets a row of the matrix")]
        [LuaDocsParameter("index", "The index of the row to set (0 is the first row)")]
        [LuaDocsParameter("value", "The value to set")]
        [LuaDocsExample("myMatrix:SetRow(0, myVector)")]
        public void SetRow(int index, Vector4ApiWrapper value)
        {
            _Matrix.SetRow(index, value._Vector4);
        }

        [LuaDocsDescription("Sets this matrix to a translation, rotation and scaling matrix")]
        [LuaDocsParameter("pos", "The position")]
        [LuaDocsParameter("rot", "The rotation")]
        [LuaDocsParameter("scale", "The scale")]
        [LuaDocsExample("myMatrix:SetTRS(myPosition, myRotation, myScale)")]
        public void SetTRS(Vector3ApiWrapper pos, RotationApiWrapper rot, Vector3ApiWrapper scale)
        {
            _Matrix.SetTRS(pos._Vector3, rot._Quaternion, scale._Vector3);
        }

        // [LuaDocsDescription("Returns a plane that is transformed in space")]
        // [LuaDocsParameter("", "")]
        // [LuaDocsExample("")]
        // [LuaDocsReturnValue("")]
        // public Plane TransformPlane()
        // {
        //     return _Matrix.TransformPlane();
        // }

        // [LuaDocsDescription("This function returns a projection matrix with viewing frustum that has a near plane defined by the coordinates that were passed in")]
        // public Frustrum Frustum()
        // {
        //     return Matrix4x4.Frustum(_Matrix);
        // }

        // [LuaDocsDescription("Computes the inverse of a 3D affine matrix")]
        // public MatrixApiWrapper Inverse3DAffine()
        // {
        //     return new MatrixApiWrapper(Matrix4x4.Inverse3DAffine(_Matrix));
        // }

        [LuaDocsDescription("Creates a  \"Look At\" matrix")]
        public MatrixApiWrapper LookAt(Vector3ApiWrapper from, Vector3ApiWrapper to, Vector3ApiWrapper up)
        {
            return new MatrixApiWrapper(Matrix4x4.LookAt(from._Vector3, to._Vector3, up._Vector3));
        }

        [LuaDocsDescription("Creates a rotation matrix")]
        public static MatrixApiWrapper NewRotation(RotationApiWrapper rotation)
        {
            return new MatrixApiWrapper(Matrix4x4.Rotate(rotation._Quaternion));
        }

        [LuaDocsDescription("Creates a scaling matrix")]
        public static MatrixApiWrapper NewScaling(Vector3ApiWrapper scale)
        {
            return new MatrixApiWrapper(Matrix4x4.Scale(scale._Vector3));
        }

        [LuaDocsDescription("Creates a translation matrix")]
        public static MatrixApiWrapper NewTranslation(Vector3ApiWrapper translation)
        {
            return new MatrixApiWrapper(Matrix4x4.Translate(translation._Vector3));
        }

        [LuaDocsDescription("Creates a translation, rotation and scaling matrix")]
        public static MatrixApiWrapper NewTRS(Vector3ApiWrapper translation, RotationApiWrapper rotation, Vector3ApiWrapper scale)
        {
            return new MatrixApiWrapper(Matrix4x4.TRS(translation._Vector3, rotation._Quaternion, scale._Vector3));
        }

        // Operators

        public static MatrixApiWrapper operator *(MatrixApiWrapper a, MatrixApiWrapper b) => new MatrixApiWrapper(a._Matrix * b._Matrix);
    }
}
