using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoxReader.Interfaces;

namespace VoxReader
{
    public class Palette : IPalette
    {
        public Color[] RawColors { get; }
        public Color[] Colors { get; }
        public string[] Notes { get; }

        public Palette(Color[] rawColors, Color[] colors, string[] notes)
        {
            RawColors = rawColors;
            Colors = colors;
            Notes = notes;
        }

        private IEnumerable<int> GetNoteIndices(string note)
        {
            for (int i = 0; i < Notes.Length; i++)
            {
                if (Notes[i] == note)
                {
                    yield return i;
                }
            }
        }

        public Color[] GetColorsByNote(string note)
        {
            return GetColorIndicesByNote(note).Select(index => Colors[index]).ToArray();
        }

        public int[] GetColorIndicesByNote(string note)
        {
            int[] noteIndices = GetNoteIndices(note).ToArray();

            if (noteIndices.Length == 0)
                return Array.Empty<int>();

            var colors = new List<int>();

            foreach (int noteIndex in noteIndices)
            {
                for (int i = 0; i < 8; i++)
                {
                    int colorIndex = Math.Abs(noteIndex - 31) * 8 + i;

                    if (colorIndex >= Colors.Length)
                        continue;

                    colors.Add(colorIndex);
                }
            }

            return colors.ToArray();
        }

        public override string ToString()
        {
            var output = new StringBuilder();

            for (int i = 0; i < Colors.Length - 1; i++)
            {
                output.AppendLine(GetText(i));
            }

            output.Append(GetText(Colors.Length - 1));

            string GetText(int index) => $"{index}: [{Colors[index]}]";

            return output.ToString();
        }
    }
}