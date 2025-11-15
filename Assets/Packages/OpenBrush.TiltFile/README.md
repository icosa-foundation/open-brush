# OpenBrush.TiltFile

A standalone package for reading and writing Open Brush / Tilt Brush `.tilt` files.

## Overview

This package contains the core file format implementation for `.tilt` files, extracted into a standalone package that can be used independently of Unity or Open Brush.

## Features

- **TiltFile Format**: ZIP-based file format with custom header for .tilt files
- **Binary Serialization**: Efficient binary I/O for stroke data
- **Metadata**: JSON-based metadata structures for scenes, brushes, widgets, etc.
- **Logging Abstraction**: Configurable logging interface for different environments
- **Format Options**: Support for ZIP or directory-based file formats

## Package Contents

### Core Classes

- `TiltFile`: Main class for reading and writing .tilt files
- `SketchBinaryWriter`: Efficient binary writer for sketch data
- `SketchBinaryReader`: Efficient binary reader for sketch data
- `SketchMetadata`: Metadata structures for .tilt files
- `ITiltFileLogger`: Logging abstraction interface

### Support Classes

- `WrappedStream`: Base class for stream wrappers
- `ZipSubfileReader`: ZIP reading wrapper classes
- `ZipOutputStreamWrapper`: ZIP writing wrapper classes

## Usage

### Basic File Reading

```csharp
using OpenBrush.TiltFile;

// Create a TiltFile instance
var tiltFile = new TiltFile("/path/to/sketch.tilt");

// Read metadata
using (var metadataStream = tiltFile.GetReadStream(TiltFile.FN_METADATA))
{
    // Deserialize JSON metadata
    // ...
}

// Read sketch data
using (var sketchStream = tiltFile.GetReadStream(TiltFile.FN_SKETCH))
{
    var reader = new SketchBinaryReader(sketchStream);
    // Read binary stroke data
    // ...
}
```

### Basic File Writing

```csharp
using OpenBrush.TiltFile;

// Configure format (optional)
TiltFile.PreferredFormat = TiltFormat.Zip;

// Create an atomic writer
using (var writer = new TiltFile.AtomicWriter("/path/to/sketch.tilt"))
{
    // Write metadata
    using (var metadataStream = writer.GetWriteStream(TiltFile.FN_METADATA))
    {
        // Serialize JSON metadata
        // ...
    }

    // Write sketch data
    using (var sketchStream = writer.GetWriteStream(TiltFile.FN_SKETCH))
    {
        var binaryWriter = new SketchBinaryWriter(sketchStream);
        // Write binary stroke data
        // ...
    }

    // Commit the changes
    writer.Commit();
}
```

### Logging

By default, the package uses a `NullLogger` that doesn't log anything. You can configure logging:

```csharp
// For Unity projects
TiltFile.Logger = UnityLogger.Instance;

// For custom logging
public class MyLogger : ITiltFileLogger
{
    public void Log(string message) => Console.WriteLine(message);
    public void LogWarning(string message) => Console.WriteLine($"WARNING: {message}");
    public void LogError(string message) => Console.Error.WriteLine($"ERROR: {message}");
    // ... implement other methods
}

TiltFile.Logger = new MyLogger();
```

## File Format

A `.tilt` file is a ZIP archive with a custom 16-byte header:

```
.tilt (ZIP with custom header)
├── [16-byte Tilt header]
│   ├── Sentinel: 0x546c6974 ('tilT')
│   ├── Header size: 16
│   ├── Header version: 1
│   └── Reserved fields
├── metadata.json         (JSON metadata)
├── data.sketch          (Binary stroke data)
├── thumbnail.png        (Preview image)
└── hires.png           (Optional high-res preview)
```

### Binary Sketch Format

See `SketchWriter.cs` header comments for detailed binary format specification.

## Dependencies

- **UnityEngine**: For Vector3, Quaternion, Color types (available in Unity projects)
- **Newtonsoft.Json**: For JSON serialization
- **ICSharpCode.SharpZipLib** or **Ionic.Zip**: For ZIP operations

## License

Licensed under the Apache License 2.0. See file headers for full license text.

## Credits

Original code from Tilt Brush by Google, now maintained as Open Brush by Icosa Foundation.
