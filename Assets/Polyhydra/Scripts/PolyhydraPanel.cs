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
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
namespace TiltBrush
{

    public class PolyhydraPanel : BasePanel
    {
        [NonSerialized] public PreviewPolyhedron PolyhydraModel;
        [NonSerialized] public PreviewPolyhedron.MainCategories CurrentShapeCategory;

        public PolyhydraOptionButton ButtonShapeType;
        public PolyhydraOptionButton ButtonUniformType;
        [FormerlySerializedAs("ButtonRotationalType")] public PolyhydraOptionButton ButtonRadialType;
        public PolyhydraOptionButton ButtonGridType;
        public PolyhydraOptionButton ButtonOtherPolyType;
        public PolyhydraOptionButton ButtonGridShape;
        public PolyhydraOptionButton[] ButtonsConwayOps;
        public PolyhydraOptionButton[] ButtonsFaceSel;
        public PolyhydraSlider[] SlidersConwayOps;

        [FormerlySerializedAs("SliderP")] public PolyhydraSlider Slider1;
        public PolyhydraSlider Slider2;
        public PolyhydraSlider Slider3;
        public List<GameObject> MonoscopicOnlyButtons;

        private MeshFilter meshFilter;
        
        override public void InitPanel()
        {
            base.InitPanel();
            PolyhydraModel = gameObject.GetComponentInChildren<PreviewPolyhedron>(true);
            SetSliderConfiguration();
            SetPanelButtonVisibility();
        }

        public void HandleSlider1(Vector3 value)
        {
            PolyhydraModel.Param1Int = Mathf.FloorToInt(value.z);
            PolyhydraModel.Param1Float = value.z;
            PolyhydraModel.RebuildPoly();
        }

        public void HandleSlider2(Vector3 value)
        {
            PolyhydraModel.Param2Int = Mathf.FloorToInt(value.z);
            PolyhydraModel.Param2Float = value.z;
            PolyhydraModel.RebuildPoly();
        }

        public void HandleSlider3(Vector3 value)
        {
            PolyhydraModel.Param3Int = Mathf.FloorToInt(value.z);
            PolyhydraModel.Param3Float = value.z;
            PolyhydraModel.RebuildPoly();
        }
        
        public void HandleOpAmountSlider(Vector3 value)
        {
            int opStackIndex = (int)value.x;
            int paramIndex = (int)value.y;
            float amount = value.z;
            var op = PolyhydraModel.ConwayOperators[opStackIndex];
            switch (paramIndex)
            {
                case 0:
                    op.amount = amount;
                    break;
                case 1:
                    op.amount2 = amount;
                    break;
            }
            PolyhydraModel.ConwayOperators[opStackIndex] = op;
            PolyhydraModel.RebuildPoly();
        }


        void Update()
        {
            BaseUpdate();
            PolyhydraModel.transform.parent.Rotate(1, 1, 1);
        }

        public void SetPanelButtonVisibility()
        {

            foreach (var go in MonoscopicOnlyButtons)
            {
                go.SetActive(App.Config.m_SdkMode==SdkMode.Monoscopic);
            }
            
            var buttons = gameObject.GetComponentsInChildren<PolyhydraOptionButton>(true);

            switch (CurrentShapeCategory)
            {
                // All the shapeCategories that use the Uniform popup
                case PreviewPolyhedron.MainCategories.Archimedean:
                case PreviewPolyhedron.MainCategories.Platonic:
                case PreviewPolyhedron.MainCategories.KeplerPoinsot:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                            // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenUniformsPopup:
                                button.gameObject.SetActive(true);
                                break;

                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraRadialTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOtherTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

                case PreviewPolyhedron.MainCategories.Grids:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                            // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                                button.gameObject.SetActive(true);
                                break;

                            case SketchControlsScript.GlobalCommands.PolyhydraOpenUniformsPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraRadialTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOtherTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

                case PreviewPolyhedron.MainCategories.Various:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                            // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOtherTypesPopup:
                                button.gameObject.SetActive(true);
                                break;

                            case SketchControlsScript.GlobalCommands.PolyhydraOpenUniformsPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraRadialTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

                case PreviewPolyhedron.MainCategories.Radial:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                            // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraRadialTypesPopup:
                                button.gameObject.SetActive(true);
                                break;

                            case SketchControlsScript.GlobalCommands.PolyhydraOtherTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenUniformsPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

                case PreviewPolyhedron.MainCategories.Waterman:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                                // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                                button.gameObject.SetActive(true);
                                break;

                            case SketchControlsScript.GlobalCommands.PolyhydraRadialTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOtherTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenUniformsPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

            }
        }

        public void ConfigureGeometry()
        {
            switch (CurrentShapeCategory)
            {
                case PreviewPolyhedron.MainCategories.Platonic:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Uniform;
                    PolyhydraModel.UniformPolyType = (UniformTypes)Uniform.Platonic[0].Index - 1;
                    break;
                case PreviewPolyhedron.MainCategories.Archimedean:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Uniform;
                    PolyhydraModel.UniformPolyType = (UniformTypes)Uniform.Archimedean[0].Index - 1;
                    break;
                // case PolyhedraTypes.UniformConvex:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Convex[0].Index - 1;
                //     break;
                case PreviewPolyhedron.MainCategories.KeplerPoinsot:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Uniform;
                    PolyhydraModel.UniformPolyType = (UniformTypes)Uniform.KeplerPoinsot[0].Index - 1;
                    break;
                // case PolyhedraTypes.UniformStar:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Star[0].Index - 1;
                //     break;
                case PreviewPolyhedron.MainCategories.Radial:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Radial;
                    break;
                case PreviewPolyhedron.MainCategories.Waterman:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Waterman;
                    break;
                case PreviewPolyhedron.MainCategories.Grids:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Grid;
                    break;
                case PreviewPolyhedron.MainCategories.Various:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Various;
                    break;
            }

        }

