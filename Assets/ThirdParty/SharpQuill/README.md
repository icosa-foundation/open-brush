
# SharpQuill

SharpQuill is an open source .NET library for reading and writing Oculus Quill scenes.

This project is not affiliated with Facebook or Oculus.

## Features
- .NET Standard 2.0+.
- Reads, creates, modifies and saves Oculus Quill project files.
- Exposes the scene hierarchy, transforms, drawings, paint strokes, vertices, animation (tweening & frame by frame).
- Supported layer types: Group, Paint, Viewpoint, Camera. Not supported: Sound, Picture, Model.

## Use cases
- Exchange data with other VR painting programs or traditional programs.
- Convert traditional assets into spatial drawings.
- Create procedural VR paintings and render them in Quill.
- Merge multiple scenes by cherry picking layers from various sources.


## Limitations
- The application state file (state.json) is currently not parsed and a default one is created on output.
- Attachment layers of type Sound, Picture and Model are not currently supported. 
- The scene thumbnail is not supported.
- The transform matrices from old projects (≤ Quill 1.3, circa 2017) are not supported. A work around is to open the file in a recent version of Quill and save it back.


## Examples

### Reading

Import a folder and print some top level info.

```csharp
      var sequence = QuillSequenceReader.Read(<Directory path>);
      Console.WriteLine("Background color: {0}", sequence.BackgroundColor);
      Console.WriteLine("Framerate: {0}", sequence.Framerate);
```

Drill down the layer tree and print out paint layer statistics.

```csharp
    // Visits the tree and prints the total number of strokes and vertices of each paint layer.
    private void VisitLayers(Layer layer)
    {
      if (layer is LayerGroup)
      {
        foreach (Layer child in ((LayerGroup)layer).Children)
          VisitLayers(child);
      }
      else if (layer is LayerPaint)
      {
        LayerPaint layerPaint = layer as LayerPaint;
        int countStrokes = 0;
        int countVertices = 0;
        foreach (Drawing drawing in layerPaint.Drawings)
        {
          countStrokes += drawing.Data.Strokes.Count;
          foreach (Stroke stroke in drawing.Data.Strokes)
            countVertices += stroke.Vertices.Count;
        }

        Console.WriteLine("Layer:{0}, Drawings:{1}, Strokes:{2}, Vertices:{3}", 
          layerPaint.Name, layerPaint.Drawings.Count, countStrokes, countVertices); 
      }
    }

    // Call.
    VisitLayers(sequence.RootLayer);
```

### Writing

Create a new sequence, add an existing layer to some arbitrary path in the hierarchy, export to a folder.

```csharp
    // Create the standard default scene but without any paint layer.
    var sequence = Sequence.CreateDefault();

    // Insert an existing layer somewhere in the hierarchy.
    // This creates the necessary groups along the way if they don't exist.
    // The inital "/" is interpreted as the root group of the sequence.
    sequence.InsertLayerAt(layer, "/Group/SubGroup/SubSubGroup");

    // Export the scene to a folder.
    QuillSequenceWriter.Write(sequence, <Directory path>);
```


## Document object model
TODO

## QBin binary format
http://joancharmant.com/blog/turning-real-scenes-into-vr-paintings/#oculus-quill-data-format

## Contributing
Contributions are appreciated. The easiest way to get involved is to submit a pull request with your changes against the master branch.

## License
Apache 2.0. See [LICENSE](LICENSE.md) for details.

