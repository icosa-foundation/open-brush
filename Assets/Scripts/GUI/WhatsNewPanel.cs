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
                    m_Items.Add(m_ItemsContainer.GetChild(i));
                }

                // Version numbers align with display order
                // Item at index 0 = version 1, index 1 = version 2, etc.
                m_HighestItemVersion = m_Items.Count;

                // Start at the first unread item (oldest unread)
                int lastViewedVersion = GetLastViewedVersion();
                if (lastViewedVersion < m_HighestItemVersion)
                {
                    // There are unread items - start at the first unread
                    // lastViewedVersion corresponds to the last viewed index + 1
                    // So first unread index = lastViewedVersion
                    m_CurrentItemIndex = lastViewedVersion;
                }
                else
                {
                    // All items have been read - start at the newest (last item)
                    m_CurrentItemIndex = m_Items.Count - 1;
                }
            }

            DisplayCurrentItem();
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

        private void DisplayCurrentItem()
        {
            // Hide all items except the current one
            for (int i = 0; i < m_Items.Count; i++)
            {
                m_Items[i].gameObject.SetActive(i == m_CurrentItemIndex);
            }
        }

        private void ShowCurrentItem()
        {
            DisplayCurrentItem();

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

        public void DismissWhatsNewPanel()
        {
            // Mark the current item as viewed when closing
            m_ViewedItemIndices.Add(m_CurrentItemIndex);
            UpdateViewedVersion();

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
            // Find highest index among viewed items this session
            // Index maps to version: index 0 = version 1, index 1 = version 2, etc.
            int highestViewedIndex = 0;
            foreach (int idx in m_ViewedItemIndices)
            {
                if (idx > highestViewedIndex)
                {
                    highestViewedIndex = idx;
                }
            }

            // Convert index to version (add 1 since index 0 = version 1)
            int highestViewedVersion = highestViewedIndex + 1;

            // Only update if we've viewed something newer
            if (highestViewedVersion > GetLastViewedVersion())
            {
                PlayerPrefs.SetInt(kPlayerPrefsKey, highestViewedVersion);
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
