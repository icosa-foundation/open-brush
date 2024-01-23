using System;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityGLTF;

namespace TiltBrush
{
    // See:
    // https://github.com/icosa-mirror/UnityGLTF/issues/1

    [Serializable]
    public class EXT_blend_operations : IExtension
    {

        public enum BlendOperation
        {
            Alpha,
            Add,
            Multiply,
        }

        // Syntactic Sugar
        public static EXT_blend_operations Alpha => new() { blendOperation = BlendOperation.Alpha };
        public static EXT_blend_operations Add => new() { blendOperation = BlendOperation.Add };
        public static EXT_blend_operations Multiply => new() { blendOperation = BlendOperation.Multiply };

        public const string EXTENSION_NAME = nameof(EXT_blend_operations);
        private GLTFSceneExporter exporter;
        public BlendOperation blendOperation;

        public JProperty Serialize()
        {
            JProperty jProperty = new JProperty(EXTENSION_NAME, new JObject(
                new JProperty("blendOperation", new JArray(blendOperation.ToString()))
            ));
            return jProperty;
        }

        public IExtension Clone(GLTFRoot root)
        {
            return new EXT_blend_operations { blendOperation = blendOperation };
        }

        public class EXT_blend_operations_Factory : ExtensionFactory
        {
            public const string EXTENSION_NAME = nameof(EXT_blend_operations);

            public EXT_blend_operations_Factory()
            {
                ExtensionName = EXTENSION_NAME;
            }

            public override IExtension Deserialize(GLTFRoot root, JProperty extensionToken)
            {
                if (extensionToken != null)
                {
                    JToken blendOperation = extensionToken.Value[nameof(EXT_blend_operations.blendOperation)];
                    var extension = new EXT_blend_operations();
                    if (blendOperation != null)
                    {
                        var blendOpName = blendOperation.Value<string>();
                        extension.blendOperation = (BlendOperation)Enum.Parse(typeof(BlendOperation), blendOpName, true);
                    }
                    return extension;
                }
                return null;
            }
        }
    }
}

