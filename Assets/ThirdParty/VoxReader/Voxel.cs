namespace VoxReader
{
    public readonly struct Voxel
    {
        /// <summary>
        /// The position of the voxel in the model.
        /// </summary>
        public Vector3 LocalPosition { get; }

        /// <summary>
        /// The global position of the voxel in the scene.
        /// </summary>
        public Vector3 GlobalPosition { get; }

        /// <summary>
        /// The color of the voxel.
        /// </summary>
        public Color Color { get; }

        /// <summary>
        /// The mapped index of the voxel color that is visible in the palette UI from MagicaVoxel.
        /// </summary>
        public int ColorIndex { get; }

        internal Voxel(Vector3 localPosition, Vector3 globalPosition, Color color, int mappedColorIndex)
        {
            LocalPosition = localPosition;
            GlobalPosition = globalPosition;
            Color = color;
            ColorIndex = mappedColorIndex;
        }

        public override string ToString()
        {
            return $"Global Position: [{GlobalPosition}], Local Position: [{LocalPosition}], Color: [{Color}]";
        }
    }
}