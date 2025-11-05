namespace VoxReader.Interfaces
{
    public interface IPalette
    {
        /// <summary>
        /// The raw colors stored in the palette.
        /// </summary>
        Color[] RawColors { get; }

        /// <summary>
        /// The mapped colors that are visible in the palette UI from MagicaVoxel.
        /// </summary>
        /// <remarks>The color index in MagicaVoxel starts at <c>1</c>, but this collection starts at <c>0</c>. You need to take this offset into account when accessing this collection.</remarks>
        Color[] Colors { get; }

        string[] Notes { get; }

        /// <summary>
        /// Returns all colors from every row in the palette where the note text matches the provided <c>string</c>.
        /// </summary>
        Color[] GetColorsByNote(string note);

        /// <summary>
        /// Returns all mapped color indices from every row in the palette where the note text matches the provided <c>string</c>.
        /// </summary>
        int[] GetColorIndicesByNote(string note);
    }
}