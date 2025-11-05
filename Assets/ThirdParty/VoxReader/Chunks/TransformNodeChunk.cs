using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class TransformNodeChunk : NodeChunk, ITransformNodeChunk
    {
        public string Name { get; }
        public bool IsHidden { get; }
        public int ChildNodeId { get; }
        public int ReservedId { get; }
        public int LayerId { get; }

        public int FrameCount => Frames.Length;
        public Frame[] Frames { get; }

        public TransformNodeChunk(byte[] data) : base(data)
        {
            Attributes.TryGetValue("_name", out string name);
            Name = name;

            Attributes.TryGetValue("_hidden", out string hidden);
            IsHidden = hidden == "1";

            ChildNodeId = FormatParser.ParseInt32();
            ReservedId = FormatParser.ParseInt32();
            LayerId = FormatParser.ParseInt32();

            int frameCount = FormatParser.ParseInt32();

            Frames = new Frame[frameCount];
            
            for (int i = 0; i < frameCount; i++)
            {
                var frameDictionary = FormatParser.ParseDictionary();

                Matrix3 frameRotation = Matrix3.Identity;
                if (frameDictionary.TryGetValue("_r", out string r))
                    frameRotation = new Matrix3(byte.Parse(r));

                var frameTranslation = new Vector3(0,0,0);
                if (frameDictionary.TryGetValue("_t", out string t))
                {
                    string[] values = t.Split(' ');
                    frameTranslation = new Vector3(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
                }

                Frames[i] = new Frame
                {
                    Rotation = frameRotation,
                    Translation = frameTranslation
                };
            }
        }
    }
}