using System;

namespace VoxReader
{
    public struct Vector3 : IEquatable<Vector3>
    {
        /// <summary>
        /// The x-component of the vector (right).
        /// </summary>
        public readonly int X;

        /// <summary>
        /// The y-component of the vector (forward).
        /// </summary>
        public readonly int Y;

        /// <summary>
        /// The z-component of the vector (up).
        /// </summary>
        public readonly int Z;

        public Vector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"X: {X}, Y: {Y}, Z: {Z}";
        }

        public bool Equals(Vector3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X;
                hashCode = (hashCode * 397) ^ Y;
                hashCode = (hashCode * 397) ^ Z;
                return hashCode;
            }
        }

        public Vector3 ToAbsolute()
        {
            return new Vector3(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
        }

        //TODO: add unit tests

        public static bool operator ==(Vector3 a, Vector3 b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Vector3 a, Vector3 b)
        {
            return !(a == b);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector3 operator *(Vector3 a, int i)
        {
            return new Vector3(a.X * i, a.Y * i, a.Z * i);
        }

        public static Vector3 operator /(Vector3 a, int i)
        {
            return new Vector3(a.X / i, a.Y / i, a.Z / i);
        }
    }
}