using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace TiltBrush
{
    public interface IResourceCollection : IResource
    {
        int NumResources { get; }
        IAsyncEnumerable<IResource> ContentsAsync();
        void Refresh();

        bool Delete(IResource resource);

        event Action OnChanged;
        event Action OnRefreshingChanged;

    }

    public interface IResourceCollectionFactory
    {
        string Scheme { get; }
        IResourceCollection Create(Uri uri);
    }
}
