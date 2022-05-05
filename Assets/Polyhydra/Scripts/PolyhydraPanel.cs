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
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;
using UnityEngine.Serialization;
namespace TiltBrush
{

    public class PolyhydraPanel : BasePanel
    {
        [NonSerialized] public PreviewPolyhedron PolyhydraModel;
        [NonSerialized] public PreviewPolyhedron.MainCategories CurrentShapeCategory;

        public PolyhydraOptionButton ButtonShapeType;
        public PolyhydraOptionButton ButtonUniformType;
        [FormerlySerializedAs("ButtonJohnsonType")] public PolyhydraOptionButton ButtonRotationalType;
        public PolyhydraOptionButton ButtonGridType;
        public PolyhydraOptionButton ButtonOtherPolyType;
        public PolyhydraOptionButton ButtonGridShape;
        public PolyhydraOptionButton[] ButtonsConwayOps;
        public PolyhydraOptionButton[] ButtonsFaceSel;
        public PolyhydraSlider[] SlidersConwayOps;

        public PolyhydraSlider SliderP;
        public PolyhydraSlider SliderQ;

        private MeshFilter meshFilter;

        override public void InitPanel()
        {
            base.InitPanel();
            PolyhydraModel = gameObject.GetComponentInChildren<PreviewPolyhedron>(true);
            SetSliderConfiguration();
            SetPanelButtonVisibility();
        }

        public void HandleSliderP(Vector3 value)
        {
            PolyhydraModel.PrismP = Mathf.FloorToInt(value.z);
            PolyhydraModel.RebuildPoly();
        }

        public void HandleSliderQ(Vector3 value)
        {
            PolyhydraModel.PrismQ = Mathf.FloorToInt(value.z);
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
                            case SketchControlsScript.GlobalCommands.PolyhydraRotationalTypesPopup:
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
                            case SketchControlsScript.GlobalCommands.PolyhydraRotationalTypesPopup:
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
                            case SketchControlsScript.GlobalCommands.PolyhydraRotationalTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridShapesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraGridTypesPopup:
                                button.gameObject.SetActive(false);
                                break;
                        }
                    }
                    break;

                case PreviewPolyhedron.MainCategories.Rotational:
                    foreach (var button in buttons)
                    {
                        switch (button.m_Command)
                        {
                            case SketchControlsScript.GlobalCommands.PolyhydraOpenShapeTypesPopup:
                            // case SketchControlsScript.GlobalCommands.PolyhydraConwayOpTypesPopup:
                            case SketchControlsScript.GlobalCommands.PolyhydraRotationalTypesPopup:
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

                            case SketchControlsScript.GlobalCommands.PolyhydraRotationalTypesPopup:
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
                case PreviewPolyhedron.MainCategories.Rotational:
                    PolyhydraModel.GeneratorType = GeneratorTypes.Rotational;
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

                    SliderP.gameObject.SetActive(false);
                    SliderQ.gameObject.SetActive(false);
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

                    SliderP.gameObject.SetActive(false);
                    SliderQ.gameObject.SetActive(false);
                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;
                    break;

                // case PolyhedraTypes.UniformConvex:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Convex[0].Index - 1;
                //     break;

                case PreviewPolyhedron.MainCategories.KeplerPoinsot:

                    SliderP.gameObject.SetActive(false);
                    SliderQ.gameObject.SetActive(false);
                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;
                    break;

                // case PolyhedraTypes.UniformStar:
                //     PolyhydraModel.ShapeType = VrUiPoly.PolyhedraTypes.Uniform;
                //     PolyhydraModel.UniformPolyType = (UniformTypes) Uniform.Star[0].Index - 1;
                //     break;

                case PreviewPolyhedron.MainCategories.Rotational:

                    SliderP.gameObject.SetActive(true);
                    SliderQ.gameObject.SetActive(false);
                    SliderP.SetDescriptionText("Sides");
                    SliderP.Min = 3;
                    SliderP.Max = 16;
                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;
                    break;

                case PreviewPolyhedron.MainCategories.Waterman:

                    SliderP.gameObject.SetActive(true);
                    SliderQ.gameObject.SetActive(true);
                    SliderP.SetDescriptionText("Root");
                    SliderQ.SetDescriptionText("C");
                    SliderP.Min = 1;
                    SliderP.Max = 80;
                    SliderQ.Min = 1;
                    SliderQ.Max = 7;
                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;
                    break;

                case PreviewPolyhedron.MainCategories.Grids:

                    var p = SliderP.GetCurrentValue();
                    var q = SliderQ.GetCurrentValue();
                    SliderP.gameObject.SetActive(true);
                    SliderQ.gameObject.SetActive(true);
                    SliderP.SetDescriptionText("Width");
                    SliderQ.SetDescriptionText("Depth");
                    SliderP.Min = 1;
                    SliderP.Max = 8;
                    SliderQ.Min = 1;
                    SliderQ.Max = 8;
                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;
                    SliderP.UpdateValueAbsolute(2);
                    SliderQ.UpdateValueAbsolute(2);
                    break;

                case PreviewPolyhedron.MainCategories.Various:

                    SliderP.SliderType = SliderTypes.Int;
                    SliderQ.SliderType = SliderTypes.Int;

                    switch (PolyhydraModel.OtherPolyType)
                    {
                        case PreviewPolyhedron.OtherPolyTypes.Polygon:
                            SliderP.gameObject.SetActive(true);
                            SliderQ.gameObject.SetActive(false);
                            SliderP.Min = 3;
                            SliderP.Max = 16;
                            SliderP.SetDescriptionText("Sides");
                            break;
                        case PreviewPolyhedron.OtherPolyTypes.Box:
                            SliderP.gameObject.SetActive(true);
                            SliderQ.gameObject.SetActive(true);
                            SliderP.Min = 1;
                            SliderP.Max = 10;
                            SliderQ.Min = 1;
                            SliderQ.Max = 10;
                            SliderP.SetDescriptionText("X Resolution");
                            SliderP.SetDescriptionText("Y Resolution");
                            break;
                        case PreviewPolyhedron.OtherPolyTypes.UvSphere:
                        case PreviewPolyhedron.OtherPolyTypes.UvHemisphere:
                            SliderP.gameObject.SetActive(true);
                            SliderQ.gameObject.SetActive(true);
                            SliderP.Min = 1;
                            SliderP.Max = 16;
                            SliderQ.Min = 1;
                            SliderQ.Max = 16;
                            SliderP.SetDescriptionText("Sides");
                            SliderQ.SetDescriptionText("Slices");
                            break;
                        default:
                            SliderP.gameObject.SetActive(false);
                            SliderQ.gameObject.SetActive(false);
                            break;
                    }

                    break;
            }
            SliderP.UpdateValue(SliderP.GetCurrentValue());
            SliderQ.UpdateValue(SliderQ.GetCurrentValue());
        }
    }

} // namespace TiltBrush
