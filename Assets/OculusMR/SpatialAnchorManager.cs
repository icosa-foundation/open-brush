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
#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
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
            float operationStartedAt = Time.realtimeSinceStartup;
            Debug.Log("[Colocation] Creating OVR spatial anchor component at the scene origin.");
            var anchorGO = new GameObject("Origin Anchor");
            m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();

            bool success = await SaveAnchor();
            Debug.Log(
                $"[ColocationDiag] Spatial anchor create-and-save operation completed. Success: {success}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}.");
            return success;
        }

        async Task<bool> SaveAnchor()
        {
            Debug.Log("[Colocation] Waiting for the local spatial anchor to be created or localized.");
            while (!m_Anchor.Created && !m_Anchor.Localized)
            {
                await Task.Yield();
            }

            Debug.Log($"[Colocation] Spatial anchor creation wait completed. Created: {m_Anchor.Created}. Localized: {m_Anchor.Localized}. UUID: {m_Anchor.Uuid}.");
            //Local save, then cloud save.
            float localSaveStartedAt = Time.realtimeSinceStartup;
            var success = await m_Anchor.SaveAsync();
            Debug.Log(
                $"[ColocationDiag] Local anchor save completed. Success: {success}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - localSaveStartedAt:F3}.");

            if (!success)
            {
                Debug.LogError($"[Colocation] Failed to save spatial anchor {m_Anchor.Uuid} to local storage.");
                return false;
            }

            Debug.Log($"[Colocation] Spatial anchor {m_Anchor.Uuid} saved to local storage.");
            PlayerPrefs.SetString(kOriginSpatialAnchorPref, m_Anchor.Uuid.ToString());

            Debug.Log($"[Colocation] Saving spatial anchor {m_Anchor.Uuid} to Meta cloud storage.");
            float cloudSaveStartedAt = Time.realtimeSinceStartup;
            success = await m_Anchor.SaveAsync(saveOptions: new OVRSpatialAnchor.SaveOptions { Storage = OVRSpace.StorageLocation.Cloud });
            Debug.Log(
                $"[ColocationDiag] Cloud anchor save completed. Success: {success}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - cloudSaveStartedAt:F3}.");
            if (!success)
            {
                Debug.LogError($"[Colocation] Failed to save spatial anchor {m_Anchor.Uuid} to Meta cloud storage.");
                return false;
            }
            Debug.Log($"[Colocation] Spatial anchor {m_Anchor.Uuid} saved to Meta cloud storage.");

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
                Debug.LogError("[Colocation] Cannot localize the scene because there is no bound spatial anchor.");
                return false;
            }

            var m_anchorTr = TrTransform.FromTransform(m_Anchor.transform);

            var newPose = SketchControlsScript.MakeValidScenePose(m_anchorTr,
                SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);
            App.Scene.Pose = newPose;

            Debug.Log($"[Colocation] Scene localized to spatial anchor {m_Anchor.Uuid}. Anchor position: {m_Anchor.transform.position}.");
            return true;
        }

        public async Task<bool> SyncToRemoteAnchor(string uuid, OVRSpace.StorageLocation defaultStorageLocation = OVRSpace.StorageLocation.Local)
        {
            float operationStartedAt = Time.realtimeSinceStartup;
            Debug.Log($"[Colocation] Loading remote anchor UUID {uuid} from {defaultStorageLocation} storage.");
            if (!Guid.TryParse(uuid, out Guid guid))
            {
                Debug.LogError($"[Colocation] Invalid remote anchor UUID: {uuid}.");
                return false;
            }

            float cloudLoadStartedAt = Time.realtimeSinceStartup;
            var data = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
            {
                StorageLocation = defaultStorageLocation,
                Timeout = 0,
                Uuids = new List<Guid>() { guid }
                }
            );

            Debug.Log(
                $"[ColocationDiag] Remote anchor cloud load completed. UUID: {uuid}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - cloudLoadStartedAt:F3}.");
            Debug.Log($"[Colocation] Remote anchor load completed for UUID {uuid}. Returned anchor count: {data?.Length ?? 0}.");
            bool bindSuccess = await BindAnchors(data);

            if (bindSuccess)
            {
                Debug.Log($"[Colocation] Remote anchor UUID {uuid} bound successfully.");
                return SceneLocalizeToAnchor();
            }
            Debug.LogError($"[Colocation] Failed to bind remote anchor UUID {uuid}.");
            Debug.Log(
                $"[ColocationDiag] Remote anchor synchronization failed. UUID: {uuid}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}.");
            return false;
        }

        async Task<bool> BindAnchors(OVRSpatialAnchor.UnboundAnchor[] anchors)
        {
            if (anchors == null || anchors.Length == 0)
            {
                Debug.LogError("[Colocation] No spatial anchors were returned for binding.");
                return false;
            }

            Debug.Log($"[Colocation] Binding the first of {anchors.Length} returned spatial anchors.");
            var unboundAnchor = anchors[0];

            var anchorGO = new GameObject("Origin Anchor");
            m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();
            float bindStartedAt = Time.realtimeSinceStartup;
            unboundAnchor.BindTo(m_Anchor);

            while (m_Anchor.PendingCreation)
            {
                await Task.Yield();
            }

            Debug.Log($"[Colocation] Bound anchor creation completed. UUID: {m_Anchor.Uuid}. Localized: {m_Anchor.Localized}.");
            Debug.Log(
                $"[ColocationDiag] Bound anchor creation completed. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - bindStartedAt:F3}.");

            Debug.Log($"[Colocation] Waiting for bound anchor {m_Anchor.Uuid} to localize.");
            float localizationStartedAt = Time.realtimeSinceStartup;
            while (!m_Anchor.Localized)
            {
                await Task.Yield();
            }

            Debug.Log($"[Colocation] Bound anchor {m_Anchor.Uuid} localized.");
            Debug.Log(
                $"[ColocationDiag] Bound anchor localization completed. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - localizationStartedAt:F3}.");
            return true;
        }

        public async Task<bool> ShareAnchors(List<ulong> playerIds)
        {
            float operationStartedAt = Time.realtimeSinceStartup;
            if (m_Anchor == null)
            {
                Debug.LogError("[Colocation] Cannot share the anchor because no spatial anchor exists.");
                return false;
            }

            Debug.Log($"[Colocation] Preparing to share anchor {m_Anchor.Uuid} with {playerIds.Count} Meta users: {string.Join(",", playerIds)}.");
            var spaceUserList = new List<OVRSpaceUser>();
            foreach (var id in playerIds)
            {
                spaceUserList.Add(new OVRSpaceUser(id));
            }

            // TODO: Check anchor exists and is in cloud storage.
            var result = await m_Anchor.ShareAsync(spaceUserList);
            Debug.Log(
                $"[ColocationDiag] Meta anchor share API operation completed. Result: {result}. " +
                $"RecipientCount: {playerIds.Count}. " +
                $"DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}.");
            Debug.Log($"[Colocation] Meta anchor share completed for {m_Anchor.Uuid}. Result: {result}.");

            if (result == OVRSpatialAnchor.OperationResult.Success)
            {
                return true;
            }

            Debug.LogError($"[Colocation] Meta anchor share failed for {m_Anchor.Uuid}. Result: {result}.");
            return false;
        }
#endif // OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
    }
}
