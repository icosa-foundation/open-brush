using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  public class Metadata
  {
    public string Title;
    public string Description;
    public float ThumbnailCropPosition;

    public Metadata()
    {
      Title = "Untitled";
      Description = "";
      ThumbnailCropPosition = 0;
    }
  }
}
