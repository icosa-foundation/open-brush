using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    public class SpatialAnchorManager : MonoBehaviour
    {
        public static SpatialAnchorManager m_Instance;
        OVRSpatialAnchor m_Anchor;

        public string AnchorUuid
        {
            get
            {
                if (m_Anchor)
                {
                    return m_Anchor.Uuid.ToString();
                }
                return string.Empty;
            }
        }

        const string kOriginSpatialAnchorTest = "ORIGIN_SPATIAL_ANCHOR";

        void Awake()
        {
            if (m_Instance == null)
            {
                m_Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Start()
        {
            //CreateOrLoadSpatialAnchor();
        }

        async void CreateOrLoadSpatialAnchor()
        {
            if (PlayerPrefs.HasKey(kOriginSpatialAnchorTest))
            {
                var guidString = PlayerPrefs.GetString(kOriginSpatialAnchorTest);
                var guid = new Guid(guidString);

                var data = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
                    {
                        StorageLocation = OVRSpace.StorageLocation.Local,
                        Timeout = 0,
                        Uuids = new List<Guid>() { guid }
                    }
                );

                OnLoadUnboundAnchorComplete(data);
            }

            else
            {
                var anchorGO = new GameObject("Test Anchor");
                m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();

                SaveAnchor();
            }
        }
        
        async void SaveAnchor()
        {
            while (!m_Anchor.Created && !m_Anchor.Localized)
            {
                await Task.Yield();
            }

            //Local save, then cloud save.
            m_Anchor.Save((anchor, success) => {
                if (!success)
                {
                    return;
                }

                Debug.Log("Anchor saved to device!");
                PlayerPrefs.SetString(kOriginSpatialAnchorTest, m_Anchor.Uuid.ToString());

                m_Anchor.Save(saveOptions: new OVRSpatialAnchor.SaveOptions{ Storage = OVRSpace.StorageLocation.Cloud }, (anchor, success) => {
                    if (!success)
                    {
                        return;
                    }

                    Debug.Log("Anchor saved to cloud!");
                });
            });
        }

        public async Task<bool> ShareAnchors(List<ulong> playerIds)
        {
            var spaceUserList = new List<OVRSpaceUser>();
            foreach(var id in playerIds)
            {
                spaceUserList.Add(new OVRSpaceUser(id));
            }

            var result = await m_Anchor.ShareAsync(spaceUserList);
            Debug.Log($"Share complete!");

            return result == OVRSpatialAnchor.OperationResult.Success;
        }

        async void OnLoadUnboundAnchorComplete(OVRSpatialAnchor.UnboundAnchor[] anchors)
        {
            var unboundAnchor = anchors[0];

            var anchorGO = new GameObject("Test Anchor");
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

            var m_anchorTr = TrTransform.FromTransform(m_Anchor.transform);

            var newPose = SketchControlsScript.MakeValidScenePose(m_anchorTr,
                SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);
            App.Scene.Pose = newPose;
            
            Debug.Log("Cached anchor localized!");
        }

        public async void SyncToAnchor(string uuid)
        {
            Debug.Log("recieved sync to anchor command");
            var guid = new Guid(uuid);

            // Cloud check takes much longer, but we're testing for now
            var data = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
                {
                    StorageLocation = OVRSpace.StorageLocation.Cloud,
                    Timeout = 0,
                    Uuids = new List<Guid>() { guid }
                }
            );

            OnLoadUnboundAnchorComplete(data);
        }

        // Update is called once per frame
        void OnDestroy()
        {
            if(m_Instance == this)
            {
                m_Instance = null;
            }
        }
    }
}
