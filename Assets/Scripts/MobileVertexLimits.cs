// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

namespace TiltBrush
{
    /// Device-specific warning thresholds, not measured memory ceilings.
    public static class MobileVertexLimits
    {
        // Android Build property mappings follow ALVR's platform detection:
        // https://github.com/alvr-org/ALVR/blob/master/alvr/system_info/src/lib.rs
        // Quest thresholds scale the original 1M / ~2 GB practical app budget using
        // Meta's 4.4 GiB (Quest 2/Pro) and 5.75 GiB (Quest 3/3S) app limits.
        // Pico Ultra, Galaxy XR and Steam Frame estimates assume half of physical RAM
        // is available to the app: 12 GB -> 3M, 16 GB -> 4M. Older Pico devices retain 2M.
        // These beta estimates do not promise a particular frame rate or peak memory use.
        public static int GetMemoryWarningVertCount(
            string manufacturer, string model, string device, string product, int defaultLimit,
            bool isSteamFrame = false)
        {
            if (isSteamFrame)
            {
                return 4_000_000;
            }

            switch (manufacturer?.Trim().ToLowerInvariant())
            {
                case "oculus":
                    switch (device?.Trim().ToLowerInvariant())
                    {
                        case "monterey": return 1_000_000;
                        case "hollywood":
                        case "seacliff": return 2_000_000;
                        case "eureka":
                        case "panther": return 3_000_000;
                    }
                    break;
                case "pico":
                    switch (product?.Trim().ToLowerInvariant())
                    {
                        case "pico 4":
                        case "pico 4 pro":
                        case "pico 4 enterprise": return 2_000_000;
                        case "pico 4 ultra": return 3_000_000;
                    }
                    switch (model?.Trim().ToLowerInvariant())
                    {
                        case "pico neo 3":
                        case "pico neo3 link": return 2_000_000;
                    }
                    break;
                case "samsung":
                    if (string.Equals(device?.Trim(), "xrvst2",
                        System.StringComparison.OrdinalIgnoreCase))
                    {
                        return 4_000_000;
                    }
                    break;
            }
            return defaultLimit;
        }
    }
}
