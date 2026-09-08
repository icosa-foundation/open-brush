// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// UAC1001/UAC1015 are Unity's serialization analyzer reporting fields that *Unity's*
// serializer skips - System.Guid, Dictionary<>, nullable types. The classes in this
// file are never serialized by Unity: they are JSON DTOs round-tripped by
// Newtonsoft.Json, which handles all of those types fine. So the warnings are false
// positives and the code is correct as written.
//
// Do NOT silence them by adding [NonSerialized] to the fields. Newtonsoft honours that
// attribute and would silently stop reading and writing them.
//
// A pragma is used rather than an .editorconfig entry because Unity compiles through
// Bee rather than the generated .csproj and does not pass the analyzer config through,
// so dotnet_diagnostic severity settings there have no effect. Verified: adding them
// changed nothing across two recompiles.
#pragma warning disable UAC1001

using System;

#if TILT_BRUSH
using AxisConvention = TiltBrush.AxisConvention;
#endif

namespace TiltBrushToolkit {
/// <summary>
/// Options that indicate how to import a given asset.
/// </summary>
[Serializable]
public struct GltfImportOptions {
  public enum RescalingMode {
    // Apply scaleFactor.
    CONVERT,
    // Scale the object such that it fits a box of desiredSize, ignoring scaleFactor.
    FIT,
  }

  /// <summary>
  /// If not set, axis conventions default to the glTF 2.0 standard.
  /// </summary>
  public AxisConvention? axisConventionOverride;

  /// <summary>
  /// What type of rescaling to perform.
  /// </summary>
  public RescalingMode rescalingMode;

  /// <summary>
  /// Scale factor to apply (in addition to unit conversion).
  /// Only relevant if rescalingMode==CONVERT.
  /// </summary>
  public float scaleFactor;

  /// <summary>
  /// The desired size of the bounding cube, if scaleMode==FIT.
  /// </summary>
  public float desiredSize;

  /// <summary>
  /// If true, recenters this object such that the center of its bounding box
  /// coincides with the center of the resulting GameObject (recommended).
  /// </summary>
  public bool recenter;

  /// <summary>
  /// Returns a default set of import options.
  /// </summary>
  public static GltfImportOptions Default() {
    GltfImportOptions options = new GltfImportOptions();
    options.recenter = true;
    options.rescalingMode = RescalingMode.CONVERT;
    options.scaleFactor = 1.0f;
    options.desiredSize = 1.0f;
    return options;
  }
}
}
