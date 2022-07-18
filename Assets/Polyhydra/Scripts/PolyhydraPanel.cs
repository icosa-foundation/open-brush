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
        Preset
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

        public PolyhydraSlider Slider1;
        public PolyhydraSlider Slider2;
        public PolyhydraSlider Slider3;

        public Transform m_PreviewAttachPoint;
        public GameObject AllGeneratorControls;
        public GameObject AllOpControls;
        public GameObject AllAppearanceControls;
        public GameObject OpPanel;
        public PolyhydraOptionButton ButtonOpType;
        public PolyhydraSlider SliderOpParam1;
        public PolyhydraSlider SliderOpParam2;
        public PolyhydraColorButton ButtonOpColorPicker;
        public PolyhydraColorButton[] ColorPalletteButtons;
        public GameObject OpFilterControlParent;
        public PolyhydraOptionButton ButtonOpFilterType;
        public PolyhydraSlider SliderOpFilterParam;
        public TextMeshPro LabelOpFilterName;
        public ActionToggleButton ButtonOpFilterNot;
        public ActionToggleButton ButtonOpDisable;

        public List<GameObject> MonoscopicOnlyButtons;

        public int CurrentActiveOpIndex;
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

        public enum PolyhydraMainCategories
        {
            Platonic,
            Archimedean,
            KeplerPoinsot,
            Radial,
            Waterman,
            Grids,
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
            Box,

            C_Shape,
            L_Shape,
            H_Shape,
        }

        public string CurrentPresetPath
        {
            // Full directory and filename but with "json" extension removed

            get => m_CurrentPresetPath;
            set => m_CurrentPresetPath = value.Replace(".json", "");
        }
        public int CurrentPresetPage { get; set; }
        public int CurrentOperatorPage { get; set; }

        public string DefaultPresetsDirectory()
        {
            return Path.Combine(App.UserPath(), "Media Library/Shape Recipes/");
        }

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
            InitPreviewPoly(false);
            ShowAllGeneratorControls();
            OpFilterControlParent.SetActive(false);
            OpPanel.SetActive(false);
            SetSliderConfiguration();
            SetMainButtonVisibility();
            EnablePresetSaveButtons(popupButtonEnabled: false);
            if (!Directory.Exists(DefaultPresetsDirectory()))
            {
                Directory.CreateDirectory(DefaultPresetsDirectory());
            }

            if (EditableModelManager.CurrentModel.Colors == null)
            {
                EditableModelManager.CurrentModel.Colors = (Color[])DefaultColorPalette.Clone();
            }

            for (var i = 0; i < ColorPalletteButtons.Length; i++)
            {
                ColorPalletteButtons[i].SetColorSwatch(EditableModelManager.CurrentModel.Colors[i]);
            }
        }

        private void EnablePresetSaveButtons(bool popupButtonEnabled)
        {
            PresetInitialSaveButton.SetActive(!popupButtonEnabled);
            PresetSaveOptionsPopupButton.SetActive(popupButtonEnabled);
        }

        public void HandleSlider1(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.Param1Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.Param1Float = value.z;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleSlider2(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.Param2Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.Param2Float = value.z;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleSlider3(Vector3 value)
        {
            PreviewPolyhedron.m_Instance.Param3Int = Mathf.FloorToInt(value.z);
            PreviewPolyhedron.m_Instance.Param3Float = value.z;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleOpAmountSlider(Vector3 value)
        {
            int paramIndex = (int)value.y;
            float amount = value.z;
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amount = amount;
                    break;
                case 1:
                    op.amount2 = amount;
                    break;
            }
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleOpDisableButton()
        {
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            op.disabled = !op.disabled;
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            RefreshOpSelectButtons();
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleOpRandomizeButton(int paramIndex)
        {
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amountRandomize = !op.amountRandomize;
                    break;
                case 1:
                    op.amount2Randomize = !op.amount2Randomize;
                    break;
            }
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.RebuildPoly();
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

        public void ShowAllAppearanceControls()
        {
            AllGeneratorControls.SetActive(false);
            AllOpControls.SetActive(false);
            AllAppearanceControls.SetActive(true);
        }

        public void HandleSliderFilterParam(Vector3 value)
        {
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            op.filterParamInt = Mathf.FloorToInt(value.z);
            op.filterParamFloat = value.z;
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleButtonOpNot()
        {
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            op.filterNot = !op.filterNot;
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.RebuildPoly();
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
        }

        public void SetMainButtonVisibility()
        {
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
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, PreviewPolyhedron.m_Instance.UniformPolyType.ToString());
                    break;

                case PolyhydraMainCategories.Grids:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(true);
                    ButtonGridShape.gameObject.SetActive(true);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, PreviewPolyhedron.m_Instance.GridType.ToString());
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, PreviewPolyhedron.m_Instance.GridShape.ToString());
                    break;

                case PolyhydraMainCategories.Various:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(true);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, PreviewPolyhedron.m_Instance.VariousSolidsType.ToString());
                    break;

                case PolyhydraMainCategories.Radial:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(true);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, PreviewPolyhedron.m_Instance.RadialPolyType.ToString());
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
                    Slider1.Min = 3;
                    Slider1.Max = 16;
                    Slider1.SetDescriptionText("Sides");
                    Slider1.SliderType = SliderTypes.Int;

                    switch (PreviewPolyhedron.m_Instance.RadialPolyType)
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
                            Slider2.Min = .1f;
                            Slider2.Max = 4;
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
                            Slider2.Min = .1f;
                            Slider2.Max = 4;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider3.gameObject.SetActive(true);
                            Slider3.SetDescriptionText("Cap Height");
                            Slider3.Min = .1f;
                            Slider3.Max = 4;
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
                    Slider1.Min = 1;
                    Slider1.Max = 80;
                    Slider2.Min = 1;
                    Slider2.Max = 7;
                    Slider1.SliderType = SliderTypes.Int;
                    Slider2.SliderType = SliderTypes.Int;
                    break;

                case PolyhydraMainCategories.Grids:

                    Slider1.gameObject.SetActive(true);
                    Slider2.gameObject.SetActive(true);
                    Slider3.gameObject.SetActive(false);
                    Slider1.SetDescriptionText("Width");
                    Slider2.SetDescriptionText("Depth");
                    Slider1.Min = 1;
                    Slider1.Max = 16;
                    Slider2.Min = 1;
                    Slider2.Max = 16;
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
                            Slider1.Min = 3;
                            Slider1.Max = 16;
                            Slider1.SetDescriptionText("Sides");
                            break;
                        case OtherSolidsCategories.Star:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(false);
                            Slider1.SliderType = SliderTypes.Int;
                            Slider2.SliderType = SliderTypes.Float;
                            Slider1.Min = 3;
                            Slider1.Max = 16;
                            Slider2.Min = 0.01f;
                            Slider2.Max = 4f;
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
                            Slider1.Min = 1;
                            Slider1.Max = 16;
                            Slider2.Min = 1;
                            Slider2.Max = 16;
                            Slider3.Min = 1;
                            Slider3.Max = 16;
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
                            Slider1.Min = 1;
                            Slider1.Max = 16;
                            Slider2.Min = 1;
                            Slider2.Max = 16;
                            Slider1.SetDescriptionText("Sides");
                            Slider2.SetDescriptionText("Slices");
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
                            Slider1.Min = .1f;
                            Slider1.Max = 3f;
                            Slider2.Min = .1f;
                            Slider2.Max = 3f;
                            Slider3.Min = .1f;
                            Slider3.Max = 3f;
                            Slider1.SetDescriptionText("Size 1");
                            Slider2.SetDescriptionText("Size 2");
                            Slider3.SetDescriptionText("Size 3");
                            break;
                        default:
                            Slider1.gameObject.SetActive(false);
                            Slider2.gameObject.SetActive(false);
                            Slider3.gameObject.SetActive(false);
                            break;
                    }

                    break;
            }
            Slider1.UpdateValue(Slider1.GetCurrentValue());
            Slider2.UpdateValue(Slider2.GetCurrentValue());
        }

        public static void CreateWidgetForPolyhedron(PolyMesh poly, TrTransform tr)
        {
            var shapeType = EditableModelManager.CurrentModel.GeneratorType;
            EditableModelManager.m_Instance.GeneratePolyMesh(
                poly,
                tr,
                EditableModelManager.CurrentModel.ColorMethod,
                shapeType,
                EditableModelManager.CurrentModel.Colors,
                EditableModelManager.CurrentModel.GeneratorParameters,
                EditableModelManager.CurrentModel.Operations
            );
        }

        public void HandleSavePreset(bool overwrite)
        {
            if (string.IsNullOrEmpty(CurrentPresetPath))
            {
                CurrentPresetPath = Path.Combine(
                    DefaultPresetsDirectory(),
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
            // TODO deduplicate this logic
            ColorMethods colorMethod = ColorMethods.ByRole;
            if (PreviewPolyhedron.m_Instance.Operators.Any(o => o.opType == PolyMesh.Operation.AddTag))
            {
                colorMethod = ColorMethods.ByTags;
            }

            // TODO Refactor:
            // Required info shouldn't be split between PolyhydraPanel and PreviewPoly
            // There's too many different classes at play with overlapping responsibilities
            var em = new EditableModelManager.EditableModel(
                PreviewPolyhedron.m_Instance.m_PolyMesh,
                EditableModelManager.CurrentModel.Colors,
                colorMethod,
                EditableModelManager.CurrentModel.GeneratorType,
                EditableModelManager.CurrentModel.GeneratorParameters,
                EditableModelManager.CurrentModel.Operations
            );

            EditableModelDefinition emDef = MetadataUtils.GetEditableModelDefinition(em);
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
                case GeneratorTypes.Grid:
                    return $"ShapeButtons/poly_gridshape_{action}";
                case GeneratorTypes.Radial:
                    return $"ShapeButtons/poly_johnson_{action}";
                case GeneratorTypes.Uniform:
                    return $"ShapeButtons/poly_uniform_{action}";
                case GeneratorTypes.Shapes:
                case GeneratorTypes.Various:
                    return $"ShapeButtons/poly_other_{action}";
            }
            Debug.LogError($"Unsupported generator type: {mainType}");
            return null;
        }

        public string LabelFormatter(string text)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            text = textInfo.ToTitleCase(text.Replace("_", " "));
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
                    path = $"ShapeButtons/poly_grid_{label}";
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.OtherSolidsType:
                    path = GetButtonTexturePath(GeneratorTypes.Various, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.GridShape:
                    path = GetButtonTexturePath(GeneratorTypes.Grid, label);
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.OperatorType:
                    path = $"IconButtons/{label}";
                    return Resources.Load<Texture2D>(path);
                case PolyhydraButtonTypes.FilterType:
                    return null;
                case PolyhydraButtonTypes.Preset:
                    return null;
            }
            return null;
        }

        public void SetButtonTextAndIcon(PolyhydraButtonTypes buttonType, string label, string friendlyLabel = "")
        {
            if (string.IsNullOrEmpty(friendlyLabel)) friendlyLabel = label;

            switch (buttonType)
            {
                case PolyhydraButtonTypes.MainCategory:
                    ButtonMainCategory.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonMainCategory.SetDescriptionText($"Category: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.UniformType:
                    ButtonUniformType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonUniformType.SetDescriptionText($"Type: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.RadialType:
                    ButtonRadialType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonRadialType.SetDescriptionText($"Type: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.GridType:
                    ButtonGridType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridType.SetDescriptionText($"Grid Type: {LabelFormatter(friendlyLabel).Replace("_", "")}");
                    break;
                case PolyhydraButtonTypes.OtherSolidsType:
                    ButtonOtherSolidsType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOtherSolidsType.SetDescriptionText($"Type: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.GridShape:
                    ButtonGridShape.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridShape.SetDescriptionText($"Grid Shape: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.OperatorType:
                    ButtonOpType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpType.SetDescriptionText($"Operation: {LabelFormatter(friendlyLabel)}");
                    break;
                case PolyhydraButtonTypes.FilterType:
                    ButtonOpFilterType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpFilterType.SetDescriptionText($"Filter: {LabelFormatter(friendlyLabel)}");
                    break;
            }
        }

        public void LoadFromDefinition(EditableModelDefinition emd)
        {
            Color[] colors = emd.Colors ?? DefaultColorPalette;
            var polyMesh = new PolyMesh();
            var emodel = new EditableModelManager.EditableModel(
                polyMesh,
                (Color[])colors.Clone(),
                emd.ColorMethod,
                emd.GeneratorType,
                emd.GeneratorParameters,
                emd.Operations
            );
            LoadFromEditableModel(emodel);
        }

        public void LoadFromEditableModel(EditableModelManager.EditableModel emodel)
        {
            void setSlidersFromGeneratorParams(List<string> names)
            {
                if (names.Count == 0) return;
                var sliderParamValues = names.Select(n => Convert.ToSingle(emodel.GeneratorParameters[n])).ToList();

                Slider1.UpdateValueAbsolute(sliderParamValues[0]);
                if (sliderParamValues.Count == 1) return;

                Slider2.UpdateValueAbsolute(sliderParamValues[1]);
                if (sliderParamValues.Count == 2) return;

                Slider3.UpdateValueAbsolute(sliderParamValues[2]);
            }

            // If no colors are supplied then use the current palette
            Color[] colors;
            if (emodel.Colors == null || emodel.Colors.Length == 0)
            {
                colors = (Color[])EditableModelManager.CurrentModel.Colors.Clone();
            }
            else
            {
                colors = emodel.Colors;
            }

            PreviewPolyhedron.m_Instance.AssignColors(colors);
            EditableModelManager.CurrentModel.GeneratorType = emodel.GeneratorType;
            EditableModelManager.CurrentModel.GeneratorParameters = emodel.GeneratorParameters;
            EditableModelManager.CurrentModel.Operations = emodel.Operations;

            var sliderParamNames = new List<string>();

            // Set up generator UI to match preset

            // Widgets must be visible when setting textures
            ShowAllGeneratorControls();

            switch (emodel.GeneratorType)
            {
                case GeneratorTypes.FileSystem:
                case GeneratorTypes.GeometryData:
                case GeneratorTypes.ConwayString:
                case GeneratorTypes.Johnson:
                    Debug.LogError($"Preset has unsupported generator type: {emodel.GeneratorType}");
                    break;
                case GeneratorTypes.Grid:
                    m_CurrentMainCategory = PolyhydraMainCategories.Grids;
                    PreviewPolyhedron.m_Instance.GridType = (GridEnums.GridTypes)Convert.ToInt32(emodel.GeneratorParameters["type"]);
                    PreviewPolyhedron.m_Instance.GridShape = (GridEnums.GridShapes)Convert.ToInt32(emodel.GeneratorParameters["shape"]);
                    sliderParamNames = new List<string> { "x", "y" };
                    break;
                case GeneratorTypes.Shapes:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    PreviewPolyhedron.m_Instance.ShapeType = (ShapeTypes)Convert.ToInt32(emodel.GeneratorParameters["type"]);
                    switch (PreviewPolyhedron.m_Instance.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            m_OtherSolidsCategory = OtherSolidsCategories.Polygon;
                            PreviewPolyhedron.m_Instance.ShapeType = ShapeTypes.Polygon;
                            sliderParamNames = new List<string> { "sides" };
                            break;
                        case ShapeTypes.Star:
                            m_OtherSolidsCategory = OtherSolidsCategories.Star;
                            PreviewPolyhedron.m_Instance.ShapeType = ShapeTypes.Star;
                            sliderParamNames = new List<string> { "sides", "sharpness" };
                            break;
                        case ShapeTypes.L_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.L_Shape;
                            PreviewPolyhedron.m_Instance.ShapeType = ShapeTypes.L_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.C_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.C_Shape;
                            PreviewPolyhedron.m_Instance.ShapeType = ShapeTypes.C_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.H_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.H_Shape;
                            PreviewPolyhedron.m_Instance.ShapeType = ShapeTypes.H_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    PreviewPolyhedron.m_Instance.VariousSolidsType = (VariousSolidTypes)Convert.ToInt32(emodel.GeneratorParameters["type"]);
                    switch (PreviewPolyhedron.m_Instance.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            m_OtherSolidsCategory = OtherSolidsCategories.Box;
                            PreviewPolyhedron.m_Instance.VariousSolidsType = VariousSolidTypes.Box;
                            sliderParamNames = new List<string> { "x", "y", "z" };
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvHemisphere;
                            PreviewPolyhedron.m_Instance.VariousSolidsType = VariousSolidTypes.UvHemisphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                        case VariousSolidTypes.UvSphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvSphere;
                            PreviewPolyhedron.m_Instance.VariousSolidsType = VariousSolidTypes.UvSphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                    }
                    break;
                case GeneratorTypes.Radial:
                    m_CurrentMainCategory = PolyhydraMainCategories.Radial;
                    PreviewPolyhedron.m_Instance.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(emodel.GeneratorParameters["type"]);
                    sliderParamNames = new List<string> { "sides", "height", "capheight" };
                    break;
                case GeneratorTypes.Waterman:
                    m_CurrentMainCategory = PolyhydraMainCategories.Waterman;
                    sliderParamNames = new List<string> { "root", "c" };
                    break;
                case GeneratorTypes.Uniform:
                    int subtypeID = Convert.ToInt32(emodel.GeneratorParameters["type"]);
                    var uniformType = Uniform.Uniforms[subtypeID];
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
                    PreviewPolyhedron.m_Instance.UniformPolyType = (UniformTypes)subtypeID;
                    break;
            }

            SetMainButtonVisibility();
            SetSliderConfiguration();
            setSlidersFromGeneratorParams(sliderParamNames);

            // Set Op UI to match preset

            // Widgets must be visible when setting textures
            ShowAllOpControls();


            PreviewPolyhedron.m_Instance.Operators.Clear();

            // TODO This has some similarities to code in SaveLoadScript and PreviewPolyhedron
            foreach (var opDict in emodel.Operations)
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

                PreviewPolyhedron.m_Instance.Operators.Add(newOp);
                AddOpButton();
            }

            RefreshOpSelectButtons();
            HandleSelectOpButton(PreviewPolyhedron.m_Instance.Operators.Count - 1);

            ShowAllGeneratorControls();

            PreviewPolyhedron.m_Instance.RebuildPoly();
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
            var ops = PreviewPolyhedron.m_Instance.Operators;
            var op = ops[CurrentActiveOpIndex];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            OpConfig opConfig = OpConfigs.Configs[op.opType];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            op.amount = opConfig.amountDefault;
            op.amount2 = opConfig.amount2Default;
            ops[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.Operators = ops;

            RefreshOpSelectButtons();
            ConfigureOpPanel(op);
        }

        public void HandleSelectOpButton(int index)
        {
            CurrentActiveOpIndex = index;
            RefreshOpSelectButtons();
            if (PreviewPolyhedron.m_Instance.Operators.Count > 0)
            {
                var op = PreviewPolyhedron.m_Instance.Operators[index];
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
            ButtonOpDisable.SetToggleState(op.disabled);

            if (opConfig.usesAmount)
            {
                SliderOpParam1.gameObject.SetActive(true);
                SliderOpParam1.Min = opConfig.amountSafeMin;
                SliderOpParam1.Max = opConfig.amountSafeMax;
                SliderOpParam1.UpdateValueAbsolute(op.amount);
                SliderOpParam1.GetComponentInChildren<ActionToggleButton>().SetToggleState(op.amountRandomize);
            }
            else
            {
                SliderOpParam1.gameObject.SetActive(false);
            }

            if (opConfig.usesAmount2)
            {
                SliderOpParam2.gameObject.SetActive(true);
                SliderOpParam2.Min = opConfig.amount2SafeMin;
                SliderOpParam2.Max = opConfig.amount2SafeMax;
                SliderOpParam2.UpdateValueAbsolute(op.amount2);
                SliderOpParam2.GetComponentInChildren<ActionToggleButton>().SetToggleState(op.amount2Randomize);
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
                () => ColorPalletteButtons[index].SetColorSwatch(EditableModelManager.CurrentModel.Colors[index])
            );

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += c =>
            {
                SetFinalColor(c, index);
                PreviewPolyhedron.m_Instance.RebuildPoly();
            };

            // Init must be called after all popup.ColorPicked actions have been assigned.
            popup.ColorPicker.Controller.CurrentColor = EditableModelManager.CurrentModel.Colors[index];

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
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            return op.paramColor;
        }

        private void SetOpColor(Color color)
        {
            ButtonOpColorPicker.SetColorSwatch(color);
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            op.paramColor = color;
            PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex] = op;
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleSetAllColorsToCurrentButtonPressed()
        {
            SetAllFinalColors(PointerManager.m_Instance.PointerColor);
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleResetAllColorsToDefaultButtonPressed()
        {
            for (int index = 0; index < ColorPalletteButtons.Length; index++)
            {
                SetFinalColor(DefaultColorPalette[index], index);
            }
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void HandleJitterAllColorsButtonPressed()
        {
            for (var index = 0; index < ColorPalletteButtons.Length; index++)
            {
                Color currentColor = EditableModelManager.CurrentModel.Colors[index];
                Color newColor = PointerManager.m_Instance.CalculateJitteredColor(currentColor);
                SetFinalColor(newColor, index);
            }
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        private void SetAllFinalColors(Color color)
        {
            for (var index = 0; index < ColorPalletteButtons.Length; index++)
            {
                SetFinalColor(color, index);
            }
        }

        private void SetFinalColor(Color color, int index)
        {
            PolyhydraColorButton btn = ColorPalletteButtons[index];
            btn.SetColorSwatch(color);
            EditableModelManager.CurrentModel.Colors[index] = color;
        }

        public void HandleAddOpButton()
        {
            var newOp = new PreviewPolyhedron.OpDefinition
            {
                disabled = false,
                filterNot = false
            };
            AddOpButton();
            PreviewPolyhedron.m_Instance.Operators.Add(newOp);
            HandleSelectOpButton(PreviewPolyhedron.m_Instance.Operators.Count - 1);
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
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Shapes;
                    PreviewPolyhedron.m_Instance.ShapeType = (ShapeTypes)Enum.Parse(typeof(ShapeTypes), action);
                    break;
                case OtherSolidsCategories.UvSphere:
                case OtherSolidsCategories.UvHemisphere:
                case OtherSolidsCategories.Box:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Various;
                    PreviewPolyhedron.m_Instance.VariousSolidsType = (VariousSolidTypes)Enum.Parse(typeof(VariousSolidTypes), action);
                    break;
            }
            SetSliderConfiguration();
        }

        public void HandleMainCategoryButtonPress(PolyhydraMainCategories mainCategory)
        {
            m_CurrentMainCategory = mainCategory;
            SetMainButtonVisibility();
            SetSliderConfiguration();
            switch (m_CurrentMainCategory)
            {
                case PolyhydraMainCategories.Platonic:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Uniform;
                    PreviewPolyhedron.m_Instance.UniformPolyType = (UniformTypes)Uniform.Platonic[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, PreviewPolyhedron.m_Instance.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Archimedean:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Uniform;
                    PreviewPolyhedron.m_Instance.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, PreviewPolyhedron.m_Instance.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.KeplerPoinsot:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Uniform;
                    PreviewPolyhedron.m_Instance.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, PreviewPolyhedron.m_Instance.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Radial:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Radial;
                    PreviewPolyhedron.m_Instance.RadialPolyType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, PreviewPolyhedron.m_Instance.RadialPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Waterman:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case PolyhydraMainCategories.Grids:
                    EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Grid;
                    PreviewPolyhedron.m_Instance.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, PreviewPolyhedron.m_Instance.GridType.ToString());
                    PreviewPolyhedron.m_Instance.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, PreviewPolyhedron.m_Instance.GridShape.ToString());
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
                            EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Shapes;
                            PreviewPolyhedron.m_Instance.ShapeType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, PreviewPolyhedron.m_Instance.ShapeType.ToString());
                            break;
                        case OtherSolidsCategories.UvSphere:
                        case OtherSolidsCategories.UvHemisphere:
                        case OtherSolidsCategories.Box:
                            EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Various;
                            PreviewPolyhedron.m_Instance.VariousSolidsType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, PreviewPolyhedron.m_Instance.VariousSolidsType.ToString());
                            break;
                    }
                    break;
            }
        }

        public List<string> GetOtherSolidCategoryNames()
        {
            return Enum.GetNames(typeof(OtherSolidsCategories)).ToList();
        }

        public List<string> GetMainCategoryNames()
        {
            return Enum.GetNames(typeof(PolyhydraMainCategories)).ToList();
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
            ButtonOpFilterNot.SetToggleState(op.filterNot);
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
                    SliderOpFilterParam.Min = 0;
                    SliderOpFilterParam.Max = 10;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.OnlyNth:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 0;
                    SliderOpFilterParam.Max = 100;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.EveryNth:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 1;
                    SliderOpFilterParam.Max = 32;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.FirstN:
                case FilterTypes.LastN:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 1;
                    SliderOpFilterParam.Max = 100;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case FilterTypes.NSided:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 3;
                    SliderOpFilterParam.Max = 16;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;

                case FilterTypes.FacingUp:
                case FilterTypes.FacingForward:
                case FilterTypes.FacingRight:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 180f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.FacingVertical:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 90f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.Random:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 1f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.PositionX:
                case FilterTypes.PositionY:
                case FilterTypes.PositionZ:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = -5f;
                    SliderOpFilterParam.Max = 5f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case FilterTypes.DistanceFromCenter:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 10f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
            }
        }

        public void HandleOpDelete()
        {
            PreviewPolyhedron.m_Instance.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex = Mathf.Min(PreviewPolyhedron.m_Instance.Operators.Count - 1, CurrentActiveOpIndex);
            HandleSelectOpButton(CurrentActiveOpIndex);
            PreviewPolyhedron.m_Instance.RebuildPoly();
            RefreshOpSelectButtons();
        }

        [ContextMenu("Test RefreshOpSelectButtons")]
        private void RefreshOpSelectButtons()
        {
            OpPanel.gameObject.SetActive(PreviewPolyhedron.m_Instance.Operators.Count > 0);
            OperatorSelectPopupTools.gameObject.SetActive(PreviewPolyhedron.m_Instance.Operators.Count > 0);

            var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();

            for (var i = 0; i < btns.Length; i++)
            {
                var btn = btns[i];
                var btnParent = btn.transform.parent;
                if (i > PreviewPolyhedron.m_Instance.Operators.Count - 1)
                {
                    Destroy(btnParent.gameObject);
                    continue;
                }
                var op = PreviewPolyhedron.m_Instance.Operators[i];
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
                    ToolBtnNext.gameObject.SetActive(i < PreviewPolyhedron.m_Instance.Operators.Count - 1);
                    FriendlyOpLabels.TryGetValue(opName, out string friendlyLabel);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, opName, friendlyLabel);
                }

            }
            if (gameObject.activeSelf)
            {
                AnimateOpParentIntoPlace();
            }
        }

        public void HandleOpMove(int delta)
        {
            var op = PreviewPolyhedron.m_Instance.Operators[CurrentActiveOpIndex];
            PreviewPolyhedron.m_Instance.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex += delta;
            PreviewPolyhedron.m_Instance.Operators.Insert(CurrentActiveOpIndex, op);
            HandleSelectOpButton(CurrentActiveOpIndex);
            PreviewPolyhedron.m_Instance.RebuildPoly();
            RefreshOpSelectButtons();
        }
    }

} // namespace TiltBrush
