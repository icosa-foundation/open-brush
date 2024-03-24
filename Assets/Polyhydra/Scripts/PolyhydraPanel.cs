// Copyright 2022 The Open Brush Authors
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TiltBrush
{

    public enum PolyhydraButtonTypes
    {
        MainCategory,
        UniformType,
        RadialType,
        GridType,
        OtherSolidsType,
        GridShape,
        OperatorType,
        FilterType,
        Preset,
        ColorMethod
    }

    public class PolyhydraPanel : BasePanel
    {
        public Color[] DefaultColorPalette;

        public GameObject PresetInitialSaveButton;
        public GameObject PresetSaveOptionsPopupButton;

        public PolyhydraOptionButton ButtonMainCategory;
        public PolyhydraOptionButton ButtonUniformType;
        public PolyhydraOptionButton ButtonRadialType;
        public PolyhydraOptionButton ButtonGridType;
        public PolyhydraOptionButton ButtonOtherSolidsType;
        public PolyhydraOptionButton ButtonGridShape;

        public AdvancedSlider Slider1;
        public AdvancedSlider Slider2;
        public AdvancedSlider Slider3;

        public Transform m_PreviewAttachPoint;
        public GameObject AllGeneratorControls;
        public GameObject AllOpControls;
        public GameObject AllAppearanceControls;
        public GameObject OpPanel;
        public PolyhydraOptionButton ButtonOpType;
        public AdvancedSlider SliderOpParam1;
        public AdvancedSlider SliderOpParam2;
        public PolyhydraColorButton ButtonOpColorPicker;
        public PolyhydraColorButton[] ColorPalletteButtons;
        public GameObject OpFilterControlParent;
        public PolyhydraOptionButton ButtonOpFilterType;
        public PolyhydraOptionButton ButtonColorMethod;
        public AdvancedSlider SliderOpFilterParam;
        public TextMeshPro LabelOpFilterName;
        public ActionToggleButton ButtonOpFilterNot;
        public ActionToggleButton ButtonOpDisable;

        public List<GameObject> MonoscopicOnlyButtons;

        public int CurrentActiveOpIndex = -1;
        public Transform OperatorSelectButtonParent;
        public Transform OperatorSelectButtonPrefab;
        public Transform OperatorSelectPopupTools;
        public PolyhydraOpPopupToolsButton ToolBtnPrev;
        public PolyhydraOpPopupToolsButton ToolBtnNext;
        [SerializeField] private string m_CurrentPresetPath;

        private MeshFilter meshFilter;

        public Transform m_PreviewPrefab;

        private PolyhydraMainCategories m_CurrentMainCategory;
        private OtherSolidsCategories m_OtherSolidsCategory;

        [NonSerialized] public string CurrentPresetsDirectory;
        [NonSerialized] public int CurrentOpCategoryIndex;
        public OpCategories CurrentOpCategory => (OpCategories)CurrentOpCategoryIndex;

        [NonSerialized]
        public static List<List<string>> ColorPalettes = new()
        {
            new() { "#264653", "#2a9d8f", "#e9c46a", "#f4a261", "#e76f51" },
            new() { "#e63946", "#f1faee", "#a8dadc", "#457b9d", "#1d3557" },
            new() { "#ffbe0b", "#fb5607", "#ff006e", "#8338ec", "#3a86ff" },
            new() { "#390099", "#9e0059", "#ff0054", "#ff5400", "#ffbd00" },
            new() { "#094074", "#3c6997", "#5adbff", "#ffdd4a", "#fe9000" },
            new() { "#ff0000", "#ff8700", "#ffd300", "#deff0a", "#a1ff0a" },
            new() { "#a1ff0a", "#0aff99", "#0aefff", "#147df5", "#580aff" },
            new() { "#ffc800", "#ffe000", "#fff700", "#b8f500", "#95e214" },
            new() { "#1e1a29", "#82c0d5", "#313344", "#127799", "#fbf8e9" },
            new() { "#127969", "#254b44", "#edaa96", "#ff4924", "#ed9700" },
            new() { "#c5856d", "#efccb4", "#fff5db", "#211f23", "#3e3c3e" },
            new() { "#c8cb44", "#6b8b1b", "#265414", "#fbe191", "#f6bb46" },
            new() { "#30302e", "#517b8b", "#2d3860", "#ffcc01", "#8a5204" },
            new() { "#fac2b0", "#e90026", "#005746", "#f2e5d9", "#30302e" },
            new() { "#16070a", "#512011", "#e65d2c", "#8a3c27", "#351e1b" },
            new() { "#eab13f", "#588938", "#454913", "#efebc7", "#c4b594" },
            new() { "#f9c5c4", "#efa9c5", "#d1a8d9", "#ae9ee1", "#939adf" },
            new() { "#292b30", "#613854", "#ab6c84", "#ffc4d1", "#ffe8e1" },
            new() { "#ffecd6", "#ffb873", "#cb765c", "#7a4a5a", "#25213e" },
            new() { "#f5ddbc", "#fabb64", "#fd724e", "#a02f40", "#5f2f45" },
            new() { "#dee3e2", "#fccbcb", "#78b3d6", "#d86969", "#4f7969" },
            new() { "#1f1f29", "#413a42", "#596070", "#96a2b3", "#eaf0d8" },
            new() { "#74569b", "#96fbc7", "#f7ffae", "#ffb3cb", "#d8bfd8" },
            new() { "#f39344", "#d95926", "#9f2d23", "#592b26", "#32151b" },
            new() { "#eefab3", "#c2d97d", "#98b253", "#4e8433", "#174f39" },
            new() { "#dee7ed", "#9cbae3", "#808fb3", "#4060ba", "#2c476d" },
            new() { "#f1f8b4", "#72eecf", "#20c5b8", "#148190", "#0a415c" },
            new() { "#ecd4bb", "#d09d8a", "#d2516d", "#882b33", "#4a1d11" },
            new() { "#bddeef", "#8dacc8", "#787d87", "#4b515d", "#1b2546" },
            new() { "#2e071d", "#213847", "#486e6b", "#b38f86", "#dbd0bf" },
            new() { "#232221", "#cb2f2c", "#1b96ba", "#f1be43", "#e2e9e9" },
        };

        public enum PolyhydraMainCategories
        {
            Platonic,
            Archimedean,
            KeplerPoinsot,
            Radial,
            Waterman,
            RegularGrids,
            CatalanGrids,
            ArchimedeanGrids,
            TwoUniformGrids,
            DurerGrids,
            Various
        }

        public static Dictionary<string, string> FriendlyOpLabels = new Dictionary<string, string>
        {
            {"AddTag", "Set Face Color"},
            {"RemoveTag", "Remove Face Color"},
            {"ClearTags", "Clear All Face Colors"},
        };

        private enum OtherSolidsCategories
        {
            Polygon,
            Star,

            UvSphere,
            UvHemisphere,
            Torus,
            Box,
            Stairs,

            C_Shape,
            L_Shape,
            H_Shape,
            Arc,
            Arch,
        }

        public string CurrentPresetPath
        {
            // Full directory and filename but with "json" extension removed

            get => m_CurrentPresetPath;
            set => m_CurrentPresetPath = value.Replace(".json", "");
        }
        public int CurrentPresetPage { get; set; }
        public int CurrentOperatorPage { get; set; }
        public int CurrentColorPalettePage { get; set; }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            InitPreviewPoly(true);
        }

        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();
            InitPreviewPoly(false);
        }

        private void InitPreviewPoly(bool attachPreviewHere)
        {
            // Instantiate the preview poly if needed
            if (PreviewPolyhedron.m_Instance == null)
            {
                Instantiate(m_PreviewPrefab);
            }

            PreviewPolyhedron.m_Instance.m_PolyRecipe = new PolyRecipe
            {
                GeneratorType = GeneratorTypes.Uniform,
                UniformPolyType = UniformTypes.Cube,
                MaterialIndex = 0,
                ColorMethod = ColorMethods.ByRole,
                Colors = (Color[])DefaultColorPalette.Clone(),
                Operators = new List<PreviewPolyhedron.OpDefinition>()
            };

            // Attach the preview poly to the Polyhydra panel if opening, or else the polyhydra tray
            Transform attachPoint = null;
            if (attachPreviewHere)
            {
                attachPoint = m_PreviewAttachPoint;
            }
            else
            {
                BasePanel experimentalPanel;
                experimentalPanel = PanelManager.m_Instance.GetActivePanelByType(PanelType.Experimental);
                if (experimentalPanel != null)
                {
                    attachPoint = experimentalPanel.GetComponentInChildren<PolyhydraModeTray>().m_PreviewPolyAttachPoint;
                }
            }
            if (attachPoint != null)
            {
                PreviewPolyhedron.m_Instance.transform.SetParent(attachPoint, false);
            }
        }

        public override void InitPanel()
        {
            base.InitPanel();
            CurrentPresetsDirectory = App.ShapeRecipesPath();

            InitPreviewPoly(false);
            ShowAllGeneratorControls();
            OpFilterControlParent.SetActive(false);
            OpPanel.SetActive(false);
            SetSliderConfiguration();
            SetMainButtonVisibility();
            EnablePresetSaveButtons(popupButtonEnabled: false);
        }

        private void EnablePresetSaveButtons(bool popupButtonEnabled)
        {
            PresetInitialSaveButton.SetActive(!popupButtonEnabled);
            PresetSaveOptionsPopupButton.SetActive(popupButtonEnabled);
        }

        public void HandleSlider1(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Float = value.z;
            RebuildPreviewAndLinked();
        }

        public void HandleSlider2(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = value.z;
            RebuildPreviewAndLinked();
        }

        public void HandleSlider3(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = value.z;
            RebuildPreviewAndLinked();
        }

        private void RebuildPreviewAndLinked()
        {
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleOpAmountSlider(Vector3 value)
        {
            int paramIndex = (int)value.y;
            float amount = value.z;
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amount = amount;
                    break;
                case 1:
                    op.amount2 = amount;
                    break;
            }
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RebuildPreviewAndLinked();
        }

        public void HandleOpDisableButton()
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            op.disabled = !op.disabled;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RefreshOpSelectButtons();
            RebuildPreviewAndLinked();
        }

        public void HandleOpRandomizeButton(int paramIndex)
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amountRandomize = !op.amountRandomize;
                    break;
                case 1:
                    op.amount2Randomize = !op.amount2Randomize;
                    break;
            }
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RebuildPreviewAndLinked();
        }

        public void ShowAllGeneratorControls()
        {
            AllGeneratorControls.SetActive(true);
            AllOpControls.SetActive(false);
            AllAppearanceControls.SetActive(false);
        }

        public void ShowAllOpControls()
        {
            AllGeneratorControls.SetActive(false);
            AllOpControls.SetActive(true);
            AllAppearanceControls.SetActive(false);
        }

        public void OnInputFieldSelected()
        {
            var overlayKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
        }

        public void ShowAllAppearanceControls()
        {
            AllGeneratorControls.SetActive(false);
            AllOpControls.SetActive(false);
            AllAppearanceControls.SetActive(true);
        }

        public void HandleSliderFilterParam(Vector3 value)
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            op.filterParamInt = Mathf.FloorToInt(value.z);
            op.filterParamFloat = value.z;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RebuildPreviewAndLinked();
        }

        public void HandleButtonOpNot()
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            op.filterNot = !op.filterNot;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RebuildPreviewAndLinked();
        }

        void AnimateOpParentIntoPlace()
        {
            float targetX = 0.7f - (CurrentActiveOpIndex * 0.25f);
            StartCoroutine(DoOpParentAnim(targetX));
        }

        IEnumerator DoOpParentAnim(float targetX)
        {
            void RescaleButtons()
            {
                // Snap parent to final position
                var pos = OperatorSelectButtonParent.localPosition;
                pos.x = targetX;
                OperatorSelectButtonParent.localPosition = pos;

                var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();
                bool overflow = false;
                for (var i = 0; i < btns.Length; i++)
                {
                    var btn = btns[i];
                    var btnParent = btn.transform.parent;
                    var x = btnParent.transform.parent.localPosition.x +
                        (btnParent.transform.localPosition.x + btnParent.transform.parent.localScale.x);
                    float scale;
                    if (x is < 0.8f or > 2.6f)
                    {
                        scale = .05f;
                        overflow = true;
                    }
                    else
                    {
                        scale = .2f;
                    }
                    btnParent.transform.localScale = Vector3.one * scale;
                }
                if (overflow)
                {
                    // TODO some visual indicator that there's overflow
                }
            }

            if (OperatorSelectButtonParent.localPosition.x < targetX)
            {
                while (OperatorSelectButtonParent.localPosition.x < targetX)
                {
                    OperatorSelectButtonParent.Translate(Vector3.right * 0.05f);
                    yield return null;
                }
            }
            else
            {
                while (OperatorSelectButtonParent.localPosition.x > targetX)
                {
                    OperatorSelectButtonParent.Translate(Vector3.left * 0.05f);
                    yield return null;
                }
            }

            RescaleButtons();
        }

        void Update()
        {
            BaseUpdate();
            m_PreviewAttachPoint.Rotate(0, 0.25f, 0);
            if (PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count > 0 && CurrentActiveOpIndex > PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1)
            {
                Debug.LogError($"Mismatch between {CurrentActiveOpIndex} and Operators.Count: {PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count}");
                CurrentActiveOpIndex = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1;
            }
        }

        public void SetMainButtonVisibility()
        {
            var recipe = PreviewPolyhedron.m_Instance.m_PolyRecipe;
            SetButtonTextAndIcon(PolyhydraButtonTypes.MainCategory, m_CurrentMainCategory.ToString());

            foreach (var go in MonoscopicOnlyButtons)
            {
                go.SetActive(App.Config.m_SdkMode == SdkMode.Monoscopic);
            }

            switch (m_CurrentMainCategory)
            {
                // All the shapeCategories that use the Uniform popup
                case PolyhydraMainCategories.Archimedean:
                case PolyhydraMainCategories.Platonic:
                case PolyhydraMainCategories.KeplerPoinsot:
                    ButtonUniformType.gameObject.SetActive(true);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, recipe.UniformPolyType.ToString());
                    break;

                case PolyhydraMainCategories.RegularGrids:
                case PolyhydraMainCategories.CatalanGrids:
                case PolyhydraMainCategories.ArchimedeanGrids:
                case PolyhydraMainCategories.TwoUniformGrids:
                case PolyhydraMainCategories.DurerGrids:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(true);
                    ButtonGridShape.gameObject.SetActive(true);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;

                case PolyhydraMainCategories.Various:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(true);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, recipe.VariousSolidsType.ToString());
                    break;

                case PolyhydraMainCategories.Radial:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(true);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, recipe.RadialPolyType.ToString());
                    break;

                case PolyhydraMainCategories.Waterman:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    break;
            }
        }

        public void SetSliderConfiguration()
        {
            switch (m_CurrentMainCategory)
            {
                case PolyhydraMainCategories.Platonic:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                case PolyhydraMainCategories.Archimedean:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                case PolyhydraMainCategories.KeplerPoinsot:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                case PolyhydraMainCategories.Radial:

                    Slider1.gameObject.SetActive(true);
                    Slider1.SetMin(3, 3);
                    Slider1.SetMax(16, 64);
                    Slider1.SetDescriptionText("Sides");
                    Slider1.SliderType = SliderTypes.Int;

                    switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            Slider2.gameObject.SetActive(true);
                            Slider2.SetDescriptionText("Height");
                            Slider2.SetMin(.1f, .1f);
                            Slider2.SetMax(4, 16);
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.gameObject.SetActive(false);
                            break;
                        case RadialSolids.RadialPolyType.ElongatedPyramid:
                        case RadialSolids.RadialPolyType.GyroelongatedPyramid:
                        case RadialSolids.RadialPolyType.ElongatedDipyramid:
                        case RadialSolids.RadialPolyType.GyroelongatedDipyramid:
                        case RadialSolids.RadialPolyType.ElongatedCupola:
                        case RadialSolids.RadialPolyType.GyroelongatedCupola:
                        case RadialSolids.RadialPolyType.ElongatedOrthoBicupola:
                        case RadialSolids.RadialPolyType.ElongatedGyroBicupola:
                        case RadialSolids.RadialPolyType.GyroelongatedBicupola:
                            Slider2.gameObject.SetActive(true);
                            Slider2.SetDescriptionText("Height");
                            Slider2.SetMin(.1f, .1f);
                            Slider2.SetMax(4, 16);
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.gameObject.SetActive(true);
                            Slider3.SetDescriptionText("Cap Height");
                            Slider3.SetMin(.1f, .1f);
                            Slider3.SetMax(4, 16);
                            Slider3.SliderType = SliderTypes.Float;
                            break;
                    }
                    break;

                case PolyhydraMainCategories.Waterman:

                    Slider1.gameObject.SetActive(true);
                    Slider2.gameObject.SetActive(true);
                    Slider3.gameObject.SetActive(false);
                    Slider1.SetDescriptionText("Root");
                    Slider2.SetDescriptionText("C");
                    Slider1.SetMin(1, 1);
                    Slider1.SetMax(80, 80);
                    Slider2.SetMin(0, 0);
                    Slider2.SetMax(6, 6);
                    Slider1.SliderType = SliderTypes.Int;
                    Slider2.SliderType = SliderTypes.Int;
                    break;

                case PolyhydraMainCategories.RegularGrids:
                case PolyhydraMainCategories.CatalanGrids:
                case PolyhydraMainCategories.ArchimedeanGrids:
                case PolyhydraMainCategories.TwoUniformGrids:
                case PolyhydraMainCategories.DurerGrids:

                    Slider1.gameObject.SetActive(true);
                    Slider2.gameObject.SetActive(true);
                    Slider3.gameObject.SetActive(false);
                    Slider1.SetDescriptionText("Width");
                    Slider2.SetDescriptionText("Depth");
                    Slider1.SetMin(1, 1);
                    Slider1.SetMax(16, 48);
                    Slider2.SetMin(1, 1);
                    Slider2.SetMax(16, 48);
                    Slider1.SliderType = SliderTypes.Int;
                    Slider2.SliderType = SliderTypes.Int;
                    break;

                case PolyhydraMainCategories.Various:

                    switch (m_OtherSolidsCategory)
                    {
                        case OtherSolidsCategories.Polygon:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(false);
                            Slider3.gameObject.SetActive(false);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider1.SetMin(3, 3);
                            Slider1.SetMax(16, 48);
                            Slider1.SetDescriptionText("Sides");
                            break;
                        case OtherSolidsCategories.Star:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(false);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider1.SetMin(3, 3);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(0.01f, 0.01f);
                            Slider2.SetMax(4, 12);
                            Slider1.SetDescriptionText("Sides");
                            Slider2.SetDescriptionText("Amount");
                            break;
                        case OtherSolidsCategories.Box:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Int;
                            Slider3.SliderType = SliderTypes.Int;
                            Slider1.SetMin(1, 1);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(1, 1);
                            Slider2.SetMax(16, 48);
                            Slider3.SetMin(1, 1);
                            Slider3.SetMax(16, 48);
                            Slider1.SetDescriptionText("X Resolution");
                            Slider2.SetDescriptionText("Y Resolution");
                            Slider3.SetDescriptionText("Z Resolution");
                            break;
                        case OtherSolidsCategories.UvSphere:
                        case OtherSolidsCategories.UvHemisphere:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(false);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Int;
                            Slider1.SetMin(3, 3);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(3, 3);
                            Slider2.SetMax(16, 48);
                            Slider1.SetDescriptionText("Sides");
                            Slider2.SetDescriptionText("Slices");
                            break;
                        case OtherSolidsCategories.Torus:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Int;
                            Slider3.SliderType = SliderTypes.Float;
                            Slider1.SetMin(3, 3);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(3, 3);
                            Slider2.SetMax(16, 48);
                            Slider3.SetMin(.01f, .01f);
                            Slider3.SetMax(32, 80);
                            Slider1.SetDescriptionText("Sides");
                            Slider2.SetDescriptionText("Inner Sides");
                            Slider3.SetDescriptionText("Inner Radius");
                            break;
                        case OtherSolidsCategories.L_Shape:
                        case OtherSolidsCategories.C_Shape:
                        case OtherSolidsCategories.H_Shape:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Float;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.SliderType = SliderTypes.Float;
                            Slider1.SetMin(.1f, .1f);
                            Slider1.SetMax(3, 12);
                            Slider2.SetMin(.1f, .1f);
                            Slider2.SetMax(3, 12);
                            Slider3.SetMin(.1f, .1f);
                            Slider3.SetMax(3, 12);
                            Slider1.SetDescriptionText("Size 1");
                            Slider2.SetDescriptionText("Size 2");
                            Slider3.SetDescriptionText("Size 3");
                            break;
                        case OtherSolidsCategories.Arc:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.SliderType = SliderTypes.Float;
                            Slider1.SetMin(1f, 1f);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(.01f, .01f);
                            Slider2.SetMax(4, 12);
                            Slider3.SetMin(.01f, .01f);
                            Slider3.SetMax(1, 16);
                            Slider1.SetDescriptionText("Size 1");
                            Slider2.SetDescriptionText("Size 2");
                            Slider3.SetDescriptionText("Size 3");
                            break;
                        case OtherSolidsCategories.Arch:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.SliderType = SliderTypes.Float;
                            Slider1.SetMin(1f, 1f);
                            Slider1.SetMax(16, 48);
                            Slider2.SetMin(.01f, .01f);
                            Slider2.SetMax(4, 12);
                            Slider3.SetMin(.01f, .01f);
                            Slider3.SetMax(12, 48);
                            Slider1.SetDescriptionText("Size 1");
                            Slider2.SetDescriptionText("Size 2");
                            Slider3.SetDescriptionText("Size 3");
                            break;
                        case OtherSolidsCategories.Stairs:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.SliderType = SliderTypes.Float;
                            Slider1.SetMin(1, 1);
                            Slider1.SetMax(30, 80);
                            Slider2.SetMin(.1f, .1f);
                            Slider2.SetMax(30, 80);
                            Slider3.SetMin(.1f, .1f);
                            Slider3.SetMax(30, 80);
                            Slider1.SetDescriptionText("Steps");
                            Slider2.SetDescriptionText("Width");
                            Slider3.SetDescriptionText("Step Height");
                            break;
                        default:
                            Slider1.gameObject.SetActive(false);
                            Slider2.gameObject.SetActive(false);
                            Slider3.gameObject.SetActive(false);
                            break;
                    }

                    break;
            }
            Slider1.UpdateValueAbsolute(Slider1.GetCurrentValue());
            Slider2.UpdateValueAbsolute(Slider2.GetCurrentValue());
            Slider3.UpdateValueAbsolute(Slider3.GetCurrentValue());
        }

        public void HandleSavePreset(bool overwrite)
        {
            if (string.IsNullOrEmpty(CurrentPresetPath))
            {
                CurrentPresetPath = Path.Combine(
                    App.ShapeRecipesPath(),
                    Guid.NewGuid().ToString().Substring(0, 8)
                );
            }
            else
            {
                if (!overwrite)
                {
                    CurrentPresetPath += " (Copy)";
                }
            }
            SavePresetJson(CurrentPresetPath);
            RenderToImageFile($"{CurrentPresetPath}.png");
            EnablePresetSaveButtons(popupButtonEnabled: true);
        }

        void SavePresetJson(string presetPath)
        {
            EditableModelDefinition emDef = new EditableModelDefinition(PreviewPolyhedron.m_Instance.m_PolyRecipe);
            var jsonSerializer = new JsonSerializer
            {
                ContractResolver = new CustomJsonContractResolver()
            };

            using var textWriter = new StreamWriter($"{presetPath}.json");
            using var jsonWriter = new CustomJsonWriter(textWriter);
            jsonSerializer.Serialize(jsonWriter, emDef);
        }

        public void HandleLoadPresetFromPath(string path)
        {
            var jsonDeserializer = new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
            EditableModelDefinition emd;
            using (var textReader = new StreamReader(path))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                emd = jsonDeserializer.Deserialize<EditableModelDefinition>(jsonReader);
            }
            LoadFromDefinition(emd);
            CurrentPresetPath = path;
            EnablePresetSaveButtons(popupButtonEnabled: true);
        }

        public void HandleLoadPresetFromString(string presetText)
        {
            var jsonDeserializer = new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
            EditableModelDefinition emd;
            using (var textReader = new StringReader(presetText))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                emd = jsonDeserializer.Deserialize<EditableModelDefinition>(jsonReader);
            }
            LoadFromDefinition(emd);
            CurrentPresetPath = "";
            EnablePresetSaveButtons(popupButtonEnabled: false);
        }

        public string GetButtonTexturePath(GeneratorTypes mainType, string action)
        {
            action = action.Replace(" ", "_");
            switch (mainType)
            {
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    return $"ShapeButtons/gridshape_{action}";
                case GeneratorTypes.Radial:
                    return $"ShapeButtons/radial_{action}";
                case GeneratorTypes.Uniform:
                    return $"ShapeButtons/uniform_{action}";
                case GeneratorTypes.Shapes:
                case GeneratorTypes.Various:
                    return $"ShapeButtons/other_{action}";
            }
            Debug.LogError($"Unsupported generator type: {mainType}");
            return null;
        }

        public static string LabelFormatter(string text)
        {
            // Camel case to spaces
            text = Regex.Replace(text, @"[A-Z]", " $0");
            // Underscores to spaces then title case
            text = new CultureInfo("en-US", false).TextInfo
                .ToTitleCase(text.Replace("_", " "));
            return text;
        }

        public Texture2D GetButtonTexture(PolyhydraButtonTypes buttonType, string label)
        {
            string path;
            switch (buttonType)
            {
                case PolyhydraButtonTypes.MainCategory:
                    path = $"ShapeTypeButtons/{label}";
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.UniformType:
                    path = GetButtonTexturePath(GeneratorTypes.Uniform, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.RadialType:
                    path = GetButtonTexturePath(GeneratorTypes.Radial, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.GridType:
                    path = $"ShapeButtons/grid_{label}";
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.OtherSolidsType:
                    path = GetButtonTexturePath(GeneratorTypes.Various, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.GridShape:
                    path = GetButtonTexturePath(GeneratorTypes.RegularGrids, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.OperatorType:
                    path = $"IconButtons/{label}";
                    return Resources.Load<Texture2D>(path);
                default:
                    return null;
            }
        }

        public void SetButtonTextAndIcon(PolyhydraButtonTypes buttonType, string label, string friendlyLabel = "")
        {
            if (string.IsNullOrEmpty(friendlyLabel)) friendlyLabel = label;
            friendlyLabel = LabelFormatter(friendlyLabel);

            switch (buttonType)
            {
                case PolyhydraButtonTypes.MainCategory:
                    ButtonMainCategory.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonMainCategory.SetDescriptionText($"Category: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.UniformType:
                    ButtonUniformType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonUniformType.SetDescriptionText($"Type: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.RadialType:
                    ButtonRadialType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonRadialType.SetDescriptionText($"Type: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.GridType:
                    ButtonGridType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridType.SetDescriptionText($"Grid Type: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.OtherSolidsType:
                    ButtonOtherSolidsType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOtherSolidsType.SetDescriptionText($"Type: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.GridShape:
                    ButtonGridShape.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridShape.SetDescriptionText($"Grid Shape: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.OperatorType:
                    ButtonOpType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpType.SetDescriptionText($"Operation: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.FilterType:
                    ButtonOpFilterType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpFilterType.SetDescriptionText($"Filter: {friendlyLabel}");
                    break;
                case PolyhydraButtonTypes.ColorMethod:
                    ButtonColorMethod.GetComponentInChildren<TextMeshPro>().text = friendlyLabel;
                    ButtonColorMethod.SetDescriptionText($"Filter: {friendlyLabel}");
                    break;
            }
        }

        public void LoadFromWidget(EditableModelWidget ewidget)
        {
            // LoadFromRecipe(ewidget.m_PolyRecipe);
            var edef = new EditableModelDefinition(ewidget.m_PolyRecipe);
            LoadFromDefinition(edef);
        }

        public void LoadFromDefinition(EditableModelDefinition emd)
        {
            void setSlidersFromGeneratorParams(List<string> names)
            {
                if (names.Count == 0) return;
                var sliderParamValues = names.Select(n => Convert.ToSingle(emd.GeneratorParameters[n])).ToList();

                Slider1.UpdateValueAbsolute(sliderParamValues[0]);
                if (sliderParamValues.Count == 1) return;

                Slider2.UpdateValueAbsolute(sliderParamValues[1]);
                if (sliderParamValues.Count == 2) return;

                Slider3.UpdateValueAbsolute(sliderParamValues[2]);
            }

            // If no colors are supplied then use the current palette
            Color[] colors;
            if (emd.Colors == null || emd.Colors.Length == 0)
            {
                colors = (Color[])DefaultColorPalette.Clone();
            }
            else
            {
                colors = (Color[])emd.Colors.Clone();
            }
            List<string> colorStrings = colors.Select(c => $"#{ColorUtility.ToHtmlStringRGB(c)}").ToList();
            SetColorsToPalette(colorStrings);
            HandleSetColorMethod(emd.ColorMethod);
            SetMaterial(emd.MaterialIndex);

            PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = emd.GeneratorType;

            var sliderParamNames = new List<string>();

            // Set up generator UI to match preset

            // Widgets must be visible when setting textures
            ShowAllGeneratorControls();

            switch (emd.GeneratorType)
            {
                case GeneratorTypes.FileSystem:
                case GeneratorTypes.ConwayString:
                case GeneratorTypes.Johnson:
                    Debug.LogError($"Preset has unsupported generator type: {emd.GeneratorType}");
                    break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    m_CurrentMainCategory = emd.GeneratorType switch
                    {

                        GeneratorTypes.RegularGrids => PolyhydraMainCategories.RegularGrids,
                        GeneratorTypes.CatalanGrids => PolyhydraMainCategories.CatalanGrids,
                        GeneratorTypes.OneUniformGrids => PolyhydraMainCategories.ArchimedeanGrids,
                        GeneratorTypes.TwoUniformGrids => PolyhydraMainCategories.TwoUniformGrids,
                        GeneratorTypes.DurerGrids => PolyhydraMainCategories.DurerGrids,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.GridType = (GridEnums.GridTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.GridShape = (GridEnums.GridShapes)Convert.ToInt32(emd.GeneratorParameters["shape"]);
                    sliderParamNames = new List<string> { "x", "y" };
                    break;
                case GeneratorTypes.Shapes:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = (ShapeTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            m_OtherSolidsCategory = OtherSolidsCategories.Polygon;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.Polygon;
                            sliderParamNames = new List<string> { "sides" };
                            break;
                        case ShapeTypes.Star:
                            m_OtherSolidsCategory = OtherSolidsCategories.Star;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.Star;
                            sliderParamNames = new List<string> { "sides", "sharpness" };
                            break;
                        case ShapeTypes.L_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.L_Shape;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.L_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.C_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.C_Shape;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.C_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.H_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.H_Shape;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.H_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.Arc:
                            m_OtherSolidsCategory = OtherSolidsCategories.Arc;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.Arc;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.Arch:
                            m_OtherSolidsCategory = OtherSolidsCategories.Arch;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = ShapeTypes.Arch;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = (VariousSolidTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            m_OtherSolidsCategory = OtherSolidsCategories.Box;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = VariousSolidTypes.Box;
                            sliderParamNames = new List<string> { "x", "y", "z" };
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvHemisphere;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = VariousSolidTypes.UvHemisphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                        case VariousSolidTypes.UvSphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvSphere;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = VariousSolidTypes.UvSphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                        case VariousSolidTypes.Torus:
                            m_OtherSolidsCategory = OtherSolidsCategories.Torus;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = VariousSolidTypes.Torus;
                            sliderParamNames = new List<string> { "x", "y", "z" };
                            break;
                        case VariousSolidTypes.Stairs:
                            m_OtherSolidsCategory = OtherSolidsCategories.Stairs;
                            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = VariousSolidTypes.Stairs;
                            sliderParamNames = new List<string> { "x", "y", "z" };
                            break;
                    }
                    break;
                case GeneratorTypes.Radial:
                    m_CurrentMainCategory = PolyhydraMainCategories.Radial;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    sliderParamNames = new List<string> { "sides", "height", "capheight" };
                    break;
                case GeneratorTypes.Waterman:
                    m_CurrentMainCategory = PolyhydraMainCategories.Waterman;
                    sliderParamNames = new List<string> { "root", "c" };
                    break;
                case GeneratorTypes.Uniform:
                    int subtypeID = Convert.ToInt32(emd.GeneratorParameters["type"]);
                    var uniformType = Uniform.Uniforms[subtypeID + 1]; // Uniforms are 1-indexed...
                    if (Uniform.Platonic.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.Platonic;
                    }
                    else if (Uniform.Archimedean.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.Archimedean;
                    }
                    else if (Uniform.KeplerPoinsot.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.KeplerPoinsot;
                    }
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.UniformPolyType = (UniformTypes)subtypeID;
                    break;
            }

            SetMainButtonVisibility();
            SetSliderConfiguration();
            setSlidersFromGeneratorParams(sliderParamNames);

            // Set Op UI to match preset

            // Widgets must be visible when setting textures
            ShowAllOpControls();

            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators = new List<PreviewPolyhedron.OpDefinition>();

            foreach (var opDict in emd.Operations)
            {
                var newOp = new PreviewPolyhedron.OpDefinition
                {
                    opType = (PolyMesh.Operation)Convert.ToInt32(opDict["operation"]),
                    disabled = Convert.ToBoolean(opDict["disabled"]),
                    amount = Convert.ToSingle(opDict["param1"]),
                    amountRandomize = Convert.ToBoolean(opDict.GetValueOrDefault("param1Randomize")),
                    amount2 = Convert.ToSingle(opDict["param2"]),
                    amount2Randomize = Convert.ToBoolean(opDict.GetValueOrDefault("param2Randomize")),
                };

                if (opDict.ContainsKey("paramColor"))
                {
                    var colorData = opDict["paramColor"] as JArray;
                    if (colorData != null && colorData.Count >= 3)
                    {
                        newOp.paramColor = new Color(
                            colorData[0].Value<float>(),
                            colorData[1].Value<float>(),
                            colorData[2].Value<float>()
                        );
                    }
                }

                object filterType;
                object filterParamFloat;
                object filterParamInt;
                object filterNot;

                if (opDict.TryGetValue("filterType", out filterType))
                {
                    opDict.TryGetValue("filterParamFloat", out filterParamFloat);
                    opDict.TryGetValue("filterParamInt", out filterParamInt);
                    opDict.TryGetValue("filterNot", out filterNot);
                    newOp.filterType = (FilterTypes)Convert.ToInt32(filterType);
                    newOp.filterParamFloat = Convert.ToSingle(filterParamFloat);
                    newOp.filterParamInt = Convert.ToInt32(filterParamInt);
                    newOp.filterNot = Convert.ToBoolean(filterNot);
                }

                PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Add(newOp);
                AddOpButton();
            }

            if (CurrentActiveOpIndex > PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1)
            {
                CurrentActiveOpIndex = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1;
            }

            RefreshOpSelectButtons();
            HandleSelectOpButton(PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1);

            ShowAllGeneratorControls();

            PreviewPolyhedron.m_Instance.RebuildPoly();
            RebuildPreviewAndLinked();
        }

        // I'm actually only using LoadFromDefinition currently
        // and converting recipes to EditableDefinition beforehand
        // This method currently doesn't set up the op buttons correctly
        // It also duplicates a ton of logic with LoadFromDefinition
        // Keeping it around mainly for reference purposes at the moment
        public void LoadFromRecipe(PolyRecipe recipe)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe = recipe;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators = recipe.Operators;  // Clone

            // If no colors are supplied then use the current palette
            Color[] colors;
            if (recipe.Colors == null || recipe.Colors.Length == 0)
            {
                colors = (Color[])recipe.Colors.Clone();
            }
            else
            {
                colors = recipe.Colors;
            }

            List<string> colorStrings = colors.Select(c => $"#{ColorUtility.ToHtmlStringRGB(c)}").ToList();
            SetColorsToPalette(colorStrings);

            HandleSetColorMethod(recipe.ColorMethod);
            SetMaterial(recipe.MaterialIndex);

            // Set up generator UI to match preset

            // Widgets must be visible when setting textures
            ShowAllGeneratorControls();

            switch (recipe.GeneratorType)
            {
                case GeneratorTypes.FileSystem:
                case GeneratorTypes.ConwayString:
                case GeneratorTypes.Johnson:
                    Debug.LogError($"Preset has unsupported generator type: {recipe.GeneratorType}");
                    break;
                case GeneratorTypes.RegularGrids:
                    m_CurrentMainCategory = PolyhydraMainCategories.RegularGrids;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.CatalanGrids:
                    m_CurrentMainCategory = PolyhydraMainCategories.CatalanGrids;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.OneUniformGrids:
                    m_CurrentMainCategory = PolyhydraMainCategories.ArchimedeanGrids;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.TwoUniformGrids:
                    m_CurrentMainCategory = PolyhydraMainCategories.TwoUniformGrids;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.DurerGrids:
                    m_CurrentMainCategory = PolyhydraMainCategories.DurerGrids;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.Shapes:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    switch (recipe.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            m_OtherSolidsCategory = OtherSolidsCategories.Polygon;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            break;
                        case ShapeTypes.Star:
                            m_OtherSolidsCategory = OtherSolidsCategories.Star;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            break;
                        case ShapeTypes.L_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.L_Shape;
                            Slider1.UpdateValueAbsolute(recipe.Param1Float);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                        case ShapeTypes.C_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.C_Shape;
                            Slider1.UpdateValueAbsolute(recipe.Param1Float);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                        case ShapeTypes.H_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.H_Shape;
                            Slider1.UpdateValueAbsolute(recipe.Param1Float);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                        case ShapeTypes.Arc:
                            m_OtherSolidsCategory = OtherSolidsCategories.Arc;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                        case ShapeTypes.Arch:
                            m_OtherSolidsCategory = OtherSolidsCategories.Arch;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    switch (recipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            m_OtherSolidsCategory = OtherSolidsCategories.Box;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Int);
                            Slider3.UpdateValueAbsolute(recipe.Param3Int);
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvHemisphere;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Int);
                            break;
                        case VariousSolidTypes.UvSphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvSphere;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Int);
                            break;
                        case VariousSolidTypes.Torus:
                            m_OtherSolidsCategory = OtherSolidsCategories.Torus;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Int);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                        case VariousSolidTypes.Stairs:
                            m_OtherSolidsCategory = OtherSolidsCategories.Stairs;
                            Slider1.UpdateValueAbsolute(recipe.Param1Int);
                            Slider2.UpdateValueAbsolute(recipe.Param2Float);
                            Slider3.UpdateValueAbsolute(recipe.Param3Float);
                            break;
                    }
                    break;
                case GeneratorTypes.Radial:
                    m_CurrentMainCategory = PolyhydraMainCategories.Radial;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Float);
                    Slider3.UpdateValueAbsolute(recipe.Param3Float);
                    break;
                case GeneratorTypes.Waterman:
                    m_CurrentMainCategory = PolyhydraMainCategories.Waterman;
                    Slider1.UpdateValueAbsolute(recipe.Param1Int);
                    Slider2.UpdateValueAbsolute(recipe.Param2Int);
                    break;
                case GeneratorTypes.Uniform:
                    var uniformType = Uniform.Uniforms[(int)recipe.UniformPolyType];
                    if (Uniform.Platonic.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.Platonic;
                    }
                    else if (Uniform.Archimedean.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.Archimedean;
                    }
                    else if (Uniform.KeplerPoinsot.Contains(uniformType))
                    {
                        m_CurrentMainCategory = PolyhydraMainCategories.KeplerPoinsot;
                    }
                    break;
            }

            SetMainButtonVisibility();
            SetSliderConfiguration();

            // Set Op UI to match preset

            // Widgets must be visible when setting textures
            ShowAllOpControls();

            RefreshOpSelectButtons();
            HandleSelectOpButton(recipe.Operators.Count - 1);
            ShowAllGeneratorControls();
            RebuildPreviewAndLinked();
        }

        public static Bounds CalculateBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            Object[] rList = go.GetComponentsInChildren(typeof(Renderer));
            foreach (Renderer r in rList)
            {
                b.Encapsulate(r.bounds);
            }
            return b;
        }

        public static void FocusCameraOnGameObject(Camera c, GameObject go, float zoomFactor, bool randomPos)
        {
            Bounds b = CalculateBounds(go);
            Vector3 max = b.size;
            float radius = Mathf.Max(max.x, Mathf.Max(max.y, max.z));
            float dist = radius / (Mathf.Sin(c.fieldOfView * Mathf.Deg2Rad / 2f));
            dist *= zoomFactor;
            Vector3 pos;
            if (randomPos)
            {
                pos = Random.onUnitSphere * dist + b.center;
            }
            else
            {
                var vector = b.center - c.transform.position;
                vector.Normalize();
                vector *= Mathf.Abs(dist);
                pos = -vector;
            }
            c.transform.position = pos;
            c.transform.LookAt(b.center);
        }

        void RenderToImageFile(string presetThumbnailPath)
        {
            Camera thumbnailCamera = PreviewPolyhedron.m_Instance.GetComponentInChildren<Camera>(true);
            thumbnailCamera.enabled = true;
            thumbnailCamera.gameObject.SetActive(true);
            FocusCameraOnGameObject(thumbnailCamera, PreviewPolyhedron.m_Instance.gameObject, 0.5f, true);
            RenderTexture activeRenderTexture = RenderTexture.active;
            var tex = new RenderTexture(256, 256, 32);
            thumbnailCamera.targetTexture = tex;
            RenderTexture.active = thumbnailCamera.targetTexture;
            thumbnailCamera.Render();
            Texture2D image = new Texture2D(tex.width, tex.height);
            image.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            image.Apply();
            RenderTexture.active = activeRenderTexture;
            byte[] bytes = image.EncodeToPNG();
            Destroy(image);
            File.WriteAllBytes(presetThumbnailPath, bytes);
            thumbnailCamera.gameObject.SetActive(false);
            thumbnailCamera.enabled = false;
        }

        // Used mainly in mono mode and during testing
        public void MonoscopicAddPolyhedron()
        {
            var poly = PreviewPolyhedron.m_Instance.m_PolyMesh;
            // Just a random position for now to avoid overlap.
            var tr = TrTransform.TRS(
                new Vector3(Random.value * 3 - 1.5f, Random.value * 7 + 7, Random.value * 8 + 2),
                Quaternion.identity,
                1f
            );
            PolyhydraTool polyTool = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.PolyhydraTool) as PolyhydraTool;
            polyTool.CreatePolyForCurrentMode(poly, tr);
        }

        public void ChangeCurrentOpType(string operationName)
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            if (CurrentActiveOpIndex > PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1)
            {
                Debug.LogWarning($"CurrentActiveOpIndex: {CurrentActiveOpIndex} and Operators.Count: {PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count}");
            }
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            OpConfig opConfig = OpConfigs.Configs[op.opType];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            op.amount = opConfig.amountDefault;
            op.amount2 = opConfig.amount2Default;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;

            RefreshOpSelectButtons();
            ConfigureOpPanel(op);
        }

        public void HandleSelectOpButton(int index)
        {
            CurrentActiveOpIndex = index;
            RefreshOpSelectButtons();
            if (PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count > 0)
            {
                var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[index];
                ConfigureOpPanel(op);
            }
            else
            {
                OpPanel.SetActive(false);
            }
        }

        public void ConfigureOpPanel(PreviewPolyhedron.OpDefinition op)
        {
            OpPanel.SetActive(true);
            OpConfig opConfig = OpConfigs.Configs[op.opType];
            OpFilterControlParent.SetActive(opConfig.usesFilter);
            ButtonOpDisable.ToggleState = op.disabled;

            if (opConfig.usesAmount)
            {
                SliderOpParam1.gameObject.SetActive(true);
                SliderOpParam1.SetMin(opConfig.amountSafeMin, opConfig.amountMin);
                SliderOpParam1.SetMax(opConfig.amountSafeMax, opConfig.amountMax);
                SliderOpParam1.UpdateValueAbsolute(op.amount);
                SliderOpParam1.GetComponentInChildren<ActionToggleButton>().ToggleState = op.amountRandomize;
            }
            else
            {
                SliderOpParam1.gameObject.SetActive(false);
            }

            if (opConfig.usesAmount2)
            {
                SliderOpParam2.gameObject.SetActive(true);
                SliderOpParam2.SetMin(opConfig.amount2SafeMin, opConfig.amount2Min);
                SliderOpParam2.SetMax(opConfig.amount2SafeMax, opConfig.amount2Max);
                SliderOpParam2.UpdateValueAbsolute(op.amount2);
                SliderOpParam2.GetComponentInChildren<ActionToggleButton>().ToggleState = op.amount2Randomize;
            }
            else
            {
                SliderOpParam2.gameObject.SetActive(false);
            }

            if (opConfig.usesColor)
            {
                ButtonOpColorPicker.gameObject.SetActive(true);
            }
            else
            {
                ButtonOpColorPicker.gameObject.SetActive(false);
            }

            ConfigureOpFilterPanel(op);

        }

        public void FinalColorButtonPressed(int index)
        {
            SketchControlsScript.GlobalCommands command = SketchControlsScript.GlobalCommands.PolyhydraColorPickerPopup;
            CreatePopUp(command, -1, -1, "Color",
                () => ColorPalletteButtons[index].SetColorSwatch(PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors[index])
            );

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += c =>
            {
                SetFinalColor(c, index);
                RebuildPreviewAndLinked();
            };

            // Init must be called after all popup.ColorPicked actions have been assigned.
            popup.ColorPicker.Controller.CurrentColor = PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors[index];

            m_EatInput = true;
        }

        public void OpColorButtonPressed(int index)
        {
            // Create the popup with callback.
            SketchControlsScript.GlobalCommands command = SketchControlsScript.GlobalCommands.PolyhydraColorPickerPopup;
            CreatePopUp(command, -1, -1, "Color",
                () => ButtonOpColorPicker.SetColorSwatch(GetOpColor())
            );

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += delegate (Color c)
            {
                ButtonOpColorPicker.SetColorSwatch(c);
                SetOpColor(c);
            };

            // Init must be called after all popup.ColorPicked actions have been assigned.
            popup.ColorPicker.Controller.CurrentColor = GetOpColor();

            m_EatInput = true;
        }

        private Color GetOpColor()
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            return op.paramColor;
        }

        private void SetOpColor(Color color)
        {
            ButtonOpColorPicker.SetColorSwatch(color);
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            op.paramColor = color;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex] = op;
            RebuildPreviewAndLinked();
        }

        public void HandleSetAllColorsToCurrentButtonPressed()
        {
            var color = PointerManager.m_Instance.PointerColor;
            for (var index = 0; index < ColorPalletteButtons.Length; index++)
            {
                SetFinalColor(color, index);
            }
            RebuildPreviewAndLinked();
        }

        public void HandleSetColorsToPalette(int paletteIndex)
        {
            SetColorsToPalette(ColorPalettes[paletteIndex]);
        }

        private void SetColorsToPalette(List<string> palette)
        {
            for (int index = 0; index < ColorPalletteButtons.Length; index++)
            {
                string colorString = palette[index];
                if (ColorUtility.TryParseHtmlString(colorString, out Color color))
                {
                    SetFinalColor(color, index);
                }
            }
            RebuildPreviewAndLinked();
        }

        public void HandleResetAllColorsToDefaultButtonPressed()
        {
            for (int index = 0; index < ColorPalletteButtons.Length; index++)
            {
                SetFinalColor(DefaultColorPalette[index], index);
            }
            RebuildPreviewAndLinked();
        }

        public void HandleJitterAllColorsButtonPressed()
        {
            for (var index = 0; index < ColorPalletteButtons.Length; index++)
            {
                Color currentColor = PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors[index];
                Color newColor = PointerManager.m_Instance.CalculateJitteredColor(currentColor);
                SetFinalColor(newColor, index);
            }
            RebuildPreviewAndLinked();
        }

        private void SetFinalColor(Color color, int index)
        {
            PolyhydraColorButton btn = ColorPalletteButtons[index];
            btn.SetColorSwatch(color);
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors[index] = color;
        }

        public void HandleAddOpButton()
        {
            var newOp = new PreviewPolyhedron.OpDefinition
            {
                disabled = false,
                filterNot = false
            };
            AddOpButton();
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Add(newOp);
            HandleSelectOpButton(PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1);
        }

        public void AddOpButton()
        {
            Transform btnTr = Instantiate(OperatorSelectButtonPrefab, OperatorSelectButtonParent, false);
            btnTr.gameObject.SetActive(true);
        }

        public void HandleOtherSolidsButtonPress(string action)
        {
            m_OtherSolidsCategory = (OtherSolidsCategories)Enum.Parse(typeof(OtherSolidsCategories), action);
            switch (m_OtherSolidsCategory)
            {
                case OtherSolidsCategories.Polygon:
                case OtherSolidsCategories.Star:
                case OtherSolidsCategories.C_Shape:
                case OtherSolidsCategories.L_Shape:
                case OtherSolidsCategories.H_Shape:
                case OtherSolidsCategories.Arc:
                case OtherSolidsCategories.Arch:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Shapes;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = (ShapeTypes)Enum.Parse(typeof(ShapeTypes), action);
                    break;
                case OtherSolidsCategories.UvSphere:
                case OtherSolidsCategories.UvHemisphere:
                case OtherSolidsCategories.Box:
                case OtherSolidsCategories.Torus:
                case OtherSolidsCategories.Stairs:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Various;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = (VariousSolidTypes)Enum.Parse(typeof(VariousSolidTypes), action);
                    break;
            }
            SetSliderConfiguration();
        }

        public void HandleMainCategoryButtonPress(PolyhydraMainCategories mainCategory)
        {
            var recipe = PreviewPolyhedron.m_Instance.m_PolyRecipe;
            m_CurrentMainCategory = mainCategory;
            SetMainButtonVisibility();
            SetSliderConfiguration();
            switch (m_CurrentMainCategory)
            {
                case PolyhydraMainCategories.Platonic:
                    recipe.GeneratorType = GeneratorTypes.Uniform;
                    recipe.UniformPolyType = (UniformTypes)Uniform.Platonic[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, recipe.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Archimedean:
                    recipe.GeneratorType = GeneratorTypes.Uniform;
                    recipe.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, recipe.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.KeplerPoinsot:
                    recipe.GeneratorType = GeneratorTypes.Uniform;
                    recipe.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, recipe.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Radial:
                    recipe.GeneratorType = GeneratorTypes.Radial;
                    recipe.RadialPolyType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, recipe.RadialPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Waterman:
                    recipe.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case PolyhydraMainCategories.RegularGrids:
                    recipe.GeneratorType = GeneratorTypes.RegularGrids;
                    recipe.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    recipe.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;
                case PolyhydraMainCategories.CatalanGrids:
                    recipe.GeneratorType = GeneratorTypes.CatalanGrids;
                    recipe.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    recipe.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;
                case PolyhydraMainCategories.ArchimedeanGrids:
                    recipe.GeneratorType = GeneratorTypes.OneUniformGrids;
                    recipe.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    recipe.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;
                case PolyhydraMainCategories.TwoUniformGrids:
                    recipe.GeneratorType = GeneratorTypes.TwoUniformGrids;
                    recipe.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    recipe.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;
                case PolyhydraMainCategories.DurerGrids:
                    recipe.GeneratorType = GeneratorTypes.DurerGrids;
                    recipe.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, recipe.GridType.ToString());
                    recipe.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, recipe.GridShape.ToString());
                    break;
                case PolyhydraMainCategories.Various:
                    // Various can map to either GeneratorTypes.Various or GeneratorTypes.Shapes
                    switch (m_OtherSolidsCategory)
                    {
                        case OtherSolidsCategories.Polygon:
                        case OtherSolidsCategories.Star:
                        case OtherSolidsCategories.C_Shape:
                        case OtherSolidsCategories.L_Shape:
                        case OtherSolidsCategories.H_Shape:
                        case OtherSolidsCategories.Arc:
                        case OtherSolidsCategories.Arch:
                            recipe.GeneratorType = GeneratorTypes.Shapes;
                            recipe.ShapeType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, recipe.ShapeType.ToString());
                            break;
                        case OtherSolidsCategories.UvSphere:
                        case OtherSolidsCategories.UvHemisphere:
                        case OtherSolidsCategories.Box:
                        case OtherSolidsCategories.Torus:
                        case OtherSolidsCategories.Stairs:
                            recipe.GeneratorType = GeneratorTypes.Various;
                            recipe.VariousSolidsType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, recipe.VariousSolidsType.ToString());
                            break;
                    }
                    break;
            }
            PreviewPolyhedron.m_Instance.m_PolyRecipe = recipe;
        }

        public List<string> GetOtherSolidCategoryNames()
        {
            return Enum.GetNames(typeof(OtherSolidsCategories)).ToList();
        }

        public List<string> GetMainCategoryNames()
        {
            return Enum.GetNames(typeof(PolyhydraMainCategories)).ToList();
        }

        public List<string> GetGridTypeNames()
        {
            List<GridEnums.GridTypes> gridTypeList = null;
            switch (m_CurrentMainCategory)
            {
                case PolyhydraMainCategories.RegularGrids:
                    gridTypeList = GridEnums.RegularGridTypes;
                    break;
                case PolyhydraMainCategories.ArchimedeanGrids:
                    gridTypeList = GridEnums.ArchimedeanGridTypes;
                    break;
                case PolyhydraMainCategories.CatalanGrids:
                    gridTypeList = GridEnums.CatalanGridTypes;
                    break;
                case PolyhydraMainCategories.DurerGrids:
                    gridTypeList = GridEnums.DurerGridTypes;
                    break;
                case PolyhydraMainCategories.TwoUniformGrids:
                    gridTypeList = GridEnums.TwoUniformGridTypes;
                    break;
            }
            return gridTypeList.Select(x => x.ToString()).ToList();
        }

        public List<string> GetUniformPolyNames()
        {
            Uniform[] uniformList = null;
            switch (m_CurrentMainCategory)
            {
                case PolyhydraMainCategories.Platonic:
                    uniformList = Uniform.Platonic;
                    break;
                case PolyhydraMainCategories.Archimedean:
                    uniformList = Uniform.Archimedean;
                    break;
                case PolyhydraMainCategories.KeplerPoinsot:
                    uniformList = Uniform.KeplerPoinsot;
                    break;
            }
            return uniformList.Select(x => x.Name).ToList();
        }

        public void ConfigureOpFilterPanel(PreviewPolyhedron.OpDefinition op)
        {
            var opFilterName = op.filterType.ToString();
            ButtonOpFilterType.SetDescriptionText(opFilterName);

            ButtonOpFilterNot.gameObject.SetActive(true);
            ButtonOpFilterNot.ToggleState = op.filterNot;
            LabelOpFilterName.text = opFilterName;

            switch (op.filterType)
            {
                case FilterTypes.All:
                case FilterTypes.Inner:
                case FilterTypes.EvenSided:
                    SliderOpFilterParam.gameObject.SetActive(false);
                    break;
                case FilterTypes.Role:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(10, 10);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.OnlyNth:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(32, 320);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.EveryNth:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.SetMin(2, 2);
                    SliderOpFilterParam.SetMax(8, 64);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.FirstN:
                case FilterTypes.LastN:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.SetMin(1, 1);
                    SliderOpFilterParam.SetMax(32, 320);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.NSided:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.SetMin(3, 3);
                    SliderOpFilterParam.SetMax(12, 32);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;

                case FilterTypes.FacingUp:
                case FilterTypes.FacingForward:
                case FilterTypes.FacingRight:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(180, 180);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.FacingVertical:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(90, 90);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.Random:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(1, 1);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.PositionX:
                case FilterTypes.PositionY:
                case FilterTypes.PositionZ:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.SetMin(-5, -30);
                    SliderOpFilterParam.SetMax(5, 30);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.DistanceFromCenter:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.SetMin(0, 0);
                    SliderOpFilterParam.SetMax(10, 30);
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
            }
        }

        public void HandleOpDelete()
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex = Mathf.Min(PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Count - 1, CurrentActiveOpIndex);
            HandleSelectOpButton(CurrentActiveOpIndex);
            RebuildPreviewAndLinked();
            RefreshOpSelectButtons();
        }

        [ContextMenu("Test RefreshOpSelectButtons")]
        private void RefreshOpSelectButtons()
        {
            var recipe = PreviewPolyhedron.m_Instance.m_PolyRecipe;
            bool hasOps = recipe.Operators.Count > 0;
            CurrentActiveOpIndex = Mathf.Min(CurrentActiveOpIndex, recipe.Operators.Count - 1);
            OpPanel.gameObject.SetActive(hasOps);
            OperatorSelectPopupTools.gameObject.SetActive(hasOps);

            var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();

            for (var i = 0; i < btns.Length; i++)
            {
                var btn = btns[i];
                var btnParent = btn.transform.parent;
                if (i > recipe.Operators.Count - 1)
                {
                    Destroy(btnParent.gameObject);
                    continue;
                }
                var op = recipe.Operators[i];
                btn.OpIndex = i;
                string opName = op.opType.ToString();
                btn.SetDescriptionText(LabelFormatter(opName));
                var tex = GetButtonTexture(PolyhydraButtonTypes.OperatorType, opName);
                btn.gameObject.SetActive(true);
                btn.SetButtonTexture(tex);

                btn.SetButtonOverlay(op.disabled);

                btn.name = $"Op {i}: {op.opType}";
                btn.ParentPanel = this;

                var btnPos = btn.transform.localPosition;
                btnPos.Set(i * 0.25f, 0, 0);
                btnParent.transform.localPosition = btnPos;

                if (i == CurrentActiveOpIndex)
                {
                    var popupPos = btnParent.transform.localPosition;
                    popupPos.Set(i * 0.25f + 0.04f, 0.05f, 0);
                    OperatorSelectPopupTools.localPosition = popupPos;
                    OperatorSelectPopupTools.localScale = Vector3.one * 0.2f;
                    ToolBtnPrev.gameObject.SetActive(i > 0);
                    ToolBtnNext.gameObject.SetActive(i < recipe.Operators.Count - 1);
                    FriendlyOpLabels.TryGetValue(opName, out string friendlyLabel);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, opName, friendlyLabel);
                }

            }
            if (gameObject.activeSelf)
            {
                AnimateOpParentIntoPlace();
            }
        }

        public void HandleMaterialButton(int index)
        {
            SetMaterial(index);
        }

        public void SetMaterial(int index)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.MaterialIndex = index;
            var mat = PreviewPolyhedron.m_Instance.m_PolyRecipe.CurrentMaterial;
            var mr = PreviewPolyhedron.m_Instance.GetComponent<MeshRenderer>();
            mr.material = mat;

            // TODO
            // We can do this
            RebuildPreviewAndLinked();

            // ...which does what we want but also a ton of extra work.
            // But all we really are missing is this bit
            // from "AssignMesh":
            // if (m_UpdateSelectedModels)
            // {
            //     foreach (var widget in GetSelectedWidgets())
            //     {
            //         EditableModelManager.UpdateWidgetFromPolyMesh(widget, m_PolyMesh, m_PolyRecipe.Clone());
            //     }
            // }
            // Probably can refactor to just do the last bit without all the rest
            // without code duplication?
        }

        public void HandleOpMove(int delta)
        {
            var op = PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators[CurrentActiveOpIndex];
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex += delta;
            PreviewPolyhedron.m_Instance.m_PolyRecipe.Operators.Insert(CurrentActiveOpIndex, op);
            HandleSelectOpButton(CurrentActiveOpIndex);
            RebuildPreviewAndLinked();
            RefreshOpSelectButtons();
        }

        public bool IsPresetsSubdirOrSameDir(string dirPath)
        {
            return dirPath.StartsWith(App.ShapeRecipesPath());
        }

        public bool PresetRootIsCurrent()
        {
            return Path.GetFullPath(App.ShapeRecipesPath()) == Path.GetFullPath(CurrentPresetsDirectory);
        }

        public void HandleUpdateSelectedModelsToggle(UpdateSelectedModelsToggleButton btn)
        {
            PreviewPolyhedron.m_Instance.m_UpdateSelectedModels = btn.ToggleState;
        }

        public void HandleSetColorMethod(ColorMethods colorMethod)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.ColorMethod = colorMethod;
            SetButtonTextAndIcon(PolyhydraButtonTypes.ColorMethod, colorMethod.ToString());
            RebuildPreviewAndLinked();
        }
    }

} // namespace TiltBrush
