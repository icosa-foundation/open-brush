using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{

  public class TextToStrokes
  {
    private CHRFont _font;

    public TextToStrokes(CHRFont font)
    {
      _font = font;
    }

    public List<List<Vector3>> Build(string text)
    {
      var shape = new List<List<Vector3>>();
      Vector2 offset = Vector2.zero;
      foreach (var character in text)
      {
        if (character == '\n') {
          offset.y -= (_font.Height * 1.1f);
          offset.x = 0;
          continue;
        }
        if (character < _font.Outlines.Count)
        {
          List<List<Vector2>> letter = _font.Outlines[character];
          // Offset letter outline by the current total offset
          shape.AddRange(
            letter.Select(
              path => path.Select(
                point=>new Vector3(point.x + offset.x, point.y + offset.y, 0)
              ).ToList()
            ).ToList()
          );
          offset.x += _font.Widths[character];
        }
      }
      return shape;
    }
  }
}