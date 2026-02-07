using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Brush types.
  /// The "capped" versions of the brushes are made by setting the start and end vertices width to zero.
  /// </summary>
  public enum BrushType : short
  {
    Ribbon = 1, 
    Cylinder = 2, 
    Ellipse = 3,
    Cube = 4,
  }
}
