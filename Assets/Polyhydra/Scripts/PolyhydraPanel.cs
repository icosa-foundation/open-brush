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
using UnityEngine.Serialization;
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
        [NonSerialized] public PreviewPolyhedron CurrentPolyhedra;

        public Camera m_ThumbnailCamera;
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

        public GameObject PreviewPolyParent;
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
        public static Dictionary<string, object> m_GeneratorParameters;
        public static List<Dictionary<string, object>> m_Operations;
        [SerializeField] private string m_CurrentPresetPath;

        public float previewRotationX, previewRotationY, previewRotationZ = .5f;

        private MeshFilter meshFilter;

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

        public override void InitPanel()
        {
            base.InitPanel();
            ShowAllGeneratorControls();
            OpFilterControlParent.SetActive(false);
            OpPanel.SetActive(false);
            CurrentPolyhedra = gameObject.GetComponentInChildren<PreviewPolyhedron>(true);
            SetSliderConfiguration();
            SetMainButtonVisibility();
            SetPresetSaveButtonState(popupButtonEnabled: false);
            if (!Directory.Exists(DefaultPresetsDirectory()))
            {
                Directory.CreateDirectory(DefaultPresetsDirectory());
            }

            for (var i = 0; i < ColorPalletteButtons.Length; i++)
            {
                ColorPalletteButtons[i].SetColorSwatch(CurrentPolyhedra.ColorPalette[i]);
            }
        }

        private void SetPresetSaveButtonState(bool popupButtonEnabled)
        {
            PresetInitialSaveButton.SetActive(!popupButtonEnabled);
            PresetSaveOptionsPopupButton.SetActive(popupButtonEnabled);
        }

        public void HandleSlider1(Vector3 value)
        {
            CurrentPolyhedra.Param1Int = Mathf.FloorToInt(value.z);
            CurrentPolyhedra.Param1Float = value.z;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleSlider2(Vector3 value)
        {
            CurrentPolyhedra.Param2Int = Mathf.FloorToInt(value.z);
            CurrentPolyhedra.Param2Float = value.z;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleSlider3(Vector3 value)
        {
            CurrentPolyhedra.Param3Int = Mathf.FloorToInt(value.z);
            CurrentPolyhedra.Param3Float = value.z;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleOpAmountSlider(Vector3 value)
        {
            int paramIndex = (int)value.y;
            float amount = value.z;
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amount = amount;
                    break;
                case 1:
                    op.amount2 = amount;
                    break;
            }
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleOpDisableButton()
        {
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            op.disabled = !op.disabled;
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            RefreshOpSelectButtons();
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleOpRandomizeButton(int paramIndex)
        {
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amountRandomize = !op.amountRandomize;
                    break;
                case 1:
                    op.amount2Randomize = !op.amount2Randomize;
                    break;
            }
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
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
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            op.filterParamInt = Mathf.FloorToInt(value.z);
            op.filterParamFloat = value.z;
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleButtonOpNot()
        {
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            op.filterNot = !op.filterNot;
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
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
            CurrentPolyhedra.transform.parent.Rotate(previewRotationX, previewRotationY, previewRotationZ);
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
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, CurrentPolyhedra.UniformPolyType.ToString());
                    break;

                case PolyhydraMainCategories.Grids:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(true);
                    ButtonGridShape.gameObject.SetActive(true);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, CurrentPolyhedra.GridType.ToString());
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, CurrentPolyhedra.GridShape.ToString());
                    break;

                case PolyhydraMainCategories.Various:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(true);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, CurrentPolyhedra.VariousSolidsType.ToString());
                    break;

                case PolyhydraMainCategories.Radial:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(true);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, CurrentPolyhedra.RadialPolyType.ToString());
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

                    switch (CurrentPolyhedra.RadialPolyType)
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

        public static void CreateWidgetForPolyhedron(Vector3 position_CS, Quaternion rotation_CS, float scale_CS, PolyMesh poly)
        {
            var creationTr = TrTransform.TRS(
                position_CS,
                rotation_CS,
                scale_CS
            );
            var shapeType = EditableModelManager.m_Instance.m_PreviewPolyhedron.GeneratorType;
            EditableModelManager.m_Instance.GeneratePolyMesh(
                poly,
                creationTr,
                EditableModelManager.m_Instance.m_PreviewPolyhedron.ColorMethod,
                shapeType,
                EditableModelManager.m_Instance.m_PreviewPolyhedron.ColorPalette,
                m_GeneratorParameters, m_Operations
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
            SetPresetSaveButtonState(popupButtonEnabled: true);
        }

        public void HandleDuplicatePreset()
        {
            SetPresetSaveButtonState(popupButtonEnabled: false);
        }

        void SavePresetJson(string presetPath)
        {
            // TODO deduplicate this logic
            ColorMethods colorMethod = ColorMethods.ByRole;
            if (CurrentPolyhedra.Operators.Any(o => o.opType == PolyMesh.Operation.AddTag))
            {
                colorMethod = ColorMethods.ByTags;
            }

            // TODO Refactor:
            // Required info shouldn't be split between PolyhydraPanel and PreviewPoly
            // There's too many different classes at play with overlapping responsibilities
            var em = new EditableModelManager.EditableModel(
                CurrentPolyhedra.m_PolyMesh,
                CurrentPolyhedra.ColorPalette,
                colorMethod,
                CurrentPolyhedra.GeneratorType,
                m_GeneratorParameters,
                m_Operations
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

        public void HandleLoadPreset(string path)
        {
            var jsonDeserializer = new JsonSerializer();
            jsonDeserializer.ContractResolver = new CustomJsonContractResolver();
            EditableModelDefinition emd;
            using (var textReader = new StreamReader(path))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                emd = jsonDeserializer.Deserialize<EditableModelDefinition>(jsonReader);
            }
            LoadFromDefinition(emd);
            CurrentPresetPath = path;
            SetPresetSaveButtonState(popupButtonEnabled: true);
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

            CurrentPolyhedra.AssignColors(emd.Colors);
            CurrentPolyhedra.GeneratorType = emd.GeneratorType;
            m_GeneratorParameters = emd.GeneratorParameters;
            m_Operations = emd.Operations;

            if (false) // Disable WIP color loading code.
            {
                CustomColorPaletteStorage.m_Instance.ClearAllColors();
                var palette = new Palette();
                byte floatToByte(float v)
                {
                    return (byte)Mathf.FloorToInt(v * 255);
                }
                palette.Colors = emd.Colors.Select(c => new Color32(
                    floatToByte(c.r),
                    floatToByte(c.g),
                    floatToByte(c.b),
                    0)
                ).ToArray();
                CustomColorPaletteStorage.m_Instance.SetColorsFromPalette(palette);
                CustomColorPaletteStorage.m_Instance.RefreshStoredColors();
            }

            var sliderParamNames = new List<string>();

            // Set up generator UI to match preset

            // Widgets must be visible when setting textures
            ShowAllGeneratorControls();

            switch (emd.GeneratorType)
            {
                case GeneratorTypes.FileSystem:
                case GeneratorTypes.GeometryData:
                case GeneratorTypes.ConwayString:
                case GeneratorTypes.Johnson:
                    Debug.LogError($"Preset has unsupported generator type: {emd.GeneratorType}");
                    break;
                case GeneratorTypes.Grid:
                    m_CurrentMainCategory = PolyhydraMainCategories.Grids;
                    CurrentPolyhedra.GridType = (GridEnums.GridTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    CurrentPolyhedra.GridShape = (GridEnums.GridShapes)Convert.ToInt32(emd.GeneratorParameters["shape"]);
                    sliderParamNames = new List<string> { "x", "y" };
                    break;
                case GeneratorTypes.Shapes:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    CurrentPolyhedra.ShapeType = (ShapeTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    switch (CurrentPolyhedra.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            m_OtherSolidsCategory = OtherSolidsCategories.Polygon;
                            CurrentPolyhedra.ShapeType = ShapeTypes.Polygon;
                            sliderParamNames = new List<string> { "sides" };
                            break;
                        case ShapeTypes.Star:
                            m_OtherSolidsCategory = OtherSolidsCategories.Star;
                            CurrentPolyhedra.ShapeType = ShapeTypes.Star;
                            sliderParamNames = new List<string> { "sides", "sharpness" };
                            break;
                        case ShapeTypes.L_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.L_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.L_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.C_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.C_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.C_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                        case ShapeTypes.H_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.H_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.H_Shape;
                            sliderParamNames = new List<string> { "a", "b", "c" };
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    CurrentPolyhedra.VariousSolidsType = (VariousSolidTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    switch (CurrentPolyhedra.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            m_OtherSolidsCategory = OtherSolidsCategories.Box;
                            CurrentPolyhedra.VariousSolidsType = VariousSolidTypes.Box;
                            sliderParamNames = new List<string> { "x", "y", "z" };
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvHemisphere;
                            CurrentPolyhedra.VariousSolidsType = VariousSolidTypes.UvHemisphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                        case VariousSolidTypes.UvSphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvSphere;
                            CurrentPolyhedra.VariousSolidsType = VariousSolidTypes.UvSphere;
                            sliderParamNames = new List<string> { "x", "y" };
                            break;
                    }
                    break;
                case GeneratorTypes.Radial:
                    m_CurrentMainCategory = PolyhydraMainCategories.Radial;
                    CurrentPolyhedra.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    sliderParamNames = new List<string> { "sides", "height", "capheight" };
                    break;
                case GeneratorTypes.Waterman:
                    m_CurrentMainCategory = PolyhydraMainCategories.Waterman;
                    sliderParamNames = new List<string> { "root", "c" };
                    break;
                case GeneratorTypes.Uniform:
                    int subtypeID = Convert.ToInt32(emd.GeneratorParameters["type"]);
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
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)subtypeID;
                    break;
            }

            SetMainButtonVisibility();
            SetSliderConfiguration();
            setSlidersFromGeneratorParams(sliderParamNames);

            // Set Op UI to match preset

            // Widgets must be visible when setting textures
            ShowAllOpControls();


            CurrentPolyhedra.Operators.Clear();

            // TODO This has some similarities to code in SaveLoadScript and PreviewPolyhedron
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
                    var colorData = (opDict["paramColor"] as JArray);
                    newOp.paramColor = new Color(
                        colorData[0].Value<float>(),
                        colorData[1].Value<float>(),
                        colorData[2].Value<float>()
                    );
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

                CurrentPolyhedra.Operators.Add(newOp);
                AddOpButton();
            }

            RefreshOpSelectButtons();
            HandleSelectOpButton(CurrentPolyhedra.Operators.Count - 1);

            ShowAllGeneratorControls();

            CurrentPolyhedra.RebuildPoly();
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
            m_ThumbnailCamera.enabled = true;
            m_ThumbnailCamera.gameObject.SetActive(true);
            FocusCameraOnGameObject(m_ThumbnailCamera, PreviewPolyParent, 0.5f, true);
            RenderTexture activeRenderTexture = RenderTexture.active;
            var tex = new RenderTexture(256, 256, 32);
            m_ThumbnailCamera.targetTexture = tex;
            RenderTexture.active = m_ThumbnailCamera.targetTexture;
            m_ThumbnailCamera.Render();
            Texture2D image = new Texture2D(tex.width, tex.height);
            image.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            image.Apply();
            RenderTexture.active = activeRenderTexture;
            byte[] bytes = image.EncodeToPNG();
            Destroy(image);
            File.WriteAllBytes(presetThumbnailPath, bytes);
            m_ThumbnailCamera.gameObject.SetActive(false);
            m_ThumbnailCamera.enabled = false;
        }



        public void MonoscopicAddPolyhedron()
        {
            // Used in mono mode and during testing

            var poly = EditableModelManager.m_Instance.m_PreviewPolyhedron.m_PolyMesh;

            CreateWidgetForPolyhedron(
                new Vector3(Random.value * 3 - 1.5f, Random.value * 7 + 7, Random.value * 8 + 2),
                Quaternion.identity,
                1f,
                poly
            );
        }

        public void ChangeCurrentOpType(string operationName)
        {
            var ops = CurrentPolyhedra.Operators;
            var op = ops[CurrentActiveOpIndex];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            OpConfig opConfig = OpConfigs.Configs[op.opType];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            op.amount = opConfig.amountDefault;
            op.amount2 = opConfig.amount2Default;
            ops[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.Operators = ops;

            RefreshOpSelectButtons();
            ConfigureOpPanel(op);
        }

        public void HandleSelectOpButton(int index)
        {
            CurrentActiveOpIndex = index;
            RefreshOpSelectButtons();
            if (CurrentPolyhedra.Operators.Count > 0)
            {
                var op = CurrentPolyhedra.Operators[index];
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
                () => ColorPalletteButtons[index].SetColorSwatch(CurrentPolyhedra.ColorPalette[index])
            );

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += c => SetFinalColor(c, index);

            // Init must be called after all popup.ColorPicked actions have been assigned.
            popup.ColorPicker.Controller.CurrentColor = CurrentPolyhedra.ColorPalette[index];

            m_EatInput = true;
        }

        public void OpColorButtonPressed(int index)
        {
            // Create the popup with callback.
            //CreatePopUp(SketchControlsScript.GlobalCommands.LightingLdr, -1, -1, popupText, OnPopUpClose);
            SketchControlsScript.GlobalCommands command = SketchControlsScript.GlobalCommands.PolyhydraColorPickerPopup;
            CreatePopUp(command, -1, -1, "Color",
                () => ButtonOpColorPicker.SetDescriptionText(
                    "Color",
                    ColorTable.m_Instance.NearestColorTo(GetOpColor())
                )
            );

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += delegate (Color c)
            {
                ButtonOpColorPicker.SetDescriptionText("Color", ColorTable.m_Instance.NearestColorTo(c));
                SetOpColor(c);
            };

            // Init must be called after all popup.ColorPicked actions have been assigned.
            popup.ColorPicker.Controller.CurrentColor = GetOpColor();

            m_EatInput = true;
        }

        private Color GetOpColor()
        {
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            return op.paramColor;
        }

        private void SetOpColor(Color color)
        {
            ButtonOpColorPicker.SetDescriptionText("Color", ColorTable.m_Instance.NearestColorTo(color));
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            op.paramColor = color;
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
        }

        private void SetFinalColor(Color color, int index)
        {
            ButtonOpColorPicker.SetDescriptionText("Color", ColorTable.m_Instance.NearestColorTo(color));
            CurrentPolyhedra.ColorPalette[index] = color;
            CurrentPolyhedra.RebuildPoly();
        }

        public void HandleAddOpButton()
        {
            var newOp = new PreviewPolyhedron.OpDefinition
            {
                disabled = false,
                filterNot = false
            };
            AddOpButton();
            CurrentPolyhedra.Operators.Add(newOp);
            HandleSelectOpButton(CurrentPolyhedra.Operators.Count - 1);
        }

        public void AddOpButton()
        {
            Transform btnTr = Instantiate(OperatorSelectButtonPrefab, OperatorSelectButtonParent, false);
            btnTr.gameObject.SetActive(true);
        }


        public void AddGuideForCurrentPolyhedron()
        {
            // TODO Find a better way to pick a location;
            var tr = TrTransform.T(new Vector3(
                Random.value * 3 - 1.5f,
                Random.value * 7 + 7,
                Random.value * 8 + 2)
            );
            var poly = EditableModelManager.m_Instance.m_PreviewPolyhedron.m_PolyMesh;
            EditableModelManager.AddCustomGuide(poly, tr);
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
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Shapes;
                    CurrentPolyhedra.ShapeType = (ShapeTypes)Enum.Parse(typeof(ShapeTypes), action);
                    break;
                case OtherSolidsCategories.UvSphere:
                case OtherSolidsCategories.UvHemisphere:
                case OtherSolidsCategories.Box:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Various;
                    CurrentPolyhedra.VariousSolidsType = (VariousSolidTypes)Enum.Parse(typeof(VariousSolidTypes), action);
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
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.Platonic[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, CurrentPolyhedra.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Archimedean:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, CurrentPolyhedra.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.KeplerPoinsot:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, CurrentPolyhedra.UniformPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Radial:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Radial;
                    CurrentPolyhedra.RadialPolyType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.RadialType, CurrentPolyhedra.RadialPolyType.ToString());
                    break;
                case PolyhydraMainCategories.Waterman:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case PolyhydraMainCategories.Grids:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Grid;
                    CurrentPolyhedra.GridType = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridType, CurrentPolyhedra.GridType.ToString());
                    CurrentPolyhedra.GridShape = 0;
                    SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, CurrentPolyhedra.GridShape.ToString());
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
                            CurrentPolyhedra.GeneratorType = GeneratorTypes.Shapes;
                            CurrentPolyhedra.ShapeType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, CurrentPolyhedra.ShapeType.ToString());
                            break;
                        case OtherSolidsCategories.UvSphere:
                        case OtherSolidsCategories.UvHemisphere:
                        case OtherSolidsCategories.Box:
                            CurrentPolyhedra.GeneratorType = GeneratorTypes.Various;
                            CurrentPolyhedra.VariousSolidsType = 0;
                            SetButtonTextAndIcon(PolyhydraButtonTypes.OtherSolidsType, CurrentPolyhedra.VariousSolidsType.ToString());
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
            CurrentPolyhedra.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex = Mathf.Min(CurrentPolyhedra.Operators.Count - 1, CurrentActiveOpIndex);
            HandleSelectOpButton(CurrentActiveOpIndex);
            CurrentPolyhedra.RebuildPoly();
            RefreshOpSelectButtons();
        }

        [ContextMenu("Test RefreshOpSelectButtons")]
        private void RefreshOpSelectButtons()
        {
            OpPanel.gameObject.SetActive(CurrentPolyhedra.Operators.Count > 0);
            OperatorSelectPopupTools.gameObject.SetActive(CurrentPolyhedra.Operators.Count > 0);

            var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();

            for (var i = 0; i < btns.Length; i++)
            {
                var btn = btns[i];
                var btnParent = btn.transform.parent;
                if (i > CurrentPolyhedra.Operators.Count - 1)
                {
                    Destroy(btnParent.gameObject);
                    continue;
                }
                var op = CurrentPolyhedra.Operators[i];
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
                    ToolBtnNext.gameObject.SetActive(i < CurrentPolyhedra.Operators.Count - 1);
                    FriendlyOpLabels.TryGetValue(opName, out string friendlyLabel);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, opName, friendlyLabel);
                }

            }
            AnimateOpParentIntoPlace();
        }

        public void HandleOpMove(int delta)
        {
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            CurrentPolyhedra.Operators.RemoveAt(CurrentActiveOpIndex);
            CurrentActiveOpIndex += delta;
            CurrentPolyhedra.Operators.Insert(CurrentActiveOpIndex, op);
            HandleSelectOpButton(CurrentActiveOpIndex);
            CurrentPolyhedra.RebuildPoly();
            RefreshOpSelectButtons();
        }
    }

} // namespace TiltBrush
