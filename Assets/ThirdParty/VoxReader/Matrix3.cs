using System;
using VoxReader.Exceptions;

namespace VoxReader
{
    public struct Matrix3 : IEquatable<Matrix3>
    {
        public static readonly Matrix3 Identity = new(new[,]
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        });

        private static readonly int[] _row2Lookup = { int.MaxValue, int.MaxValue, int.MaxValue, 2, int.MaxValue, 1, 0, int.MaxValue };

        private readonly int[,] m;

        public int this[int row, int column]
        {
            get => m[row, column];
            set => m[row, column] = value;
        }

        /// <summary>
        /// Creates a new 3x3 matrix from the given byte representation.
        /// </summary>
        public Matrix3(byte data)
        {
            int row0Index = data & 0b11;
            int row1Index = (data >> 2) & 0b11;
            int row2Index = _row2Lookup[(1 << row0Index) | (1 << row1Index)];
            if (row2Index == int.MaxValue)
                throw new InvalidDataException("Invalid rotation bytes!");

            int sign0 = (data >> 4) & 1;
            int sign1 = (data >> 5) & 1;
            int sign2 = (data >> 6) & 1;

            m = new int[3, 3];
            m[0, row0Index] = sign0 == 0 ? 1 : -1;
            m[1, row1Index] = sign1 == 0 ? 1 : -1;
            m[2, row2Index] = sign2 == 0 ? 1 : -1;
        }

        /// <summary>
        /// Creates a new 3x3 matrix from the given data.
        /// </summary>
        /// <remarks>Data is copied.</remarks>
        public Matrix3(int[,] data)
        {
            if (data.Length != 9)
                throw new ArgumentException($"{nameof(Matrix3)} must have 9 elements!");

            m = new int[3, 3];

            Array.Copy(data, m, data.Length);
        }

        public static Matrix3 operator *(Matrix3 a, Matrix3 b)
        {
            int[,] result = new int[3, 3];
            result[0, 0] = a[0, 0] * b[0, 0] + a[0, 1] * b[1, 0] + a[0, 2] * b[2, 0];
            result[0, 1] = a[0, 0] * b[0, 1] + a[0, 1] * b[1, 1] + a[0, 2] * b[2, 1];
            result[0, 2] = a[0, 0] * b[0, 2] + a[0, 1] * b[1, 2] + a[0, 2] * b[2, 2];

            result[1, 0] = a[1, 0] * b[0, 0] + a[1, 1] * b[1, 0] + a[1, 2] * b[2, 0];
            result[1, 1] = a[1, 0] * b[0, 1] + a[1, 1] * b[1, 1] + a[1, 2] * b[2, 1];
            result[1, 2] = a[1, 0] * b[0, 2] + a[1, 1] * b[1, 2] + a[1, 2] * b[2, 2];

            result[2, 0] = a[2, 0] * b[0, 0] + a[2, 1] * b[1, 0] + a[2, 2] * b[2, 0];
            result[2, 1] = a[2, 0] * b[0, 1] + a[2, 1] * b[1, 1] + a[2, 2] * b[2, 1];
            result[2, 2] = a[2, 0] * b[0, 2] + a[2, 1] * b[1, 2] + a[2, 2] * b[2, 2];

            return new Matrix3(result);
        }

        public static Vector3 operator *(Matrix3 a, Vector3 b)
        {
            return new Vector3(
                a[0, 0] * b.X + a[0, 1] * b.Y + a[0, 2] * b.Z,
                a[1, 0] * b.X + a[1, 1] * b.Y + a[1, 2] * b.Z,
                a[2, 0] * b.X + a[2, 1] * b.Y + a[2, 2] * b.Z);
        }

        public Vector3 RotateIndex(Vector3 b)
        {
            float offsetX = b.X + 0.5f;
            float offsetY = b.Y + 0.5f;
            float offsetZ = b.Z + 0.5f;

            float valX = this[0, 0] * offsetX + this[0, 1] * offsetY + this[0, 2] * offsetZ;
            float valY = this[1, 0] * offsetX + this[1, 1] * offsetY + this[1, 2] * offsetZ;
            float valZ = this[2, 0] * offsetX + this[2, 1] * offsetY + this[2, 2] * offsetZ;

            return new Vector3(
                (int)Math.Floor(valX),
                (int)Math.Floor(valY),
                (int)Math.Floor(valZ));
        }

        public override string ToString()
        {
            return $"[({this[0, 0]}, {this[0, 1]}, {this[0, 2]}), ({this[1, 0]}, {this[1, 1]}, {this[1, 2]}), ({this[2, 0]}, {this[2, 1]}, {this[2, 2]})]";
        }

        public bool Equals(Matrix3 other)
        {
            return m[0, 0] == other.m[0, 0] && m[0, 1] == other.m[0, 1] && m[0, 2] == other.m[0, 2] &&
                   m[1, 0] == other.m[1, 0] && m[1, 1] == other.m[1, 1] && m[1, 2] == other.m[1, 2] &&
                   m[2, 0] == other.m[2, 0] && m[2, 1] == other.m[2, 1] && m[2, 2] == other.m[2, 2];
        }

        public override bool Equals(object obj)
        {
            return obj is Matrix3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return m != null ? m.GetHashCode() : 0;
        }

        public static bool operator ==(Matrix3 left, Matrix3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Matrix3 left, Matrix3 right)
        {
            return !left.Equals(right);
        }
    }
}