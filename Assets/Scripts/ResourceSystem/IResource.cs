using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public interface IResource
    {
        string Name { get; }
        Uri Uri { get; }
        Texture2D PreviewImage { get; }
        string Description { get; }
        Author[] Authors { get; }
        ResourceLicense License { get; }
        // Todo: tags? key value pairs?

        Task InitAsync();
        Task<bool> LoadPreviewAsync();
        Task<Stream> GetStreamAsync();
    }
}
