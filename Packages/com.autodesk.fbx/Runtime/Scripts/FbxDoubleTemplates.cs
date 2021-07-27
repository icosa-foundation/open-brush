// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
#if UNITY_EDITOR || FBXSDK_RUNTIME
namespace Autodesk.Fbx
{
    /**
     * The structs in this file are for optimized transfer. Their layout must
     * be binary-compatible with the structs in the accompanying .h file.
     *
     * That allows passing these structs on the stack between C# and C++, rather than
     * heap-allocating a class on either side, which is about 100x slower.
     */
    public struct FbxDouble2: System.IEquatable<FbxDouble2> {
        public double X;
        public double Y;

        public FbxDouble2(double X) { this.X = this.Y = X; }
        public FbxDouble2(double X, double Y) { this.X = X; this.Y = Y; }
        public FbxDouble2(FbxDouble2 other) { this.X = other.X; this.Y = other.Y; }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return X;
                    case 1: return Y;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxDouble2 other) {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj){
            if (obj is FbxDouble2) {
                return this.Equals((FbxDouble2)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxDouble2 a, FbxDouble2 b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxDouble2 a, FbxDouble2 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)X.GetHashCode();
            hash = (hash << 16) | (hash >> 16);
            hash ^= (uint)Y.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxDouble2({0},{1})", X, Y);
        }
    }

    public struct FbxDouble3: System.IEquatable<FbxDouble3> {
        public double X;
        public double Y;
        public double Z;

        public FbxDouble3(double X) { this.X = this.Y = this.Z = X; }
        public FbxDouble3(double X, double Y, double Z) { this.X = X; this.Y = Y; this.Z = Z; }
        public FbxDouble3(FbxDouble3 other) { this.X = other.X; this.Y = other.Y; this.Z = other.Z; }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    case 2: Z = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxDouble3 other) {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj){
            if (obj is FbxDouble3) {
                return this.Equals((FbxDouble3)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxDouble3 a, FbxDouble3 b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxDouble3 a, FbxDouble3 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)X.GetHashCode();
            hash = (hash << 11) | (hash >> 21);
            hash ^= (uint)Y.GetHashCode();
            hash = (hash << 11) | (hash >> 21);
            hash ^= (uint)Z.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxDouble3({0},{1},{2})", X, Y, Z);
        }
    }

    public struct FbxDouble4: System.IEquatable<FbxDouble4> {
        public double X;
        public double Y;
        public double Z;
        public double W;

        public FbxDouble4(double X) { this.X = this.Y = this.Z = this.W = X; }
        public FbxDouble4(double X, double Y, double Z, double W) { this.X = X; this.Y = Y; this.Z = Z; this.W = W; }
        public FbxDouble4(FbxDouble4 other) { this.X = other.X; this.Y = other.Y; this.Z = other.Z; this.W = other.W; }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    case 3: return W;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    case 2: Z = value; break;
                    case 3: W = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxDouble4 other) {
            return X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        }

