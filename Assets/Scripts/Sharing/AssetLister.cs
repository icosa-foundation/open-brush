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
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{

    /// Class that holds page state while calling Poly ListAssets.
    public class AssetLister
    {
        private string m_Uri;
        private string m_ErrorMessage;
        private string m_PageToken;
        private int m_pageLimit = 12;

        // How do we decide when to stop?
        public bool HasMore =>
            m_PageToken != null // We have a page token
            && Int16.TryParse(m_PageToken, out short _) // It's a valid number
            && Int16.Parse(m_PageToken) < m_pageLimit; // We haven't hit the page limit

        public AssetLister(string uri, string errorMessage)
        {
            m_Uri = uri;
            m_ErrorMessage = errorMessage;
        }

        public IEnumerator<object> NextPage(List<IcosaSceneFileInfo> files)
        {
            string uri = m_PageToken == null ? m_Uri : $"{m_Uri}pageToken={m_PageToken}&";

            WebRequest request = new WebRequest(uri, App.Instance.IcosaToken);
            using (var cr = request.SendAsync().AsIeNull())
            {
                while (!request.Done)
                {
                    try
                    {
                        cr.MoveNext();
                    }
                    catch (VrAssetServiceException e)
                    {
                        e.UserFriendly = m_ErrorMessage;
                        throw;
                    }
                    yield return cr.Current;
                }
            }

            Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
            JObject json;
            while (!f.TryGetResult(out json)) { yield return null; }

            var assets = json["assets"];
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    var info = new IcosaSceneFileInfo(asset);
                    info.Author = asset["authorName"].ToString();
                    files.Add(info);
                    App.IcosaAssetCatalog.SetJsonForAsset(asset["assetId"].ToString(), (JObject)asset);
                }
            }
            m_PageToken = json["nextPageToken"]?.ToString();
        }

        public IEnumerator<Null> NextPage(List<IcosaAssetCatalog.AssetDetails> files,
                                          string thumbnailSuffix, bool includePrivate = false)
        {
            string uri = m_PageToken == null ? m_Uri : $"{m_Uri}pageToken={m_PageToken}&";

            WebRequest request = new WebRequest(uri, App.Instance.IcosaToken);
            using (var cr = request.SendAsync().AsIeNull())
            {
                while (!request.Done)
                {
                    try
                    {
                        cr.MoveNext();
                    }
                    catch (VrAssetServiceException e)
                    {
                        e.UserFriendly = m_ErrorMessage;
                        throw;
                    }
                    yield return cr.Current;
                }
            }
            Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
            JObject json;
            while (!f.TryGetResult(out json)) { yield return null; }

            if (json.Count == 0) { yield break; }

            JToken lastAsset = null;
            var assets = json["assets"];
            foreach (JObject asset in assets)
            {
                try
                {
                    if (!includePrivate && asset["visibility"].ToString() == "PRIVATE")
                    {
                        continue;
                    }

                    // We don't want to return liked Tilt Brush sketches
                    // so in this section we filter out anything with a Tilt file in it.
                    // TODO We should record the generating app and allow filtering by that
                    bool skipObject = false;
                    foreach (var format in asset["formats"])
                    {
                        var formatType = format["formatType"].ToString();
                        if (formatType == "TILT")
                        {
                            skipObject = true;
                            break;
                        }
                    }
                    if (skipObject)
                    {
                        continue;
                    }
                    lastAsset = asset;
                    string accountName = asset["authorName"]?.ToString() ?? "Unknown";
                    files.Add(new IcosaAssetCatalog.AssetDetails(asset, accountName, thumbnailSuffix));
                    App.IcosaAssetCatalog.SetJsonForAsset(asset["assetId"].ToString(), asset);
                }
                catch (NullReferenceException)
                {
                    UnityEngine.Debug.LogErrorFormat("Failed to load asset: {0}",
                        lastAsset == null ? "NULL" : lastAsset.ToString());
                }
                yield return null;
            }
            m_PageToken = json["nextPageToken"]?.ToString();
        }
    }
} // namespace TiltBrush
