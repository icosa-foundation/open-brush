# VrJpeg Support for Open Brush

This directory contains a Unity-compatible implementation of VR JPEG (*.vr.jpg) support for Open Brush. VR JPEGs are stereoscopic panoramic images created by the Google Cardboard Camera Android app.

## What are VR JPEGs?

VR JPEG files are standard JPEG images with embedded XMP metadata containing:
- A second eye view (right eye) encoded as base64 in the metadata
- Panorama cropping information (GPano metadata)
- **Spatial audio data** (MP4/AAC format, typically 4-channel ambisonic audio)
- Depth map information

The left eye view is the visible JPEG image, while the right eye and other data are stored in extended XMP metadata segments.

### Spatial Audio in VR JPEGs

Google Cardboard Camera records spatial audio using the device's microphone array. This audio is:
- Encoded as MP4/AAC format
- Embedded in extended XMP metadata
- Typically 4-channel ambisonic audio for 360-degree sound
- Synchronized with the captured panorama

## Features

This implementation provides:

- **Automatic detection** of VR JPEG files when loading background images
- **Stereo equirectangular conversion** of both left and right eye views
- **Pole filling** algorithm to complete the sphere at north and south poles
- **Flexible output** supporting both over-under and side-by-side stereo layouts
- **Audio extraction** - Extracts embedded spatial audio (MP4/AAC format)
- **Audio export** - Saves audio to standalone files for use in Unity or external tools
- **Zero dependencies** beyond Unity's standard libraries and the existing FluxJpeg decoder

## Implementation

The implementation consists of three main components:

### VrJpegMetadata.cs
Parses JPEG XMP metadata to extract:
- GPano panorama properties (crop area, full pano dimensions)
- Right eye image data (embedded JPEG)
- Audio data (optional)

### VrJpegUtils.cs
Main utility class that:
- Loads VR JPEG files
- Extracts left and right eye images
- Converts cropped images to full equirectangular format
- Combines eyes into stereo layout
- Extracts embedded spatial audio (MP4/AAC format)
- Provides methods for saving audio to files

### VrJpegPoleFiller.cs
Fills the missing pole regions using a non-linear averaging algorithm based on work by Leo Sutic:
https://monochrome.sutic.nu/2012/11/04/filling-in-the-blanks.html

## Usage

### Automatic Image Loading

VR JPEG files are automatically detected and loaded when you import them as background images in Open Brush. The system:

1. Detects VR JPEG format by scanning for GPano XMP metadata
2. Extracts both left and right eye images
3. Converts each to full equirectangular (2:1 aspect ratio)
4. Fills in the poles for seamless viewing
5. Combines into over-under stereo format

No special user action is required - just import your *.vr.jpg files like any other panoramic image.

### Audio Extraction

To extract embedded spatial audio from VR JPEG files programmatically:

```csharp
// Extract audio and save to file (automatic naming)
string audioPath = VrJpegUtils.ExtractAndSaveAudio("path/to/image.vr.jpg");
if (audioPath != null)
{
    Debug.Log($"Audio saved to: {audioPath}");
}

// Or load with audio data in memory
var result = VrJpegUtils.LoadVrJpegWithAudioFromFile("path/to/image.vr.jpg");
if (result.HasAudio)
{
    Debug.Log($"Audio MIME: {result.AudioMimeType}");
    Debug.Log($"Audio size: {result.AudioData.Length} bytes");
    // Use result.AudioData as needed
}
```

Audio files are saved with the naming pattern: `originalname_audio.mp4`

For example:
- Input: `panorama.vr.jpg`
- Output: `panorama_audio.mp4` (saved in same directory)

## Technical Details

### File Format
Reference: https://developers.google.com/vr/concepts/cardboard-camera-vr-photo-format

VR JPEGs use standard JPEG structure with:
- APP1 marker (0xFFE1) containing XMP data
- Standard XMP for metadata properties
- Extended XMP for large binary data (right eye image, audio)

### Audio Format
The embedded audio in VR JPEGs:
- Stored as base64-encoded binary in extended XMP (GAudio:Data property)
- MIME type specified in GAudio:Mime property
- Typically MP4 container with AAC codec
- Often 4-channel ambisonic audio for spatial sound
- Can be played back using Unity's AudioSource or external players

### Equirectangular Conversion
The original images are typically cropped spherical panoramas. The conversion:
1. Reads crop area from GPano:CroppedArea* properties
2. Creates full equirectangular canvas (2:1 aspect)
3. Places cropped image at correct latitude/longitude
4. Fills pole regions using horizontal averaging

### Pole Filling Algorithm
- Uses sliding window average from nearest valid row
- Neighborhood size increases non-linearly toward poles
- Wraps horizontally for seamless equirectangular mapping
- Cosine falloff function for smooth transitions

## Credits

Adapted from the VrJpeg library by Joan Charmant:
https://github.com/icosa-mirror/vrjpeg

Original library licensed under Apache License 2.0.

Pole filling algorithm based on concept by Leo Sutic:
https://monochrome.sutic.nu/2012/11/04/filling-in-the-blanks.html

## License

Copyright 2025 The Open Brush Authors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