        public override bool Equals(object obj){
            if (obj is FbxDouble4) {
                return this.Equals((FbxDouble4)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxDouble4 a, FbxDouble4 b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxDouble4 a, FbxDouble4 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)X.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)Y.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)Z.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)W.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxDouble4({0},{1},{2},{3})", X, Y, Z, W);
        }
    }

    public struct FbxColor: System.IEquatable<FbxColor> {
        public double mRed;
        public double mGreen;
        public double mBlue;
        public double mAlpha;

        public FbxColor(double red, double green, double blue, double alpha = 1) { this.mRed = red; this.mGreen = green; this.mBlue = blue; this.mAlpha = alpha; }
        public FbxColor(FbxDouble3 rgb, double alpha = 1) : this (rgb.X, rgb.Y, rgb.Z, alpha) { }
        public FbxColor(FbxDouble4 rgba) : this (rgba.X, rgba.Y, rgba.Z, rgba.W) { }

        public bool IsValid() {
            return Globals.IsValidColor(this);
        }

        public void Set(double red, double green, double blue, double alpha = 1) {
            this.mRed = red; this.mGreen = green; this.mBlue = blue; this.mAlpha = alpha;
        }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return mRed;
                    case 1: return mGreen;
                    case 2: return mBlue;
                    case 3: return mAlpha;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: mRed = value; break;
                    case 1: mGreen = value; break;
                    case 2: mBlue = value; break;
                    case 3: mAlpha = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxColor other) {
            return mRed == other.mRed && mGreen == other.mGreen && mBlue == other.mBlue && mAlpha == other.mAlpha;
        }

        public override bool Equals(object obj){
            if (obj is FbxColor) {
                return this.Equals((FbxColor)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxColor a, FbxColor b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxColor a, FbxColor b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)mRed.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)mGreen.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)mBlue.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)mAlpha.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxColor({0},{1},{2},{3})", mRed, mGreen, mBlue, mAlpha);
        }
    }

    public struct FbxVector2: System.IEquatable<FbxVector2> {
        public double X;
        public double Y;

        public FbxVector2(double X) { this.X = this.Y = X; }
        public FbxVector2(double X, double Y) { this.X = X; this.Y = Y; }
        public FbxVector2(FbxDouble2 other) { this.X = other.X; this.Y = other.Y; }
        public FbxVector2(FbxVector2 other) { this.X = other.X; this.Y = other.Y; }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return X;
                    case 1: return Y;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxVector2 other) {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj){
            if (obj is FbxVector2) {
                return this.Equals((FbxVector2)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxVector2 a, FbxVector2 b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxVector2 a, FbxVector2 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)X.GetHashCode();
            hash = (hash << 16) | (hash >> 16);
            hash ^= (uint)Y.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxVector2({0},{1})", X, Y);
        }

        // Add/sub a scalar. We add/sub each coordinate.
        public static FbxVector2 operator + (FbxVector2 a, double b) {
            return new FbxVector2(a.X + b, a.Y + b);
        }
        public static FbxVector2 operator - (FbxVector2 a, double b) {
            return new FbxVector2(a.X - b, a.Y - b);
        }

        // Scale.
        public static FbxVector2 operator * (FbxVector2 a, double b) {
            return new FbxVector2(a.X * b, a.Y * b);
        }
        public static FbxVector2 operator * (double a, FbxVector2 b) {
            return new FbxVector2(a * b.X, a * b.Y);
        }
        public static FbxVector2 operator / (FbxVector2 a, double b) {
            return new FbxVector2(a.X / b, a.Y / b);
        }

        // Negate.
        public static FbxVector2 operator - (FbxVector2 a) {
            return new FbxVector2(-a.X, -a.Y);
        }

        // Add/sub vector.
        public static FbxVector2 operator + (FbxVector2 a, FbxVector2 b) {
            return new FbxVector2(a.X + b.X, a.Y + b.Y);
        }
        public static FbxVector2 operator - (FbxVector2 a, FbxVector2 b) {
            return new FbxVector2(a.X - b.X, a.Y - b.Y);
        }

        // Memberwise multiplication -- NOT dotproduct
        public static FbxVector2 operator * (FbxVector2 a, FbxVector2 b) {
            return new FbxVector2(a.X * b.X, a.Y * b.Y);
        }
        public static FbxVector2 operator / (FbxVector2 a, FbxVector2 b) {
            return new FbxVector2(a.X / b.X, a.Y / b.Y);
        }

        public double DotProduct(FbxVector2 other) {
            return this.X * other.X + this.Y * other.Y;
        }

        public double SquareLength() {
            return X * X + Y * Y;
        }

        public double Length() {
            return System.Math.Sqrt(SquareLength());
        }

        public double Distance(FbxVector2 other) {
            return (this - other).Length();
        }
   }

    public struct FbxVector4: System.IEquatable<FbxVector4> {
        public double X;
        public double Y;
        public double Z;
        public double W;

        public FbxVector4(FbxVector4 other) { this.X = other.X; this.Y = other.Y; this.Z = other.Z; this.W = other.W; }
        public FbxVector4(double X, double Y, double Z, double W = 1) { this.X = X; this.Y = Y; this.Z = Z; this.W = W; }
        public FbxVector4(FbxDouble3 other) : this (other.X, other.Y, other.Z) { }

        public double this[int i] {
            get {
                switch(i) {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    case 3: return W;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
            set {
                switch(i) {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    case 2: Z = value; break;
                    case 3: W = value; break;
                    default: throw new System.ArgumentOutOfRangeException("i");
                }
            }
        }

        public bool Equals(FbxVector4 other) {
            return X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        }

        public override bool Equals(object obj){
            if (obj is FbxVector4) {
                return this.Equals((FbxVector4)obj);
            }
            /* types are unrelated; can't be a match */
            return false;
        }

        public static bool operator == (FbxVector4 a, FbxVector4 b) {
            return a.Equals(b);
        }

        public static bool operator != (FbxVector4 a, FbxVector4 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            uint hash = (uint)X.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)Y.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)Z.GetHashCode();
            hash = (hash << 8) | (hash >> 24);
            hash ^= (uint)W.GetHashCode();
            return (int)hash;
        }

        public override string ToString() {
            return string.Format("FbxVector4({0},{1},{2},{3})", X, Y, Z, W);
        }

        // Add/sub a scalar. We add/sub each coordinate.
        public static FbxVector4 operator + (FbxVector4 a, double b) {
            return new FbxVector4(a.X + b, a.Y + b, a.Z + b, a.W + b);
        }
        public static FbxVector4 operator - (FbxVector4 a, double b) {
            return new FbxVector4(a.X - b, a.Y - b, a.Z - b, a.W - b);
        }

        // Scale.
        public static FbxVector4 operator * (FbxVector4 a, double b) {
            return new FbxVector4(a.X * b, a.Y * b, a.Z * b, a.W * b);
        }
        public static FbxVector4 operator * (double a, FbxVector4 b) {
            // Note: this operator is not provided in C++ FBX SDK.
            // But it's how any mathematician would write it.
            return new FbxVector4(a * b.X, a * b.Y, a * b.Z, a * b.W);
        }
        public static FbxVector4 operator / (FbxVector4 a, double b) {
            return new FbxVector4(a.X / b, a.Y / b, a.Z / b, a.W / b);
        }

        // Negate.
        public static FbxVector4 operator - (FbxVector4 a) {
            return new FbxVector4(-a.X, -a.Y, -a.Z, -a.W);
        }

        // Add/sub vector.
        public static FbxVector4 operator + (FbxVector4 a, FbxVector4 b) {
            return new FbxVector4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
        }
        public static FbxVector4 operator - (FbxVector4 a, FbxVector4 b) {
            return new FbxVector4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
        }

        // Memberwise multiplication -- NOT dotproduct
        public static FbxVector4 operator * (FbxVector4 a, FbxVector4 b) {
            return new FbxVector4(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);
        }
        public static FbxVector4 operator / (FbxVector4 a, FbxVector4 b) {
            return new FbxVector4(a.X / b.X, a.Y / b.Y, a.Z / b.Z, a.W / b.W);
        }

        // Dot product of the 3d vector, ignoring W.
        public double DotProduct(FbxVector4 other) {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        // Cross product of the two 3d vectors, ignoring W.
        public FbxVector4 CrossProduct(FbxVector4 other) {
            return new FbxVector4(
                    Y * other.Z - Z * other.Y,
                    Z * other.X - X * other.Z,
                    X * other.Y - Y * other.X);
        }

        // Length of the 3d vector, ignoring W.
        public double SquareLength() {
            return X * X + Y * Y + Z * Z;
        }

        public double Length() {
            return System.Math.Sqrt(SquareLength());
        }

        public double Distance(FbxVector4 other) {
            return (this - other).Length();
        }
    }
}
#endif // UNITY_EDITOR || FBXSDK_RUNTIME