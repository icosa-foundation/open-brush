using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using TiltBrush;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class PanelLister : MonoBehaviour
{

    [MenuItem("Open Brush/Info/Panel Lister")]
    static void ListPanels()
    {
        StringBuilder panelList = new StringBuilder();
        PanelMapKey[] m_PanelMap;
        var pm = FindObjectOfType<PanelManager>();
        FieldInfo privateField = pm.GetType().GetField("m_PanelMap", BindingFlags.NonPublic | BindingFlags.Instance);
        m_PanelMap = (PanelMapKey[])privateField.GetValue(pm);
        panelList.AppendLine($"m_Advanced\tm_Basic\tm_ModeGvr\tm_ModeMono\tm_ModeQuest\tm_ModeVr\tm_PanelPrefab\tm_ModeVrExperimental");
        foreach (var panel in m_PanelMap)
        {
            panelList.AppendLine($"{panel.m_Advanced}\t{panel.m_Basic}\t{panel.m_ModeMono}\t{panel.m_ModeQuest}\t{panel.m_ModeVr}\t{panel.m_PanelPrefab}");
        }


        Debug.Log($"{panelList}");
    }

    [MenuItem("Open Brush/Info/Popup Lister")]
    static void ListPopups()
    {
        StringBuilder popupList = new StringBuilder();
        PanelMapKey[] m_PanelMap;
        var pm = FindObjectOfType<PanelManager>();
        FieldInfo privateField = pm.GetType().GetField("m_PanelMap", BindingFlags.NonPublic | BindingFlags.Instance);
        m_PanelMap = (PanelMapKey[])privateField.GetValue(pm);
        foreach (var panel in m_PanelMap)
        {
            if (panel.m_PanelPrefab == null) continue;
            var pp = panel.m_PanelPrefab.GetComponent<BasePanel>();
            if (pp == null) continue;
            popupList.AppendLine($"{pp.name}\t");
            PopupMapKey[] m_PanelPopUpMap;
            FieldInfo privateField2 = pp.GetType().GetField("m_PanelPopUpMap", BindingFlags.NonPublic | BindingFlags.Instance);
            if (privateField2 == null) continue;
            m_PanelPopUpMap = (PopupMapKey[])privateField2.GetValue(pp);
            foreach (var popup in m_PanelPopUpMap)
            {
                popupList.AppendLine($"\t{popup.m_Command}\t{popup.m_PopUpPrefab.name}");
            }
        }

        Debug.Log($"{popupList}");
    }

}