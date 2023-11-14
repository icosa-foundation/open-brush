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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    class IcosaService
    {
        // public const string kModelLandingPage = "https://api.icosa.gallery/edit/";
        // const string kApiHost = "https://api.icosa.gallery";
        
        public const string kModelLandingPage = "http://192.168.1.228:3000/edit/";
        const string kApiHost = "http://192.168.1.228:3000";
        
        /// A paginated response, for use with GetNextPageAsync()
        public interface Paginated
        {
            /// URI to the next page of results, or null
            string NextUri { get; }
            /// URI to the previous page of results, or null
            string PreviousUri { get; }
        }

        // Classes named "Related" seem to be shared between multiple different response types.
        // My guess is "related" is meant in the database-join sense of the word, and each of
        // these is its own table in the backend.

        // XxxRelated classes are listed here in alphabetical order.
        // Classes specific to an API call are found right after the API wrapper.
        // This violates conventions but is more convenient.

        [Serializable, UsedImplicitly]
        public class AvatarRelated
        {
            [Serializable, UsedImplicitly]
            public class inline_model
            {
                public string url;
                public int width;
                public int height;
                public int size;
            }
            public inline_model[] images;
            public string uri;
        }

        [Serializable, UsedImplicitly]
        public class TagRelated
        {
            public string slug;
            public string uri;
        }

        [Serializable, UsedImplicitly]
        public class ThumbnailsRelated
        {
            [Serializable, UsedImplicitly]
            public class inline_model_2
            {
                public string url;
                public int width;
                public int size;
                public string uid;
                public int height;
            }
            public inline_model_2[] images;
        }

        [Serializable, UsedImplicitly]
        public class UserRelated
        {
            public string username;
            public string profileUrl;
            public string account;
            public string displayName;
            public string uid;
            public AvatarRelated[] avatars;
            public string uri;
        }

        [Serializable, UsedImplicitly]
        public class Options
        {
            public Dictionary<string, string> background;

            public void SetBackgroundColor(Color color)
            {
                background = new Dictionary<string, string>();
                background["color"] = "#" + ColorUtility.ToHtmlStringRGB(color);
            }
        }

        private readonly string m_accessToken;

        public IcosaService(string token)
        {
            m_accessToken = token;
        }

        [Serializable, UsedImplicitly]
        public class MeDetail
        {
            public string uid;
            public string modelsUrl;
            public int modelCount;
            public string email;
            public string website;
            public string account;
            public string displayName;
            public string profileUrl;
            public string uri;
            public string username;
            public AvatarRelated avatar;
            public bool isLimited;
        }

        [Serializable, UsedImplicitly]
        public class ModelDetail
        {
            [Serializable, UsedImplicitly]
            public class File
            {
                public int wireframeSize;
                public int flag;
                public string version;
                public int modelSize;
                public string uri;
                public int osgjsSize;
                public JObject metadata;
            }
            public JObject status;
            public File[] files;
            public string uid;
            public TagRelated[] tags;
            public string viewerUrl;
            public string publishedAt;
            public int likeCount;
            public int commentCount;
            public int vertexCount;
            public UserRelated user;
            public bool isDownloadable;
            public string description;
            public string name;
            public JObject license; // license (object, optional),
            public string editorUrl;
            public string uri;
            public int faceCount;
            public ThumbnailsRelated thumbnails;
            public JObject options; // options (object, optional)
        }

        [Serializable, UsedImplicitly]
        public class ModelDownload
        {
            [Serializable, UsedImplicitly]
            public class inline_model_1
            {
                public string url;  // temporary URL where the archive can be downloaded
                public int expires; // when the temporary URL will expire (in seconds)
            }
            // This is called "gltf" but it's actually just the .zip archive that was uploaded, I believe
            public inline_model_1 gltf;
        }

        // Documentation for this API is incorrect, so this was created by inspection of the result
        // (and therefore may itself have errors)
        [Serializable, UsedImplicitly]
        public class xxModelLikesResponse : Paginated
        {
            [Serializable, UsedImplicitly]
            public class ModelLikesList
            {
                public string uid;
                public string viewerUrl;
                public bool isProtected;
                public int vertexCount;
                public UserRelated user;
                public bool isDownloadable;
                public string description;
                public string name;
                public string uri;
                public int faceCount;
                public ThumbnailsRelated thumbnails;
            }
            public string previous; // uri
            public string next;     // uri
            public ModelLikesList[] results;

            string Paginated.NextUri => next;
            string Paginated.PreviousUri => previous;
        }

        // TODO: /v3/search and /v3/me/search?
        // The parameters to the search functions are the same, except /v3/me/search omits
        // the "username" parameter, so maybe we can have a single function which wraps both.

        //
        /// Pass:
        ///   temporaryDirectory - if passed, caller is responsible for cleaning it up
        public async Task<CreateResponse> CreateModel(
            string name,
            string zipPath, IProgress<double> progress, CancellationToken token,
            Options options = null, string temporaryDirectory = null)
        {

            // No compression because it's a compressed .zip already
            WebRequest uploader = new WebRequest(
                $"{kApiHost}/assets", App.IcosaUserId, "POST", compress: false);

            var moreParams = new List<(string, string)>();
            
            // Not currently used or supported by the backend
            if (options != null)
            {
                moreParams.Add(("options", JsonConvert.SerializeObject(options)));
            }
            
            uploader.ProgressObject = progress;
            var reply = await uploader.SendNamedDataAsync(
                "files", File.OpenRead(zipPath), Path.GetFileName(zipPath), "application/zip",
                moreParams: moreParams, token, temporaryDirectory);
            return reply.Deserialize<CreateResponse>();
        }
        [Serializable, UsedImplicitly]
        public struct CreateResponse
        {
            public string uid;
            public string uri;
        }
    }
} // namespace TiltBrush
