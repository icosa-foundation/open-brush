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

using System.Collections;
using UnityEngine;

namespace TiltBrush
{

    public class IcosaModelButton : ModelButton
    {
        [SerializeField] private GameObject m_LoadingOverlay;

        private IcosaAssetCatalog.AssetDetails m_IcosaAsset;

        public IcosaAssetCatalog.AssetDetails Asset
        {
            get { return m_IcosaAsset; }
        }

        protected override Texture2D UnloadedTexture
        {
            get
            {
                if (m_IcosaAsset.Thumbnail == null)
                {
                    return base.UnloadedTexture;
                }
                else
                {
                    return m_IcosaAsset.Thumbnail;
                }
            }
        }

        protected override string ModelName
        {
            get { return m_IcosaAsset.HumanName; }
        }

        public override bool IsAvailable()
        {
            return base.IsAvailable() && m_IcosaAsset != null &&
                (!App.IcosaAssetCatalog.IsLoading(m_IcosaAsset.AssetId));
        }

        public void SetPreset(IcosaAssetCatalog.AssetDetails asset, int index)
        {
            if (asset != m_IcosaAsset)
            {
                m_LoadingOverlay.SetActive(false);
            }
            m_IcosaAsset = asset;
            SetPreset(asset.Model, index);

            if (m_IcosaAsset.ModelRotation != null)
            {
                Quaternion publishedRotation = m_IcosaAsset.ModelRotation.Value;
                m_PreviewBaseRotation = Quaternion.Euler(0, publishedRotation.eulerAngles.y, 0);
            }
        }

        override protected void RequestModelPreloadInternal(string reason)
        {
            App.IcosaAssetCatalog.RequestModelLoad(m_IcosaAsset.AssetId, reason);
            m_LoadingOverlay.SetActive(true);
        }

        override protected void CancelRequestModelPreload()
        {
            if (m_IcosaAsset == null) { return; }
            var pac = App.IcosaAssetCatalog;
            var assetId = m_IcosaAsset.AssetId;
            pac.CancelRequestModelLoad(assetId);
            m_LoadingOverlay.SetActive(pac.IsLoading(assetId));
        }

        protected override void OnButtonPressed()
        {
            // Early out if this model had errors loading.
            Debug.Log($"zzzz1 m_Model == null: {m_Model == null}");
            Debug.Log($"zzzz1 m_Model.Error == null: {m_Model.Error == null}");
            Debug.Log($"zzzz1 m_Model.Error: {m_Model.Error.Value}");
            if (m_Model != null && m_Model.Error != null)
            {
                return;
            }
            StartCoroutine(SpawnModelCoroutine("button"));
        }

        // Emits user-visible error on failure
        protected IEnumerator SpawnModelCoroutine(string reason)
        {
            if (m_Model == null)
            {
                // Same as calling Model.RequestModelPreload -> RequestModelLoadInternal, except
                // this won't ignore the request if the load-into-memory previously failed.
                App.IcosaAssetCatalog.RequestModelLoad(m_IcosaAsset.AssetId, reason);
                m_LoadingOverlay.SetActive(true);
            }
            // It is possible from this section forward that the user may have moved on to a different page
            // on the Poly panel, which is why we use a local copy of 'model' rather than m_Model.
            string assetId = m_IcosaAsset.AssetId;
            Model model;
            // A model in the catalog will become non-null once the gltf has been downloaded or is in the
            // cache.
            while ((model = App.IcosaAssetCatalog.GetModel(assetId)) == null)
            {
                yield return null;
            }
            // We only want to disable the loading overlay if the button is still referring to the same
            // asset.
            if (assetId == m_IcosaAsset.AssetId)
            {
                m_LoadingOverlay.SetActive(false);
            }

            // A model becomes valid once the gltf has been successfully read into a Unity mesh.
            if (!model.m_Valid)
            {
                // The model might be in the "loaded with error" state, but it seems harmless to try again.
                // If the user keeps clicking, we'll keep trying.
                yield return model.LoadFullyCoroutine(reason);
                Debug.Assert(model.m_Valid || model.Error != null);
            }

            if (!model.m_Valid)
            {
                // TODO: Is there a reason not to do this unconditionally?
                if (assetId == m_IcosaAsset.AssetId)
                {
                    RefreshModelButton();
                }
                OutputWindowScript.Error($"Couldn't load model: {model.Error?.message}", model.Error?.detail);
            }
            else
            {
                SpawnValidModel(model);
            }
        }

        protected override void RefreshModelButton()
        {
            base.RefreshModelButton();
            if (m_Model != null)
            {
                if (m_Model.m_Valid)
                {
                    m_LoadingOverlay.SetActive(false);
                    SetButtonTexture(m_IcosaAsset.Thumbnail, 1);
                }
                else if (m_Model.Error != null)
                {
                    m_LoadingOverlay.SetActive(false);
                }
            }
        }

        override public string UnloadedExtraDescription()
        {
            IcosaPanel polyp = m_Manager.GetComponent<IcosaPanel>();
            Debug.AssertFormat(polyp, "PolyModelButton should be a child of the PolyPanel");
            return (polyp && polyp.ShowingUser) ? null : m_IcosaAsset.AccountName;
        }

        override public string LoadedExtraDescription()
        {
            return UnloadedExtraDescription();
        }

        public override void UpdateButtonState(bool bActivateInputValid)
        {
            base.UpdateButtonState(bActivateInputValid);
            if (IsHover())
            {
                m_PreviewParent.localScale = Vector3.one;
            }
        }

        public override void ResetState()
        {
            base.ResetState();
            m_PreviewParent.localScale = Vector3.zero;
        }
    }

} // namespace TiltBrush
