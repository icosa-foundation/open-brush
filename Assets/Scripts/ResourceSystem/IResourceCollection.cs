using System.Collections.Generic;
using System.Threading.Tasks;
namespace TiltBrush
{
    public interface IResourceCollection : IResource
    {
        IAsyncEnumerable<IResource> ContentsAsync();
        string CollectionType { get; }
        string CollectionInstance { get; }
    }
}
