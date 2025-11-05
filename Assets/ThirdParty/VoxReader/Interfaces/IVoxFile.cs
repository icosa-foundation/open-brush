namespace VoxReader.Interfaces
{
    public interface IVoxFile
    {
        /// <summary>
        /// The version number specified in the file.
        /// </summary>
        int VersionNumber { get; }

        /// <summary>
        /// All models contained in the file.
        /// </summary>
        IModel[] Models { get; }

        /// <summary>
        /// The palette that is stored in the file.
        /// </summary>
        IPalette Palette { get; }

        /// <summary>
        /// All chunks inside the MAIN chunk.
        /// </summary>
        IChunk[] Chunks { get; }
    }
}