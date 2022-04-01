using System.IO;

namespace ObjLoader.Loader.Loaders
{
    public interface IMaterialLibraryLoader
    {
        void Load(Stream lineStream);
    }
}