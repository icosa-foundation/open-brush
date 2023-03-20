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
        Uri PreviewUri { get; }
        string Description { get; }
        Author[] Authors { get; }
        ResourceLicense License { get; }
        // Todo: tags? key value pairs?

        Task InitAsync();
        Task<Texture2D> LoadPreviewAsync();
        Task<Stream> GetStreamAsync();
    }

    public interface IHasPreviewImage
    {
        Task<Texture2D> LoadImageAsync();
    }

    public interface IWritableResource : IResource
    {

    }


}
