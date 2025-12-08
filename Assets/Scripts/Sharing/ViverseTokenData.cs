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
    [Serializable]
    public class ViverseTokenData
    {
        public string AccessToken;
        public string RefreshToken;
        public string TokenType;
        public int ExpiresIn;
        public long issuedAt;

        public DateTime ExpiresAt
        {
            get
            {
                DateTime issued = DateTimeOffset.FromUnixTimeSeconds(issuedAt).UtcDateTime;
                return issued.AddSeconds(ExpiresIn);
            }
        }

        public bool IsExpired(int bufferMinutes = 5)
        {
            return DateTime.UtcNow >= ExpiresAt.AddMinutes(-bufferMinutes);
        }

        public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired();

        public static ViverseTokenData FromAuthResponse(string accessToken, string refreshToken, int expiresIn)
        {
            return new ViverseTokenData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = expiresIn > 0 ? expiresIn : 3600,
                issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
