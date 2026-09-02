// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Packs two 16-bit ints in a form that can round-trip through an RGBA8 buffer.
//
// 2026-05-17 rewrite: scalar-only version. The original used a .xxyy swizzle plus
// uint4 AND/divide. Under URP+Vulkan on Quest (Adreno/Mali) this was producing
// pixels where R==G in every output - symptom of a SPIR-V compiler issue with
// vector-component broadcasting in either the .xxyy swizzle or the subsequent
// uint4 mask. Scalar arithmetic sidesteps both. (The 2017 SV_PrimitiveID warning
// the original comment refers to is unrelated; we use neither bitfieldExtract
// nor SV_PrimitiveID in this function.)
float4 PackUint16x2ToRgba8(uint2 values) {
  uint hi_x = values.x & 0xff00u;
  uint lo_x = values.x & 0x00ffu;
  uint hi_y = values.y & 0xff00u;
  uint lo_y = values.y & 0x00ffu;
  return float4(
    float(hi_x) / float(0xff00),
    float(lo_x) / float(0xff),
    float(hi_y) / float(0xff00),
    float(lo_y) / float(0xff)
  );
}

// Undoes PackUint16x2ToRgba8
uint2 UnpackRgba8ToUint16x2(float4 rgba8) {
  // Scalar conversion to avoid uint4(float4) vector cast which has been observed
  // to broadcast the first component on some Vulkan drivers.
  uint r = (uint)floor(rgba8.x * 255.0 + 0.5);
  uint g = (uint)floor(rgba8.y * 255.0 + 0.5);
  uint b = (uint)floor(rgba8.z * 255.0 + 0.5);
  uint a = (uint)floor(rgba8.w * 255.0 + 0.5);
  uint u1 = (r << 8) | g;
  uint u2 = (b << 8) | a;
  return uint2(u1, u2);
}
