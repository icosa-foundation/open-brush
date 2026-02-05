using ObjLoader.Loader.Loaders;
using ObjLoader.Loader.TypeParsers.Interfaces;

namespace ObjLoader.Loader.TypeParsers
{
    public class MaterialLibraryParser : TypeParserBase, IMaterialLibraryParser
    {
        private readonly IMaterialLibraryLoaderFacade _libraryLoaderFacade;

        public MaterialLibraryParser(IMaterialLibraryLoaderFacade libraryLoaderFacade)
        {
            _libraryLoaderFacade = libraryLoaderFacade;
        }

        protected override string Keyword
        {
            get { return "mtllib"; }
        }

        public override void Parse(string line)
        {
            _libraryLoaderFacade.Load(line);
        }
    }
}