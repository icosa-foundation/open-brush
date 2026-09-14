ObjLoader [![Build status](https://ci.appveyor.com/api/projects/status/5dbqtlt7gxninwyn?svg=true)](https://ci.appveyor.com/project/ChrisJansson/objloader)[![NuGet version](https://badge.fury.io/nu/CjClutter.ObjLoader.svg)](https://badge.fury.io/nu/CjClutter.ObjLoader)
========

Objloader is a simple Wavefront .obj and .mtl loader

Installation 
------------
Build the project and reference the .dll or reference the project directly as usual.

Loading a model
---------------
Either create the loader with the standard material stream provider, this will open the file read-only from the working directory.

	var objLoaderFactory = new ObjLoaderFactory();
	var objLoader = objLoaderFactory.Create();

    
Or provide your own:

    //With the signature Func<string, Stream>
    var objLoaderFactory = new ObjLoaderFactory();
    var objLoader = objLoaderFactory.Create(materialFileName => File.Open(materialFileName);

Then it is just a matter of invoking the loader with a stream containing the model. 

    var fileStream = new FileStream("model.obj");
    var result = objLoader.Load(fileStream);

The result object contains the loaded model in this form:
	
    public class LoadResult  
    {
        public IList<Vertex> Vertices { get; set; }
        public IList<Texture> Textures { get; set; }
        public IList<Normal> Normals { get; set; }
        public IList<Group> Groups { get; set; }
        public IList<Material> Materials { get; set; }
    }
