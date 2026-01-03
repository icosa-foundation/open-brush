using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The type-specific data of a picture layer.
  /// </summary>
  public class LayerPicture : Layer
  {
    public override LayerType Type { get { return LayerType.Picture; } }

    public PictureType PictureType { get; set; }
    public bool ViewerLocked { get; set; }
    public long DataFileOffset { get; set; }
    public string ImportFilePath { get; set; }

    /// <summary>
    /// Constructs a new picture layer.
    /// </summary>
    /// <param name="name">The name of the layer.</param>
    public LayerPicture(string name = "")
    {
      this.Name = name;
    }
  }
}
