// Probe-only options for the unchanged production TiltFile writer.
namespace TiltBrush
{
    public enum TiltFormat { Directory, Inherit, Zip }
    public sealed class DevOptions
    {
        public static readonly DevOptions I = new DevOptions();
        public TiltFormat PreferredTiltFormat = TiltFormat.Zip;
    }
}
