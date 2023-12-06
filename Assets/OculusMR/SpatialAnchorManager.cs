// Copyright 2023 The Open Brush Authors
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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    public class SpatialAnchorManager : MonoBehaviour
    {
#if OCULUS_SUPPORTED
        const string kOriginSpatialAnchorPref = "ORIGIN_SPATIAL_ANCHOR";

        OVRSpatialAnchor m_Anchor;

        public string AnchorUuid
        {
            get
            {
                if (m_Anchor != null)
                {
                    return m_Anchor.Uuid.ToString();
                }
                return string.Empty;
            }
        }

        public async Task<bool> CreateSpatialAnchor()
        {
            var anchorGO = new GameObject("Origin Anchor");
            m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();

            return await SaveAnchor();
        }

        async Task<bool> SaveAnchor()
        {
            while (!m_Anchor.Created && !m_Anchor.Localized)
            {
                await Task.Yield();
            }

            //Local save, then cloud save.
            var success = await m_Anchor.SaveAsync();

            if (!success)
            {
                return false;
            }

            Debug.Log("Anchor saved to device!");
            PlayerPrefs.SetString(kOriginSpatialAnchorPref, m_Anchor.Uuid.ToString());

            success = await m_Anchor.SaveAsync(saveOptions: new OVRSpatialAnchor.SaveOptions { Storage = OVRSpace.StorageLocation.Cloud });
            if (!success)
            {
                return false;
            }
            Debug.Log("Anchor saved to cloud!");

            return true;
        }

        public async Task<bool> LoadSpatialAnchor()
        {
            if (PlayerPrefs.HasKey(kOriginSpatialAnchorPref))
            {
                var guidString = PlayerPrefs.GetString(kOriginSpatialAnchorPref);
                var guid = new Guid(guidString);

                var data = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
                {
                    StorageLocation = OVRSpace.StorageLocation.Local,
                    Timeout = 0,
                    Uuids = new List<Guid>() { guid }
                }
                );

                return await BindAnchors(data);
            }
            else
            {
                return await CreateSpatialAnchor();
            }
        }

        public bool SceneLocalizeToAnchor()
        {
            if (m_Anchor == null)
            {
                return false;
            }

            var m_anchorTr = TrTransform.FromTransform(m_Anchor.transform);

            var newPose = SketchControlsScript.MakeValidScenePose(m_anchorTr,
                SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);
            App.Scene.Pose = newPose;

            Debug.Log("Anchor localized!");
            return true;
        }

        public async Task<bool> SyncToRemoteAnchor(string uuid, OVRSpace.StorageLocation defaultStorageLocation = OVRSpace.StorageLocation.Local)
        {
            var guid = new Guid(uuid);

            var data = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
            {
                StorageLocation = defaultStorageLocation,
                Timeout = 0,
                Uuids = new List<Guid>() { guid }
            }
            );

            Debug.Log("Remote anchor recieved!");
            bool bindSuccess = await BindAnchors(data);

            if (bindSuccess)
            {
                Debug.Log("Remote anchor bound!");
                return SceneLocalizeToAnchor();
            }
            return false;
        }

        async Task<bool> BindAnchors(OVRSpatialAnchor.UnboundAnchor[] anchors)
        {
            var unboundAnchor = anchors[0];

            var anchorGO = new GameObject("Origin Anchor");
            m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();
            unboundAnchor.BindTo(m_Anchor);

            while (m_Anchor.PendingCreation)
            {
                await Task.Yield();
            }

            Debug.Log("Cached anchor created!");

            while (!m_Anchor.Localized)
            {
                await Task.Yield();
            }

            return true;
        }

        public async Task<bool> ShareAnchors(List<ulong> playerIds)
        {
            if (m_Anchor == null)
            {
                return false;
            }

            var spaceUserList = new List<OVRSpaceUser>();
            foreach (var id in playerIds)
            {
                Debug.Log($"new share id: {id}");
                spaceUserList.Add(new OVRSpaceUser(id));
            }

            // TODO: Check anchor exists and is in cloud storage.
            var result = await m_Anchor.ShareAsync(spaceUserList);

            if (result == OVRSpatialAnchor.OperationResult.Success)
            {
                Debug.Log($"Share complete!");
                return true;
            }

            return false;
        }
#endif // OCULUS_SUPPORTED
    }
}
