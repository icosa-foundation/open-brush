using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  public class SoundAttenuation
  {
    public SoundAttenuationMode Mode { get; set; }
    public float Minimum { get; set; }
    public float Maximum { get; set; }
  }
}
