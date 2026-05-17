using System.Collections.Generic;
using ObjLoader.Loader.Data;
using ObjLoader.Loader.Data.Elements;
using ObjLoader.Loader.Data.VertexData;

namespace ObjLoader.Loader.Loaders
{
    public class LoadResult  
    {
        public IList<Vertex> Vertices { get; set; }
        public IList<Texture> Textures { get; set; }
        public IList<Normal> Normals { get; set; }
        public IList<Group> Groups { get; set; }
        public IList<Material> Materials { get; set; }
    }
}