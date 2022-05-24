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
        public GameObject OpPanel;
        public PolyhydraOptionButton ButtonOpType;
        public PolyhydraSlider SliderOpParam1;
        public PolyhydraSlider SliderOpParam2;
        public PolyhydraColorButton ButtonOpColorPicker;
        public GameObject OpFilterControlParent;
        public PolyhydraOptionButton ButtonOpFilterType;
        public PolyhydraSlider SliderOpFilterParam;
        public TextMeshPro LabelOpFilterName;
        public ActionButton ButtonOpFilterNot;

        public List<GameObject> MonoscopicOnlyButtons;

        public int CurrentActiveOpIndex;
        public Transform OperatorSelectButtonParent;
        public Transform OperatorSelectButtonPrefab;
        public Transform OperatorSelectPopupTools;
        public PolyhydraOpPopupToolsButton ToolBtnPrev;
        public PolyhydraOpPopupToolsButton ToolBtnNext;
        public static Dictionary<string, object> m_GeneratorParameters;
        public static List<Dictionary<string, object>> m_Operations;
        [NonSerialized] public string m_PresetsPath;

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
        
        override public void InitPanel()
        {
            base.InitPanel();
            OpFilterControlParent.SetActive(false);
            OpPanel.SetActive(false);
            CurrentPolyhedra = gameObject.GetComponentInChildren<PreviewPolyhedron>(true);
            SetSliderConfiguration();
            SetMainButtonVisibility();
            m_PresetsPath = Path.Combine(App.UserPath(), "Media Library/Shape Recipes/");
            if (!Directory.Exists(m_PresetsPath))
            {
                Directory.CreateDirectory(m_PresetsPath);
            }

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
            CurrentPolyhedra.RebuildPoly();
        }

        public void ShowAllGeneratorControls()
        {
            AllGeneratorControls.SetActive(true);
            AllOpControls.SetActive(false);
        }
        
        public void ShowAllOpControls()
        {
            AllGeneratorControls.SetActive(false);
            AllOpControls.SetActive(true);
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
            // float targetX = CurrentPolyhedra.Operators.Count > 4 ? (-CurrentActiveOpIndex - 0.5f) * .2f : -0.6f;
            float targetX = (-CurrentActiveOpIndex - 0.75f) * .2f;
            StartCoroutine(DoOpParentAnim(targetX));
        }

        IEnumerator DoOpParentAnim(float targetX)
        {
            void RescaleButtons()
            {
                var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();
                bool overflow = false;
                for (var i = 0; i < btns.Length; i++)
                {
                    var btn = btns[i];
                    var x = targetX + btn.transform.localPosition.x;
                    float scale;
                    if (x < -0.8f || x > 0.9f)
                    {
                        scale = 0;
                        overflow = true;
                    }
                    else
                    {
                        scale = 0.2f;
                    }
                    btn.transform.localScale = Vector3.one * scale;
                }
                if (overflow)
                {
                    // TODO some visual indicator that there's overflow
                }
            }
            
            RescaleButtons();
            
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
        }

        void Update()
        {
            BaseUpdate();
            CurrentPolyhedra.transform.parent.Rotate(previewRotationX, previewRotationY, previewRotationZ);
        }

        public void SetInitialUniform()
        {
            // Assign the correct button texture for each category
            Uniform initialUniformType;
            if (m_CurrentMainCategory == PolyhydraMainCategories.Platonic)
            {
                initialUniformType = Uniform.Platonic[0];
            }
            else if (m_CurrentMainCategory == PolyhydraMainCategories.Archimedean)
            {
                initialUniformType = Uniform.Archimedean[0];
            }
            else
            {
                initialUniformType = Uniform.KeplerPoinsot[0];
            }
            SetButtonTextAndIcon(PolyhydraButtonTypes.UniformType, initialUniformType.Name);
        }

        public void SetMainButtonVisibility()
        {
            SetButtonTextAndIcon(PolyhydraButtonTypes.MainCategory, m_CurrentMainCategory.ToString());

            foreach (var go in MonoscopicOnlyButtons)
            {
                go.SetActive(App.Config.m_SdkMode==SdkMode.Monoscopic);
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
                EditableModelManager.m_Instance.m_PreviewPolyhedron.PreviewColorMethod,
                shapeType, 
                EditableModelManager.m_Instance.m_PreviewPolyhedron.previewColors,
                m_GeneratorParameters, m_Operations
            );
        }

        public void SavePreset()
        {
            var filename = Guid.NewGuid().ToString().Substring(0, 8);
            SavePresetToFile(m_PresetsPath, filename);
            RenderToImageFile(m_PresetsPath, filename);
        }

        void SavePresetToFile(string path, string filename)
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
                CurrentPolyhedra.previewColors,
                colorMethod,
                CurrentPolyhedra.GeneratorType,
                m_GeneratorParameters,
                m_Operations
            );
            
            EditableModelDefinition emDef = MetadataUtils.GetEditableModelDefinition(em);
            var jsonSerializer = new JsonSerializer();
            jsonSerializer.ContractResolver = new CustomJsonContractResolver();

            using (var textWriter = new StreamWriter(path + $"{filename}.json"))
            using (var jsonWriter = new CustomJsonWriter(textWriter))
            {
                jsonSerializer.Serialize(jsonWriter, emDef);
            }
        }

        public void LoadPresetFromFile(string path)
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
        
        public void SetButtonTextAndIcon(PolyhydraButtonTypes buttonType, string label)
        {
            switch (buttonType)
            {
                case PolyhydraButtonTypes.MainCategory:
                    ButtonMainCategory.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonMainCategory.SetDescriptionText($"Category: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.UniformType:
                    ButtonUniformType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonUniformType.SetDescriptionText($"Type: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.RadialType:
                    ButtonRadialType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonRadialType.SetDescriptionText($"Type: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.GridType:
                    ButtonGridType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridType.SetDescriptionText($"Grid Type: {LabelFormatter(label).Replace("_", "")}");
                    break;
                case PolyhydraButtonTypes.OtherSolidsType:
                    ButtonOtherSolidsType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOtherSolidsType.SetDescriptionText($"Type: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.GridShape:
                    ButtonGridShape.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonGridShape.SetDescriptionText($"Grid Shape: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.OperatorType:
                    ButtonOpType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpType.SetDescriptionText($"Operation: {LabelFormatter(label)}");
                    break;
                case PolyhydraButtonTypes.FilterType:
                    ButtonOpFilterType.SetButtonTexture(GetButtonTexture(buttonType, label));
                    ButtonOpFilterType.SetDescriptionText($"Filter: {LabelFormatter(label)}");
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

            CurrentPolyhedra.previewColors = emd.Colors;
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
                palette.Colors = emd.Colors.Select(c=>new Color32(
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
                    sliderParamNames = new List<string>{"x", "y"};
                    break;
                case GeneratorTypes.Shapes:
                    m_CurrentMainCategory = PolyhydraMainCategories.Various;
                    CurrentPolyhedra.ShapeType = (ShapeTypes)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    switch (CurrentPolyhedra.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            m_OtherSolidsCategory = OtherSolidsCategories.Polygon;
                            CurrentPolyhedra.ShapeType = ShapeTypes.Polygon;
                            sliderParamNames = new List<string>{"sides"};
                            break;
                        case ShapeTypes.Star:
                            m_OtherSolidsCategory = OtherSolidsCategories.Star;
                            CurrentPolyhedra.ShapeType = ShapeTypes.Star;
                            sliderParamNames = new List<string>{"sides", "sharpness"};
                            break;
                        case ShapeTypes.L_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.L_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.L_Shape;
                            sliderParamNames = new List<string>{"a", "b", "c"};
                            break;
                        case ShapeTypes.C_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.C_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.C_Shape;
                            sliderParamNames = new List<string>{"a", "b", "c"};
                            break;
                        case ShapeTypes.H_Shape:
                            m_OtherSolidsCategory = OtherSolidsCategories.H_Shape;
                            CurrentPolyhedra.ShapeType = ShapeTypes.H_Shape;
                            sliderParamNames = new List<string>{"a", "b", "c"};
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
                            sliderParamNames = new List<string>{"x", "y", "z"};
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvHemisphere;
                            CurrentPolyhedra.VariousSolidsType = VariousSolidTypes.UvHemisphere;
                            sliderParamNames = new List<string>{"x", "y"};
                            break;
                        case VariousSolidTypes.UvSphere:
                            m_OtherSolidsCategory = OtherSolidsCategories.UvSphere;
                            CurrentPolyhedra.VariousSolidsType = VariousSolidTypes.UvSphere;
                            sliderParamNames = new List<string>{"x", "y"};
                            break;
                    }
                    break;
                case GeneratorTypes.Radial:
                    m_CurrentMainCategory = PolyhydraMainCategories.Radial;
                    CurrentPolyhedra.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(emd.GeneratorParameters["type"]);
                    sliderParamNames = new List<string>{"sides", "height", "capheight"};
                    break;
                case GeneratorTypes.Waterman:
                    m_CurrentMainCategory = PolyhydraMainCategories.Waterman;
                    sliderParamNames = new List<string>{"root", "c"};
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
                        amount2 = Convert.ToSingle(opDict["param2"])
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
                    newOp.filterType = (PreviewPolyhedron.AvailableFilters)Convert.ToInt32(filterType);
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

        void RenderToImageFile(string path, string filename)
        {
            var cam = gameObject.GetComponentInChildren<Camera>(true);
            cam.gameObject.SetActive(true);
            RenderTexture activeRenderTexture = RenderTexture.active;
            var tex = new RenderTexture(256, 256, 32);
            cam.targetTexture = tex;
            RenderTexture.active = cam.targetTexture;
            cam.Render();
            Texture2D image = new Texture2D(tex.width, tex.height);
            image.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            image.Apply();
            RenderTexture.active = activeRenderTexture;
            byte[] bytes = image.EncodeToPNG();
            Destroy(image);
            File.WriteAllBytes(path + $"{filename}.png", bytes);
            cam.gameObject.SetActive(false);
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

            if (opConfig.usesAmount)
            {
                SliderOpParam1.gameObject.SetActive(true);
                SliderOpParam1.Min = opConfig.amountSafeMin;
                SliderOpParam1.Max = opConfig.amountSafeMax;
                SliderOpParam1.UpdateValueAbsolute(op.amount);
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

        public void OpColorButtonPressed()
        {
            // Create the popup with callback.
            SketchControlsScript.GlobalCommands command = SketchControlsScript.GlobalCommands.PolyhydraColorPickerPopup;
            CreatePopUp(command, -1, -1, "Color", MakeOnOpColorPopUpClose());

            var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
            popup.transform.localPosition += new Vector3(0, 0, 0);
            popup.ColorPicker.ColorPicked += OnColorPicked();
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
            var op = CurrentPolyhedra.Operators[CurrentActiveOpIndex];
            op.paramColor = color;
            CurrentPolyhedra.Operators[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.RebuildPoly();
        }

        Action MakeOnOpColorPopUpClose()
        {
            return delegate
            {
                ButtonOpColorPicker.SetDescriptionText("Color", ColorTable.m_Instance.NearestColorTo(GetOpColor()));
            };
        }
        
        Action<Color> OnColorPicked()
        {
            return delegate (Color c)
            {
                SetOpColor(c);
           };
        }

        public void HandleAddOpButton()
        {
            var newOp = new PreviewPolyhedron.OpDefinition();
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
                    break;
                case PolyhydraMainCategories.Archimedean:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    break;
                case PolyhydraMainCategories.KeplerPoinsot:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    break;
                case PolyhydraMainCategories.Radial:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Radial;
                    break;
                case PolyhydraMainCategories.Waterman:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case PolyhydraMainCategories.Grids:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Grid;
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
                                break;
                            case OtherSolidsCategories.UvSphere:
                            case OtherSolidsCategories.UvHemisphere:
                            case OtherSolidsCategories.Box:
                                CurrentPolyhedra.GeneratorType = GeneratorTypes.Various;
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
            LabelOpFilterName.text = opFilterName;
            
            switch (op.filterType)
            {
                case PreviewPolyhedron.AvailableFilters.All:
                case PreviewPolyhedron.AvailableFilters.Inner:
                case PreviewPolyhedron.AvailableFilters.EvenSided:
                    SliderOpFilterParam.gameObject.SetActive(false);
                    break;
                case PreviewPolyhedron.AvailableFilters.Role:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 0;
                    SliderOpFilterParam.Max = 10;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case PreviewPolyhedron.AvailableFilters.Only:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 0;
                    SliderOpFilterParam.Max = 100;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case PreviewPolyhedron.AvailableFilters.EveryNth:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 1;
                    SliderOpFilterParam.Max = 32;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case PreviewPolyhedron.AvailableFilters.LastN:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 1;
                    SliderOpFilterParam.Max = 100;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case PreviewPolyhedron.AvailableFilters.NSided:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 3;
                    SliderOpFilterParam.Max = 16;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                
                case PreviewPolyhedron.AvailableFilters.FacingUp:
                case PreviewPolyhedron.AvailableFilters.FacingForward:
                case PreviewPolyhedron.AvailableFilters.FacingRight:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 180f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case PreviewPolyhedron.AvailableFilters.FacingHorizontal:
                case PreviewPolyhedron.AvailableFilters.FacingVertical:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 90f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case PreviewPolyhedron.AvailableFilters.Random:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 1f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case PreviewPolyhedron.AvailableFilters.PositionX:
                case PreviewPolyhedron.AvailableFilters.PositionY:
                case PreviewPolyhedron.AvailableFilters.PositionZ:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = -5f;
                    SliderOpFilterParam.Max = 5f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case PreviewPolyhedron.AvailableFilters.DistanceFromCenter:
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
        
        private void RefreshOpSelectButtons()
        {
            OpPanel.gameObject.SetActive(CurrentPolyhedra.Operators.Count > 0);
            OperatorSelectPopupTools.gameObject.SetActive(CurrentPolyhedra.Operators.Count > 0);

            var btns = OperatorSelectButtonParent.GetComponentsInChildren<PolyhydraSelectOpButton>();

            for (var i = 0; i < btns.Length; i++)
            {
                var btn = btns[i];
                if (i > CurrentPolyhedra.Operators.Count - 1)
                {
                    Destroy(btn.gameObject);
                    continue;
                }
                var op = CurrentPolyhedra.Operators[i];
                btn.OpIndex = i;
                string opName = op.opType.ToString();
                btn.SetDescriptionText(LabelFormatter(opName));
                var tex = GetButtonTexture(PolyhydraButtonTypes.OperatorType, opName);
                btn.gameObject.SetActive(true);
                btn.SetButtonTexture(tex);

                btn.name = $"Select Op: {i}";
                btn.ParentPanel = this;

                var btnPos = btn.transform.localPosition;
                btnPos.Set(i * 0.25f, 0, 0);
                btn.transform.localPosition = btnPos;

                if (i == CurrentActiveOpIndex)
                {
                    var popupPos = btn.transform.localPosition;
                    popupPos.Set(i * 0.25f + 0.04f, 0.05f, 0);
                    OperatorSelectPopupTools.localPosition = popupPos;
                    OperatorSelectPopupTools.localScale = Vector3.one * 0.2f;
                    ToolBtnPrev.gameObject.SetActive(i > 0);
                    ToolBtnNext.gameObject.SetActive(i < CurrentPolyhedra.Operators.Count - 1);
                    SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, opName);
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
