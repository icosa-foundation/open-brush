using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class WhatsNewPanel : BasePanel
    {
        private const string kPlayerPrefsKey = "WhatsNew_LastViewedVersion";

        [SerializeField] private int m_CurrentItemIndex;
        [SerializeField] private Transform m_ItemsContainer;
        [SerializeField] private BaseButton m_NextButton;
        [SerializeField] private BaseButton m_PreviousButton;

        private List<Transform> m_Items = new ();
        private int m_HighestItemVersion = 0;
        private HashSet<int> m_ViewedItemIndices = new HashSet<int>();

        public override void InitPanel()
        {
            base.InitPanel();

            // Populate m_Items from children of the items container
            if (m_ItemsContainer != null)
            {
                m_Items.Clear();
                for (int i = 0; i < m_ItemsContainer.childCount; i++)
                {
                    Transform child = m_ItemsContainer.GetChild(i);
                    m_Items.Add(child);

                    // Track highest version number
                    var identifier = child.GetComponent<WhatsNewItemIdentifier>();
                    if (identifier != null && identifier.VersionNumber > m_HighestItemVersion)
                    {
                        m_HighestItemVersion = identifier.VersionNumber;
                    }
                }
            }

            ShowCurrentItem();
            UpdateButtonStates();
        }

        public void NextItem()
        {
            if (m_Items.Count == 0) return;
            if (m_CurrentItemIndex >= m_Items.Count - 1) return;

            m_CurrentItemIndex++;
            ShowCurrentItem();
            UpdateButtonStates();
        }

        public void PreviousItem()
        {
            if (m_Items.Count == 0) return;
            if (m_CurrentItemIndex <= 0) return;

            m_CurrentItemIndex--;
            ShowCurrentItem();
            UpdateButtonStates();
        }

        private void ShowCurrentItem()
        {
            // Hide all items except the current one
            for (int i = 0; i < m_Items.Count; i++)
            {
                m_Items[i].gameObject.SetActive(i == m_CurrentItemIndex);
            }

            // Track that this item has been viewed
            m_ViewedItemIndices.Add(m_CurrentItemIndex);
            UpdateViewedVersion();
        }

        private void UpdateButtonStates()
        {
            // Enable/disable Previous button
            if (m_PreviousButton != null)
            {
                m_PreviousButton.SetButtonAvailable(m_CurrentItemIndex > 0);
            }

            if (m_NextButton != null)
            {
                m_NextButton.SetButtonAvailable(m_CurrentItemIndex < m_Items.Count - 1);
            }
        }

        public override void AdvancePage(int iAmount)
        {
            if (m_Items.Count == 0) return;

            m_CurrentItemIndex += iAmount;

            // Clamp to valid range instead of wrapping
            m_CurrentItemIndex = Mathf.Clamp(m_CurrentItemIndex, 0, m_Items.Count - 1);

            ShowCurrentItem();
            UpdateButtonStates();
        }

        public override void GotoPage(int iIndex)
        {
            if (m_Items.Count == 0) return;

            if (iIndex >= 0 && iIndex < m_Items.Count)
            {
                m_CurrentItemIndex = iIndex;
                ShowCurrentItem();
                UpdateButtonStates();
            }
        }

        public void ClosePanel()
        {
            m_ViewedItemIndices.Clear();
            PanelManager.m_Instance.DismissNonCorePanel(PanelType.WhatsNewPanel);
        }

        public int GetHighestItemVersion()
        {
            return m_HighestItemVersion;
        }

        public static int GetLastViewedVersion()
        {
            return PlayerPrefs.GetInt(kPlayerPrefsKey, 0);
        }

        public static bool HasUnreadItems(int highestVersion)
        {
            return highestVersion > GetLastViewedVersion();
        }

        private void UpdateViewedVersion()
        {
            // Find highest version among viewed items this session
            int highestViewed = 0;
            foreach (int idx in m_ViewedItemIndices)
            {
                if (idx < m_Items.Count)
                {
                    var identifier = m_Items[idx].GetComponent<WhatsNewItemIdentifier>();
                    if (identifier != null && identifier.VersionNumber > highestViewed)
                    {
                        highestViewed = identifier.VersionNumber;
                    }
                }
            }

            // Only update if we've viewed something newer
            if (highestViewed > GetLastViewedVersion())
            {
                PlayerPrefs.SetInt(kPlayerPrefsKey, highestViewed);
                PlayerPrefs.Save();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Reset Viewed Version")]
        private void ResetViewedVersion()
        {
            PlayerPrefs.DeleteKey(kPlayerPrefsKey);
            Debug.Log("WhatsNew viewed version reset");
        }
#endif
    }
}
