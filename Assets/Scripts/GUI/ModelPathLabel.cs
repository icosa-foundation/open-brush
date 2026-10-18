// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    /// Read-only source information with the complete paths available on hover.
    public class ModelPathLabel : UIComponent
    {
        [SerializeField] private TextMeshPro m_Label;
        private Model m_Model;
        private string m_Subtree;

        public void SetModel(Model model, string subtree)
        {
            if (m_Model == model && m_Subtree == subtree) return;
            m_Model = model;
            m_Subtree = subtree;
            var location = model.GetLocation();
            string source = location.AbsolutePath ?? model.HumanName;
            string name = location.GetLocationType() == Model.Location.Type.IcosaAssetId
                ? model.AssetId : Path.GetFileName(source);
            string displayPath = Regex.Replace(subtree.Trim('/'), @"\[ob:\d+\]", "");
            m_Label.text = $"{name} → {displayPath.Replace("/", " → ")}";
            SetDescriptionText(source, subtree);
        }

        public override void GainFocus()
        {
            SetDescriptionActive(true);
            SetDescriptionVisualsAvailable(false);
        }

        public override void LostFocus() => SetDescriptionActive(false);
        public override void ManagerLostFocus() => ForceDescriptionDeactivate();
        public override void ResetState()
        {
            base.ResetState();
            ForceDescriptionDeactivate();
        }
        protected override void OnDisable()
        {
            ForceDescriptionDeactivate();
            base.OnDisable();
        }
    }
}
