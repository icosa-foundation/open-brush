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
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{

    /// Class that assists in getting an IcosaRawAsset from an Icosa Gallery instance.
    public class AssetGetter
    {
        private bool m_Ready;
        private string m_URI;
        private IcosaRawAsset m_Asset;
        private JsonSerializer m_JsonSerializer;
        private JObject m_ListerJson;

        /// Converts a property from snake case to camel case.
        public class SnakeToCamelPropertyNameContractResolver : DefaultContractResolver
        {

            protected override JsonProperty CreateProperty(MemberInfo member,
                                                           MemberSerialization memberSerialization)
            {
                JsonProperty jsonProperty = base.CreateProperty(member, memberSerialization);
                jsonProperty.PropertyName = SnakeToCamelCase(jsonProperty.PropertyName);
                return jsonProperty;
            }

            string SnakeToCamelCase(string s)
            {
                int index = s.IndexOf("_");
                while (index != -1)
                {
                    string toHere = s.Substring(0, index);
                    string upper = s.Substring(index + 1, 1);
                    string theRest = s.Substring(index + 2);

                    s = toHere + upper.ToUpper() + theRest;
                    index = s.IndexOf("_");
                }
                return s;
            }
        }

        public bool IsCanceled
        {
            get;
            set;
        }

        public bool IsReady
        {
            get { return m_Ready; }
        }

        public IcosaRawAsset Asset
        {
            get { return m_Asset; }
        }

        public string Reason { get; }

        public AssetGetter(string uri, string assetId, VrAssetFormat[] assetTypes,
                           string reason)
        {
            m_URI = uri;
            m_Asset = new IcosaRawAsset(assetId, assetTypes);
            m_JsonSerializer = new JsonSerializer();
            m_JsonSerializer.ContractResolver = new SnakeToCamelPropertyNameContractResolver();
            Reason = reason;
        }

        // Initiates the contact with Icosa
        public IEnumerator<Null> GetAssetCoroutine()
        {
            if (!m_URI.StartsWith(VrAssetService.m_Instance.IcosaApiRoot))
            {
                m_Asset.SetRootElement(UnityWebRequest.EscapeURL(m_URI), m_URI);
            }
            else
            {
                m_Ready = false;
                JObject json = App.IcosaAssetCatalog.GetJsonForAsset(m_Asset.Id);
                if (json == null)
                {
                    // Usually implies we are loading a model not via a list query
                    // i.e. from a reference in a saved sketch file
                    Debug.LogWarning($"AssetGetter: No JSON found for {m_Asset.Id}. Making additional request.");

                    WebRequest initialRequest = new WebRequest(m_URI);
                    using (var cr = initialRequest.SendAsync().AsIeNull())
                    {
                        while (!initialRequest.Done)
                        {
                            try
                            {
                                cr.MoveNext();
                            }
                            catch (VrAssetServiceException e)
                            {
                                Debug.LogException(e);
                                IsCanceled = true;
                                yield break;
                            }
                            yield return cr.Current;
                        }
                    }

                    // Deserialize request string in to an Asset class.
                    Future<JObject> f = new Future<JObject>(() => JObject.Parse(initialRequest.Result));
                    while (!f.TryGetResult(out json)) { yield return null; }
                }

                if (json.Count == 0)
                {
                    Debug.LogErrorFormat("Failed to deserialize response for {0}", m_URI);
                    yield break;
                }

                // Find the asset by looking through the format list for the specified type.
                List<string> desiredTypes = m_Asset.DesiredTypes.Select(x => x.ToString()).ToList();

                JToken GetBestFormat(IEnumerable<JToken> formats, List<string> types)
                {
                    bool found = false;
                    JToken bestFormat = null;
                    foreach (var typeByPreference in types)
                    {
                        foreach (var x in formats)
                        {
                            var formatType = x["formatType"]?.ToString();
                            if (formatType == typeByPreference)
                            {
                                bestFormat = x;
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    return bestFormat;
                }

                while (true)
                {
                    JToken format = null;
                    string formatType = null;
                    var allFormats = json["formats"].ToList();
                    VrAssetFormat selectedType = VrAssetFormat.Unknown;

                    if (allFormats != null)
                    {
                        // This assumes that desiredTypes are ordered by preference (best to worst).
                        // Try the preferred formats first, then all formats.
                        // var preferredFormats = allFormats.Where(f => f["isPreferredForDownload"].Value<bool>());
                        // format = GetBestFormat(preferredFormats, desiredTypes) ?? GetBestFormat(allFormats, desiredTypes);

                        // Temporary hack
                        bool hasBlocks = GetBestFormat(allFormats, new List<string> { "BLOCKS" })?.HasValues ?? false;
                        format = GetBestFormat(allFormats, hasBlocks ? new List<string> { "OBJ", "OBJ_NGON" } : desiredTypes);
                        formatType = format["formatType"]?.ToString();
                        selectedType = Enum.Parse<VrAssetFormat>(formatType);
                    }
                    if (format != null)
                    {
                        string internalRootFilePath = format["root"]?["relativePath"].ToString();
                        // If we successfully get a gltf2 format file, internally change the extension to
                        // "gltf2" so that the cache knows that it is a gltf2 file.
                        if (selectedType == VrAssetFormat.GLTF2)
                        {
                            internalRootFilePath = Path.ChangeExtension(internalRootFilePath, "gltf2");
                        }

                        // Get root element info.
                        m_Asset.SetRootElement(
                            internalRootFilePath,
                            format["root"]?["url"].ToString());

                        // Get all resource infos.  There may be zero.
                        foreach (var r in format["resources"])
                        {
                            string path = r["relativePath"].ToString();
                            m_Asset.AddResourceElement(path, r["url"].ToString());

                            // The root element should be the only gltf file.
                            Debug.Assert(!path.EndsWith(".gltf") && !path.EndsWith(".gltf2"),
                                string.Format("Found extra gltf resource: {0}", path));
                        }
                        break;
                    }
                    Debug.LogWarning($"Can't download {m_Asset.Id} in {formatType} format.");
                    yield break;
                }
            }

            // Download root asset and all resources in parallel.
            var rootRequest = new WebRequest(m_Asset.RootDataURL);
            var resourceRequests = new List<(IcosaRawAsset.ElementInfo element, WebRequest request)>();

            // Start root download
            var rootEnumerator = rootRequest.SendAsync().AsIeNull();

            // Start all resource downloads
            foreach (var e in m_Asset.ResourceElements)
            {
                var resourceRequest = new WebRequest(e.dataURL);
                resourceRequests.Add((e, resourceRequest));
            }

            var resourceEnumerators = resourceRequests.Select(r => r.request.SendAsync().AsIeNull()).ToList();

            // Wait for all downloads to complete
            while (!rootRequest.Done || resourceRequests.Any(r => !r.request.Done))
            {
                try
                {
                    if (!rootRequest.Done)
                    {
                        rootEnumerator.MoveNext();
                    }

                    for (int i = 0; i < resourceEnumerators.Count; i++)
                    {
                        if (!resourceRequests[i].request.Done)
                        {
                            resourceEnumerators[i].MoveNext();
                        }
                    }
                }
                catch (VrAssetServiceException e)
                {
                    Debug.LogErrorFormat("Error downloading {0}\n{1}", m_Asset.Id, e);
                    yield break;
                }
                yield return null;
            }

            // All downloads complete - copy bytes
            m_Asset.CopyBytesToRootElement(rootRequest.ResultBytes);

            foreach (var (element, request) in resourceRequests)
            {
                element.assetBytes = request.ResultBytes;
            }

            m_Ready = true;
        }
    }

} // namespace TiltBrush
