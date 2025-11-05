using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VoxReader.UnitTests")]

namespace VoxReader
{
    public readonly struct Color : IEquatable<Color>
    {
        public bool Equals(Color other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            return obj is Color other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = R.GetHashCode();
                hashCode = (hashCode * 397) ^ G.GetHashCode();
                hashCode = (hashCode * 397) ^ B.GetHashCode();
                hashCode = (hashCode * 397) ^ A.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// The red component of the color.
        /// </summary>
        public byte R { get; }

        /// <summary>
        /// The green component of the color.
        /// </summary>
        public byte G { get; }

        /// <summary>
        /// The blue component of the color.
        /// </summary>
        public byte B { get; }

        /// <summary>
        /// The alpha component of the color.
        /// </summary>
        public byte A { get; }

        internal Color(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override string ToString()
        {
            return $"R: {R} G: {G} B: {B} A: {A}";
        }

        internal static readonly Color Red = new(255, 0, 0, 255);
        internal static readonly Color Green = new(0, 255, 0, 255);
        internal static readonly Color Blue = new(0, 0, 255, 255);
        internal static readonly Color Yellow = new(255, 255, 0, 255);
        internal static readonly Color Magenta = new(255, 0, 255, 255);
        internal static readonly Color Cyan = new(0, 255, 255, 255);
        internal static readonly Color White = new(255, 255, 255, 255);
        internal static readonly Color Black = new(0, 0, 0, 255);

        internal static Color GetColorFromName(string name)
        {
            return name.Trim().ToLowerInvariant() switch
            {
                "red" => Red,
                "green" => Green,
                "blue" => Blue,
                "yellow" => Yellow,
                "magenta" => Magenta,
                "cyan" => Cyan,
                "white" => White,
                "black" => Black,
                _ => throw new ArgumentOutOfRangeException(nameof(name), $"'{name}' was not recognized as a valid color name!")
            };
        }
    }
}