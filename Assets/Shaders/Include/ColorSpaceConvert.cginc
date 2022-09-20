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

// Moved from Brush.cginc to be more helpful in other places.

float4 SrgbToLinear(float4 color) {
  // Approximation http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
  float3 sRGB = color.rgb;
  color.rgb = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
  return color;
}

float4 SrgbToLinear_Large(float4 color) {
    float4 linearColor = SrgbToLinear(color);
  color.r = color.r < 1.0 ? linearColor.r : color.r;
  color.g = color.g < 1.0 ? linearColor.g : color.g;
  color.b = color.b < 1.0 ? linearColor.b : color.b;
  return color;
}

float4 LinearToSrgb(float4 color) {
  // Approximation http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
  float3 linearColor = color.rgb;
  float3 S1 = sqrt(linearColor);
  float3 S2 = sqrt(S1);
  float3 S3 = sqrt(S2);
  color.rgb = 0.662002687 * S1 + 0.684122060 * S2 - 0.323583601 * S3 - 0.0225411470 * linearColor;
  return color;
}
