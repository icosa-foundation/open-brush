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

using System;
using Newtonsoft.Json;

namespace TiltBrush
{
    /// <summary>
    /// Token data for VIVERSE authentication
    /// </summary>
    [Serializable]
    public class ViverseTokenData
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("issued_at")]
        public DateTime IssuedAt { get; set; }

        /// <summary>
        /// Calculate when this token expires
        /// </summary>
        public DateTime ExpiresAt => IssuedAt.AddSeconds(ExpiresIn);

        /// <summary>
        /// Check if token is expired (with safety buffer)
        /// </summary>
        /// <param name="bufferMinutes">Minutes before expiry to consider expired</param>
        public bool IsExpired(int bufferMinutes = 5)
        {
            return DateTime.UtcNow >= ExpiresAt.AddMinutes(-bufferMinutes);
        }

        /// <summary>
        /// Check if token is valid (exists and not expired)
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired();

        /// <summary>
        /// Create token data from authentication response
        /// </summary>
        public static ViverseTokenData FromAuthResponse(string accessToken, string refreshToken, int expiresIn)
        {
            return new ViverseTokenData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = expiresIn > 0 ? expiresIn : 3600, // Default 1 hour
                IssuedAt = DateTime.UtcNow
            };
        }
    }
}
