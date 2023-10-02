using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    public class SpatialAnchorManager : MonoBehaviour
    {
        // Start is called before the first frame update
        OVRSpatialAnchor m_Anchor;

        const string kOriginSpatialAnchorTest = "ORIGIN_SPATIAL_ANCHOR";

        void Start()
        {
            CreateOrLoadSpatialAnchor();
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
                });

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

            var pos = App.Scene.transform.InverseTransformPoint(m_Anchor.transform.position);
            var m_anchorTr = TrTransform.FromTransform(m_Anchor.transform);

            // var currentPose = App.Scene.Pose;
            // currentPose += pos;
            // currentPose.rotation
            var newPose = SketchControlsScript.MakeValidScenePose(m_anchorTr,
                SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);
            App.Scene.Pose = newPose;
            
            Debug.Log("Cached anchor localized!");
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