        public void SetSliderConfiguration()
        {
            switch (CurrentShapeCategory)
            {
                case PreviewPolyhedron.MainCategories.Platonic:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                // case PolyhedraTypes.Prisms:
                //
                //     SliderP.Min = 3;
                //     SliderP.Max = 16;
                //     SliderQ.Min = 2;
                //     SliderQ.Max = 3;
                //     SliderP.SliderType = SliderTypes.Int;
                //     SliderQ.SliderType = SliderTypes.Int;
                //
                //     switch (PolyhydraModel.UniformPolyType)
                //     {
                //         case PolyTypes.Polygonal_Prism:
                //         case PolyTypes.Polygonal_Antiprism:
                //             SliderP.gameObject.SetActive(true);
                //             SliderQ.gameObject.SetActive(false);
                //             SliderP.SetDescriptionText("Sides");
                //             break;
                //         case PolyTypes.Polygrammic_Prism:
                //         case PolyTypes.Polygrammic_Antiprism:
                //         case PolyTypes.Polygrammic_Crossed_Antiprism:
                //             SliderP.gameObject.SetActive(true);
                //             SliderQ.gameObject.SetActive(true);
                //             SliderP.SetDescriptionText("Sides");
                //             SliderQ.SetDescriptionText("Q");
                //             break;
                //     }
                //
                //     break;

                case PreviewPolyhedron.MainCategories.Archimedean:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                // case PolyhedraTypes.UniformConvex:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Convex[0].Index - 1;
                //     break;

                case PreviewPolyhedron.MainCategories.KeplerPoinsot:

                    Slider1.gameObject.SetActive(false);
                    Slider2.gameObject.SetActive(false);
                    Slider3.gameObject.SetActive(false);
                    break;

                // case PolyhedraTypes.UniformStar:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Star[0].Index - 1;
                //     break;

                case PreviewPolyhedron.MainCategories.Radial:

                    Slider1.gameObject.SetActive(true);
                    Slider1.Min = 3;
                    Slider1.Max = 16;
                    Slider1.SetDescriptionText("Sides");
                    Slider1.SliderType = SliderTypes.Int;

                    switch (PolyhydraModel.RadialPolyType)
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

                case PreviewPolyhedron.MainCategories.Waterman:

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

                case PreviewPolyhedron.MainCategories.Grids:

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

                case PreviewPolyhedron.MainCategories.Various:

                    Slider1.SliderType = SliderTypes.Int;
                    Slider2.SliderType = SliderTypes.Int;
                    Slider3.SliderType = SliderTypes.Int;

                    switch (PolyhydraModel.OtherPolyType)
                    {
                        case PreviewPolyhedron.OtherPolyTypes.Polygon:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(false);
                            Slider3.gameObject.SetActive(false);
                            Slider1.Min = 3;
                            Slider1.Max = 16;
                            Slider1.SetDescriptionText("Sides");
                            break;
                        case PreviewPolyhedron.OtherPolyTypes.Box:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
                            Slider1.Min = 1;
                            Slider1.Max = 16;
                            Slider2.Min = 1;
                            Slider2.Max = 16;
                            Slider2.Min = 1;
                            Slider2.Max = 16;
                            Slider3.Min = 1;
                            Slider3.Max = 16;
                            Slider1.SetDescriptionText("X Resolution");
                            Slider2.SetDescriptionText("Y Resolution");
                            Slider3.SetDescriptionText("Z Resolution");
                            break;
                        case PreviewPolyhedron.OtherPolyTypes.UvSphere:
                        case PreviewPolyhedron.OtherPolyTypes.UvHemisphere:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(false);
                            Slider1.Min = 1;
                            Slider1.Max = 16;
                            Slider2.Min = 1;
                            Slider2.Max = 16;
                            Slider1.SetDescriptionText("Sides");
                            Slider2.SetDescriptionText("Slices");
                            break;
                        case PreviewPolyhedron.OtherPolyTypes.L_Shape:
                        case PreviewPolyhedron.OtherPolyTypes.C_Shape:
                        case PreviewPolyhedron.OtherPolyTypes.H_Shape:
                            Slider1.gameObject.SetActive(true);
                            Slider2.gameObject.SetActive(true);
                            Slider3.gameObject.SetActive(true);
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
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, creationTr, ColorMethods.ByRole,
                shapeType, EditableModelManager.m_Instance.m_PreviewPolyhedron.m_Parameters, EditableModelManager.m_Instance.m_PreviewPolyhedron.m_Operations);
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

        public void AddGuideForCurrentPolyhedron()
        {
            var tr = TrTransform.T(new Vector3(
                Random.value * 3 - 1.5f,
                Random.value * 7 + 7,
                Random.value * 8 + 2)
            );
            var poly = EditableModelManager.m_Instance.m_PreviewPolyhedron.m_PolyMesh;
            EditableModelManager.AddCustomGuide(poly, tr);
        }

    }

} // namespace TiltBrush
