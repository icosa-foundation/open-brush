namespace Prowl.Unwrapper
{
    public struct Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;

        public bool IsEmpty => Width == 0 && Height == 0;

        public bool Contains(int x, int y)
        {
            return x >= Left && x < Right && y >= Top && y < Bottom;
        }

        public bool Contains(Rectangle rect)
        {
            return Contains(rect.X, rect.Y) && Contains(rect.X + rect.Width, rect.Y + rect.Height);
        }

        public bool IntersectsWith(Rectangle rect)
        {
            return rect.Left < Right && Left < rect.Right && rect.Top < Bottom && Top < rect.Bottom;
        }

        public void Offset(int offsetX, int offsetY)
        {
            X += offsetX;
            Y += offsetY;
        }

        public override string ToString()
        {
            return $"{{X={X}, Y={Y}, Width={Width}, Height={Height}}}";
        }
    }
}
