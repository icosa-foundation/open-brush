using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SpatialAnchorManager : MonoBehaviour
{
    // Start is called before the first frame update
    OVRSpatialAnchor m_Anchor;

    const string kOriginSpatialAnchorTest = "ORIGIN_SPATIAL_ANCHOR";

    void Start()
    {
        CreateOrLoadSpatialAnchor();
    }

    void CreateOrLoadSpatialAnchor()
    {
        if (PlayerPrefs.HasKey(kOriginSpatialAnchorTest))
        {
            var guidString = PlayerPrefs.GetString(kOriginSpatialAnchorTest);
            var guid = new Guid(guidString);

                
        }
        var anchorGO = new GameObject("Test Anchor");
        m_Anchor = anchorGO.AddComponent<OVRSpatialAnchor>();

        SaveAnchor();
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

            m_Anchor.Save(saveOptions: new OVRSpatialAnchor.SaveOptions{ Storage = OVRSpace.StorageLocation.Cloud }, (anchor, success) => {
                if (!success)
                {
                    return;
                }

                PlayerPrefs.SetString(kOriginSpatialAnchorTest, m_Anchor.Uuid.ToString());
                Debug.Log("Anchor saved!");
            });
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
