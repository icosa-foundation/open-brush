using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  // Used for the Gallery.
  public class Picture
  {
    public PictureType Type { get; set; }
    public long DataFileOffset { get; set; }
    public PictureMetadata Metadata { get; set; }
  }
}
