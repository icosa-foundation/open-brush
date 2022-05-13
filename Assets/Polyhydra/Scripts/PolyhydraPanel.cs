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
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TiltBrush
{

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

        public int CurrentActiveOpIndex = -1;
        private int OperatorCount = 0;
        public Transform OperatorSelectButtonParent;
        public Transform OperatorSelectButtonPrefab;

        public static Dictionary<string, object> m_GeneratorParameters;
        public static List<Dictionary<string, object>> m_Operations;

        public float previewRotationX, previewRotationY, previewRotationZ = .5f;
        
        private MeshFilter meshFilter;
        
        private MainCategories currentMainCategory;
        private OtherSolidsCategories otherSolidsCategory;

        private enum MainCategories
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
        
        

        void Update()
        {
            BaseUpdate();
            CurrentPolyhedra.transform.parent.Rotate(previewRotationX, previewRotationY, previewRotationZ);
        }

        public void SetMainButtonVisibility()
        {
            
            

            foreach (var go in MonoscopicOnlyButtons)
            {
                go.SetActive(App.Config.m_SdkMode==SdkMode.Monoscopic);
            }
            
            var mainButtons = gameObject.GetComponentsInChildren<PolyhydraOptionButton>(true);

            switch (currentMainCategory)
            {
                // All the shapeCategories that use the Uniform popup
                case MainCategories.Archimedean:
                case MainCategories.Platonic:
                case MainCategories.KeplerPoinsot:
                    ButtonUniformType.gameObject.SetActive(true);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    break;

                case MainCategories.Grids:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(true);
                    ButtonGridShape.gameObject.SetActive(true);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    break;

                case MainCategories.Various:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(true);
                    break;

                case MainCategories.Radial:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(true);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    break;

                case MainCategories.Waterman:
                    ButtonUniformType.gameObject.SetActive(false);
                    ButtonRadialType.gameObject.SetActive(false);
                    ButtonGridType.gameObject.SetActive(false);
                    ButtonGridShape.gameObject.SetActive(false);
                    ButtonOtherSolidsType.gameObject.SetActive(false);
                    break;
            }
        }

        public void ConfigureGeometry()
        {
            switch (currentMainCategory)
            {
                case MainCategories.Platonic:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.Platonic[0].Index - 1;
                    break;
                case MainCategories.Archimedean:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    break;
                case MainCategories.KeplerPoinsot:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Uniform;
                    CurrentPolyhedra.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    break;
                case MainCategories.Radial:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Radial;
                    break;
                case MainCategories.Waterman:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case MainCategories.Grids:
                    CurrentPolyhedra.GeneratorType = GeneratorTypes.Grid;
                    break;
                case MainCategories.Various:
                    // Various can map to either GeneratorTypes.Various or GeneratorTypes.Shapes
                    switch (otherSolidsCategory)
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

        public void SetSliderConfiguration()
        {
            switch (currentMainCategory)
            {
                case MainCategories.Platonic:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;
                
                case MainCategories.Archimedean:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                case MainCategories.KeplerPoinsot:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                case MainCategories.Radial:

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

                case MainCategories.Waterman:

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

                case MainCategories.Grids:

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

                case MainCategories.Various:

                    switch (otherSolidsCategory)
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
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, creationTr, ColorMethods.ByTags,
                shapeType, m_GeneratorParameters, m_Operations);
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
            var buttonSelectOpSlot = OperatorSelectButtonParent.GetChild(CurrentActiveOpIndex).GetComponent<PolyhydraActionButton>();
            buttonSelectOpSlot.SetDescriptionText(operationName);
            buttonSelectOpSlot.SetButtonTexture(GetButtonTextureByOpName(operationName));
            
            var ops = CurrentPolyhedra.Operators;
            var op = ops[CurrentActiveOpIndex];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);

            OpConfig opConfig = OpConfigs.Configs[op.opType];
            op.opType = (PolyMesh.Operation)Enum.Parse(typeof(PolyMesh.Operation), operationName);
            
            op.amount = opConfig.amountDefault;
            op.amount2 = opConfig.amount2Default;
            ops[CurrentActiveOpIndex] = op;
            CurrentPolyhedra.Operators = ops;

            ConfigureOpPanel(op);
        }

        public void HandleSelectOpButton(int index)
        {
            CurrentActiveOpIndex = index;
            var op = CurrentPolyhedra.Operators[index];
            ConfigureOpPanel(op);
        }
        
        public void ConfigureOpPanel(PreviewPolyhedron.OpDefinition op)
        {
            OpPanel.SetActive(true);
            string operationName = op.opType.ToString();
            ButtonOpType.SetDescriptionText(operationName);
            ButtonOpType.SetButtonTexture(GetButtonTextureByOpName(operationName));

            OpConfig opConfig = OpConfigs.Configs[op.opType];

            if (opConfig.usesFilter)
            {
                OpFilterControlParent.SetActive(true);
                // ButtonOpFilterType.gameObject.SetActive(true);
            }
            else
            {
                OpFilterControlParent.SetActive(false);
                // ButtonOpFilterType.gameObject.SetActive(false);
            }

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
                ////SetOpColor();
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
            popup.ColorPicker.ColorFinalized += MakeOpColorFinalized();
            popup.CustomColorPalette.ColorPicked += MakeOpColorPickedAsFinal();

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
        

        
        Action<Color> MakeOpColorPickedAsFinal()
        {
            return delegate(Color c)
            {
                ////
            };
        }
        
        Action MakeOpColorFinalized()
        {
            return delegate
            {
                ////
            };
        }
        
        
        
        
        
        
        
        
        

        public void HandleAddOpButton()
        {
            Transform btnTr = Instantiate(OperatorSelectButtonPrefab, OperatorSelectButtonParent, false);
            var btn = btnTr.GetComponent<PolyhydraActionButton>();
            var pos = btn.transform.localPosition;
            pos.Set(OperatorCount * 1.1f, 0, 0);
            btn.transform.localPosition = pos;
            btn.name = $"Select Op: {OperatorCount}";
            btn.ParentPanel = this;
            btn.OpIndex = OperatorCount;
            OperatorCount++;
            btn.SetDescriptionText($"Operator {OperatorCount}");
            btn.gameObject.SetActive(true);
            HandleSelectOpButton(OperatorCount - 1);
            CurrentPolyhedra.Operators.Add(new PreviewPolyhedron.OpDefinition());
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

        private Texture2D GetButtonTextureByOpName(string name)
        {
            var path = $"IconButtons/{name}";
            return Resources.Load<Texture2D>(path);
        }

        public void HandleOtherSolidsButtonPress(string action, Texture2D texture)
        {
            var OtherType = (OtherSolidsCategories)Enum.Parse(typeof(OtherSolidsCategories), action);
            otherSolidsCategory = OtherType;
            switch (OtherType)
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
            ButtonOtherSolidsType.SetButtonTexture(texture);
        }

        public void HandleMainCategoryButtonPress(string action)
        {
            var mainCategory = (MainCategories)Enum.Parse(typeof(MainCategories), action);
            currentMainCategory = mainCategory;
            SetMainButtonVisibility();
            SetSliderConfiguration();
            ConfigureGeometry();
        }

        public List<string> GetOtherSolidCategoryNames()
        {
            return Enum.GetNames(typeof(OtherSolidsCategories)).ToList();
        }
        
        public List<string> GetMainCategoryNames()
        {
            return Enum.GetNames(typeof(MainCategories)).ToList();
        }
        
        public List<string> GetUniformPolyNames()
        {
            Uniform[] uniformList = null;
            switch (currentMainCategory)
            {
                case MainCategories.Platonic:
                    uniformList = Uniform.Platonic;
                    break;
                case MainCategories.Archimedean:
                    uniformList = Uniform.Archimedean;
                    break;
                case MainCategories.KeplerPoinsot:
                    uniformList = Uniform.KeplerPoinsot;
                    break;
            }
            return uniformList.Select(x => x.Name).ToList();
        }
        
        public void ConfigureOpFilterPanel(PreviewPolyhedron.OpDefinition op)
        {
            var opFilterName = op.filterType.ToString();
            ButtonOpFilterType.SetDescriptionText(opFilterName);
            OpFilterControlParent.SetActive(true);

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
                    SliderOpFilterParam.Max = 10;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamInt);
                    break;
                case PreviewPolyhedron.AvailableFilters.LastN:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Int;
                    SliderOpFilterParam.Min = 1;
                    SliderOpFilterParam.Max = 10;
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
                case PreviewPolyhedron.AvailableFilters.FacingHorizontal:
                case PreviewPolyhedron.AvailableFilters.FacingVertical:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = -1f;
                    SliderOpFilterParam.Max = 1f;
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
                    SliderOpFilterParam.Min = -2f;
                    SliderOpFilterParam.Max = 2f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
                case PreviewPolyhedron.AvailableFilters.DistanceFromCenter:
                    SliderOpFilterParam.gameObject.SetActive(true);
                    SliderOpFilterParam.SliderType = SliderTypes.Float;
                    SliderOpFilterParam.Min = 0f;
                    SliderOpFilterParam.Max = 2f;
                    SliderOpFilterParam.UpdateValueAbsolute(op.filterParamFloat);
                    break;
            }
        }
    }

} // namespace TiltBrush
