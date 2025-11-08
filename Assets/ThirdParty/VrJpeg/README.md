# VrJpeg Support for Open Brush

This directory contains a Unity-compatible implementation of VR JPEG (*.vr.jpg) support for Open Brush. VR JPEGs are stereoscopic panoramic images created by the Google Cardboard Camera Android app.

## What are VR JPEGs?

VR JPEG files are standard JPEG images with embedded XMP metadata containing:
- A second eye view (right eye) encoded as base64 in the metadata
- Panorama cropping information (GPano metadata)
- Optional spatial audio data
- Depth map information

The left eye view is the visible JPEG image, while the right eye and other data are stored in extended XMP metadata segments.

## Features

This implementation provides:

- **Automatic detection** of VR JPEG files when loading background images
- **Stereo equirectangular conversion** of both left and right eye views
- **Pole filling** algorithm to complete the sphere at north and south poles
- **Flexible output** supporting both over-under and side-by-side stereo layouts
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

### VrJpegPoleFiller.cs
Fills the missing pole regions using a non-linear averaging algorithm based on work by Leo Sutic:
https://monochrome.sutic.nu/2012/11/04/filling-in-the-blanks.html

## Usage

VR JPEG files are automatically detected and loaded when you import them as background images in Open Brush. The system:

1. Detects VR JPEG format by scanning for GPano XMP metadata
2. Extracts both left and right eye images
3. Converts each to full equirectangular (2:1 aspect ratio)
4. Fills in the poles for seamless viewing
5. Combines into over-under stereo format

No special user action is required - just import your *.vr.jpg files like any other panoramic image.

## Technical Details

### File Format
Reference: https://developers.google.com/vr/concepts/cardboard-camera-vr-photo-format

VR JPEGs use standard JPEG structure with:
- APP1 marker (0xFFE1) containing XMP data
- Standard XMP for metadata properties
- Extended XMP for large binary data (right eye image, audio)

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
