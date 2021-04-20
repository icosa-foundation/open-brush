using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Wythoff;
using Conway;
using TiltBrush;
using TMPro;
using Button = UnityEngine.UI.Button;


public class VrUi : MonoBehaviour
{
    private int _PanelIndex;
    private int _ShapeCategoryIndex;
    private int _ShapeIndex;
    private int _PanelWidgetIndex;
    private VrUiPoly _Poly;
    
    public float axisThreshold = 0.5f;

    public int FrameSkip = 3;
    public Color HighlightColor;
    public Color ShapeButtonColor;
    public Transform PanelContainer;
    public Transform PanelPrefab;
    public Transform TextButtonPrefab;
    public Transform ValueButtonPrefab;
    public Transform ImagePanelPrefab;
  
    private List<VrUiPoly.ConwayOperator> _Stack;
    private List<List<Transform>> Widgets;
    private List<List<ButtonType>> ButtonTypeMapping;
    private int _MainMenuCount;
    private int _CurrentMainMenuIndex;

    private bool scrolledXlastFrame = false;
    private bool scrolledYlastFrame = false;
    public enum ShapeCategories
    {
        Platonic,
        Prisms,
        Archimedean,
        KeplerPoinsot,
        // UniformConvex,
        // UniformStar,
        Johnson,
        Waterman,
        Grids,
        Other
    }

    private enum ButtonType {
        ShapeCategory, GridType, UniformType, JohnsonType, WatermanType, OtherType,
        PolyTypeCategory, GridShape, PolyP, PolyQ,
        OpType, Amount, Amount2, FaceSelection, Tags,
        Unknown
    }

    void Start()
    {
        _Poly = FindObjectOfType<VrUiPoly>();
        //_Poly.GenerateSubmeshes = true;
        _Stack = _Poly.ConwayOperators;

        UpdateUI();
        UpdateTips();
    }

    private void UpdateTips()
    {
        PlayerPrefs.GetInt("currentTip");
    }


    private void GetPanelWidgets(int panelIndex, out List<Transform> panelWidgets, out List<ButtonType> panelWidgetTypes)
    {
        panelWidgets = new List<Transform>();
        panelWidgetTypes = new List<ButtonType>();

        int stackIndex = panelIndex - 1;
        bool isDisabled = _Stack[stackIndex].disabled;

        var panelContainer = Instantiate(PanelPrefab, PanelContainer);
        panelContainer.GetComponent<CanvasGroup>().alpha = isDisabled ? 0.5f : 1f;


        for (int i = 0; i <= 3; i++)
        {
            Transform nextWidget = null;
            (string label, ButtonType buttonType) = GetButton(panelIndex, i);

            if (buttonType == ButtonType.OpType)
            {
                nextWidget = Instantiate(TextButtonPrefab, panelContainer);
                var img = Instantiate(ImagePanelPrefab, panelContainer);
                var icon = Resources.Load<Sprite>("Icons/" + _Stack[stackIndex].opType);
                img.GetChild(0).GetComponent<Image>().sprite = icon;
                
            }
            else if (buttonType == ButtonType.Amount || buttonType == ButtonType.Amount2)
            {
                nextWidget = Instantiate(ValueButtonPrefab, panelContainer);
                (float amount, float amount2) = GetNormalisedAmountValues(_Stack[stackIndex]);
                var rt = nextWidget.gameObject.GetComponentsInChildren<Image>().Last().transform as RectTransform;
                var d = rt.sizeDelta;
                d.x = buttonType == ButtonType.Amount ? amount : amount2;
                rt.sizeDelta = d;
            }
            else if (buttonType == ButtonType.FaceSelection)
            {
                nextWidget = Instantiate(TextButtonPrefab, panelContainer);
            }

            if (nextWidget != null)
            {
                nextWidget.GetComponentInChildren<TextMeshProUGUI>().text = label;
                // nextWidget.GetComponent<Button>().interactable = !isDisabled;
                panelWidgets.Add(nextWidget);
                panelWidgetTypes.Add(buttonType);
            }
        }
    }

    void Update()
    {
        HandleInput();
    }

    int GetWidgetCount()
    {
        // Number of widgets in this column
        return Widgets[_PanelIndex].Count;
    }

    void HandleInput()
    {
        int stackIndex = _PanelIndex - 1;

        bool uiDirty = false;
        bool polyDirty = false;

        if (InputManager.Brush.GetScrollXDelta() > -axisThreshold &&
            InputManager.Brush.GetScrollXDelta() < axisThreshold)
        {
            scrolledXlastFrame = false;
        }
        
        if (InputManager.Brush.GetScrollYDelta() > -axisThreshold &&
            InputManager.Brush.GetScrollYDelta() < axisThreshold)
        {
            scrolledYlastFrame = false;
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            if (stackIndex < 0) return;
            var op = _Stack[stackIndex];
            op.disabled = !op.disabled;
            _Stack[stackIndex] = op;
            uiDirty = true;
            polyDirty = true;
        }
        else if (false) // (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _PanelIndex -= 1;
            _PanelIndex = Mathf.Clamp(_PanelIndex, 0, _Stack.Count);
            _PanelWidgetIndex = Mathf.Clamp(_PanelWidgetIndex, 0, GetWidgetCount() - 1);
            uiDirty = true;
        }
        else if (false)
        {
            _PanelIndex += 1;
            _PanelIndex = Mathf.Clamp(_PanelIndex, 0, _Stack.Count);
            _PanelWidgetIndex = Mathf.Clamp(_PanelWidgetIndex, 0, GetWidgetCount() - 1);
            uiDirty = true;
        }
        else if (InputManager.Brush.GetScrollYDelta() > axisThreshold && !scrolledYlastFrame)
        {
            if (_PanelWidgetIndex > 0)
            {
                _PanelWidgetIndex -= 1;
            }
            else
            {
                if (_PanelIndex > 0)
                {
                    _PanelIndex -= 1;
                    _PanelWidgetIndex = GetWidgetCount() - 1;
                }
            }
            scrolledYlastFrame = true;
            uiDirty = true;
        }
        else if (InputManager.Brush.GetScrollYDelta() < -axisThreshold && !scrolledYlastFrame)
        {
            if (_PanelWidgetIndex < GetWidgetCount() - 1)
            {
                _PanelWidgetIndex += 1;
            }
            else
            {
                if (_PanelIndex <= _Stack.Count - 1)
                {
                    _PanelIndex += 1;
                    _PanelWidgetIndex = 0;
                }
            }
            scrolledYlastFrame = true;
            uiDirty = true;
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (stackIndex < 0) return;
            _Stack.RemoveAt(stackIndex);
            _PanelIndex = Mathf.Clamp(_PanelIndex, 0, _Stack.Count);
            uiDirty = true;
            polyDirty = true;
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            _Stack.Insert(stackIndex + 1, new VrUiPoly.ConwayOperator
            {
                opType = Ops.Kis,
                amount = 0.1f
            });
            _PanelIndex += 1;
            _PanelWidgetIndex = 0;
            polyDirty = true;
            uiDirty = true;
        }
        else if (InputManager.Brush.GetScrollXDelta() < -axisThreshold)
        {
            if (_PanelIndex < 0) return;
            ChangeValue(-1);
            scrolledXlastFrame = true;
            // TODO only set dirty flags if we've changed
            uiDirty = true;
            polyDirty = true;
        }
        else if (InputManager.Brush.GetScrollXDelta() > axisThreshold)
        {
            if (_PanelIndex < 0) return;
            ChangeValue(1);
            scrolledXlastFrame = true;
            // TODO only set dirty flags if we've changed
            uiDirty = true;
            polyDirty = true;
        }
        // else if (Input.GetKey(KeyCode.S) || Input.GetAxis("Vertical") < -0.1f)
        // {
        //     if (_PanelIndex < 0) return;
        //     ChangeValue(-10);
        //     uiDirty = true;
        //     polyDirty = true;
        // }
        // else if (Input.GetKey(KeyCode.W) || Input.GetAxis("Vertical") > 0.1f)
        // {
        //     if (_PanelIndex < 0) return;
        //     ChangeValue(10);
        //     uiDirty = true;
        //     polyDirty = true;
        // }

        if (uiDirty) UpdateUI();
        if (polyDirty)
        {
            _Poly.Validate();
            _Poly.MakePolyhedron();
        }

    }
    
    private void ChangeValue(int direction)
    {
        if (_PanelIndex == 0)
        {
            ChangeValueOnPolyPanel(direction);
        }
        else
        {
            ChangeValueOnOpPanel(direction);
        }
    }

    private void ChangeValueOnOpPanel(int direction)
    {

        int stackIndex = _PanelIndex - 1;
        var opConfig = PolyHydraEnums.OpConfigs[_Stack[stackIndex].opType];

        ButtonType buttonType = ButtonTypeMapping[_PanelIndex][_PanelWidgetIndex];
        switch (buttonType)
        {
            case ButtonType.OpType:
                if (scrolledXlastFrame) return;
                _Stack[stackIndex] = _Stack[stackIndex].ChangeOpType(direction);
                opConfig = PolyHydraEnums.OpConfigs[_Stack[stackIndex].opType];
                _Stack[stackIndex] = _Stack[stackIndex].SetDefaultValues(opConfig);
                break;
            case ButtonType.Amount:
                if (Time.frameCount % FrameSkip == 0) // Rate limit but always allows single key presses
                {
                    _Stack[stackIndex] = _Stack[stackIndex].ChangeAmount(direction * 0.025f);
                    _Stack[stackIndex] = _Stack[stackIndex].ClampAmount(opConfig, true);
                }
                break;
            case ButtonType.Amount2:
                if (Time.frameCount % FrameSkip == 0) // Rate limit but always allows single key presses
                {
                    _Stack[stackIndex] = _Stack[stackIndex].ChangeAmount2(direction * 0.025f);
                    _Stack[stackIndex] = _Stack[stackIndex].ClampAmount2(opConfig, true);
                }
                break;
            case ButtonType.FaceSelection:
                if (scrolledXlastFrame) return;
                _Stack[stackIndex] = _Stack[stackIndex].ChangeFaceSelection(direction);
                break;
        }
    }

    void ChangeValueOnPolyPanel(int direction)
    {
        (int minPrismPValue, int maxPrismPValue, int minPrismQValue, int maxPrismQValue) =  GetPQRanges((ShapeCategories) _ShapeCategoryIndex);

        // GetKey brought us here but we only want GetKeyDown in this case
        switch (_PanelWidgetIndex)
        {
            case 0:
                if (scrolledXlastFrame) return;
                _ShapeCategoryIndex += direction;
                _ShapeCategoryIndex = Mathf.Clamp(_ShapeCategoryIndex, 0, Enum.GetNames(typeof(ShapeCategories)).Length - 1);
                switch ((ShapeCategories)_ShapeCategoryIndex)
                {
                    case ShapeCategories.Platonic:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Platonic[0].Index - 1;
                        break;
                    case ShapeCategories.Prisms:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Prismatic[0].Index - 1;
                        break;
                    case ShapeCategories.Archimedean:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Archimedean[0].Index - 1;
                        break;
                    // case ShapeCategories.UniformConvex:
                    //     _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                    //     _Poly.UniformPolyType = (PolyTypes)Uniform.Convex[0].Index - 1;
                    //     break;
                    case ShapeCategories.KeplerPoinsot:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                        _Poly.UniformPolyType = (PolyTypes)Uniform.KeplerPoinsot[0].Index - 1;
                        break;
                    // case ShapeCategories.UniformStar:
                    //     _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Uniform;
                    //     _Poly.UniformPolyType = (PolyTypes)Uniform.Star[0].Index - 1;
                    //     break;
                    case ShapeCategories.Johnson:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Johnson;
                        break;
                    case ShapeCategories.Waterman:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Waterman;
                        break;
                    case ShapeCategories.Grids:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Grid;
                        break;
                    case ShapeCategories.Other:
                        _Poly.ShapeType = PolyHydraEnums.ShapeTypes.Other;
                        break;
                }
                _ShapeIndex = 0;
                break;
            case 1:
                if (scrolledXlastFrame) return;
                switch ((ShapeCategories)_ShapeCategoryIndex)
                {
                    case ShapeCategories.Platonic:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.Platonic.Length - 1);
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Platonic[_ShapeIndex].Index - 1;
                        break;
                    case ShapeCategories.Prisms:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.Prismatic.Length - 1);
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Prismatic[_ShapeIndex].Index - 1;
                        break;
                    case ShapeCategories.Archimedean:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.Archimedean.Length - 1);
                        _Poly.UniformPolyType = (PolyTypes)Uniform.Archimedean[_ShapeIndex].Index - 1;
                        break;
                    // case ShapeCategories.UniformConvex:
                    //     _ShapeIndex += direction;
                    //     _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.Convex.Length - 1);
                    //     _Poly.UniformPolyType = (PolyTypes)Uniform.Convex[_ShapeIndex].Index - 1;
                    //     break;
                    case ShapeCategories.KeplerPoinsot:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.KeplerPoinsot.Length - 1);
                        _Poly.UniformPolyType = (PolyTypes)Uniform.KeplerPoinsot[_ShapeIndex].Index - 1;
                        break;
                    // case ShapeCategories.UniformStar:
                    //     _ShapeIndex += direction;
                    //     _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Uniform.Star.Length - 1);
                    //     _Poly.UniformPolyType = (PolyTypes)Uniform.Star[_ShapeIndex].Index - 1;
                    //     break;
                    case ShapeCategories.Johnson:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Enum.GetNames(typeof(PolyHydraEnums.JohnsonPolyTypes)).Length - 1);
                        _Poly.JohnsonPolyType = (PolyHydraEnums.JohnsonPolyTypes)_ShapeIndex;
                        break;
                    case ShapeCategories.Grids:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Enum.GetNames(typeof(PolyHydraEnums.GridTypes)).Length - 1);
                        _Poly.GridType = (PolyHydraEnums.GridTypes)_ShapeIndex;
                        break;
                    case ShapeCategories.Other:
                        _ShapeIndex += direction;
                        _ShapeIndex = Mathf.Clamp(_ShapeIndex, 0, Enum.GetNames(typeof(PolyHydraEnums.OtherPolyTypes)).Length - 1);
                        _Poly.OtherPolyType = (PolyHydraEnums.OtherPolyTypes)_ShapeIndex;
                        break;
                }
                break;
            case 2:  // PrismP
                int p = _Poly.PrismP;
                p += direction;
                p = Mathf.Clamp(p, minPrismPValue, maxPrismPValue);
                _Poly.PrismP = p;
                break;
            case 3:  // PrismQ
                int q = _Poly.PrismQ;
                q += direction;
                q = Mathf.Clamp(q, minPrismQValue, maxPrismQValue);
                _Poly.PrismQ = q;
                break;
        }

    }

    private (int, int, int, int) GetPQRanges(ShapeCategories shapeCategory)
    {
        int minPrismPValue = 0;
        int maxPrismPValue = 0;
        int minPrismQValue = 0;
        int maxPrismQValue = 0;
        switch (shapeCategory)
        {
            case ShapeCategories.Grids:
                minPrismPValue = 1;
                maxPrismPValue = 16;
                minPrismQValue = 1;
                maxPrismQValue = 16;
                break;
            case ShapeCategories.Johnson:
                minPrismPValue = 3;
                maxPrismPValue = 16;
                break;
            case ShapeCategories.Prisms:
                minPrismPValue = 3;
                maxPrismPValue = 16;
                minPrismQValue = 2;
                maxPrismQValue = 3;
                break;
            case ShapeCategories.Other:
                minPrismPValue = 3;
                maxPrismPValue = 16;
                minPrismQValue = 3;
                maxPrismQValue = 16;
                break;
        }
        return (minPrismPValue, maxPrismPValue, minPrismQValue, maxPrismQValue);
    }

    void UpdateUI()
    {
        foreach (Transform child in PanelContainer)
        {
            if (child.gameObject.name != "Main Menu")
            {
                Destroy(child.gameObject);
            }
        }

        Widgets = new List<List<Transform>>();
        ButtonTypeMapping = new List<List<ButtonType>>();

        var polyWidgets = new List<Transform>();
        var polyWidgetTypes = new List<ButtonType>();
        var polyPanel = Instantiate(PanelPrefab, PanelContainer);

        Transform polyButton;

        int polyBtnCount = 2;


        if (
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Uniform && (int)_Poly.UniformPolyType < 5) ||
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Other && (int)_Poly.OtherPolyType == 0) ||
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Johnson)
            )
        {
            polyBtnCount = 3;
        }
        else if (
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Uniform && (int)_Poly.UniformPolyType < 5) ||
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Other && (int)_Poly.OtherPolyType < 4) ||
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Grid) ||
            (_Poly.ShapeType==PolyHydraEnums.ShapeTypes.Waterman)
        )
        {
            polyBtnCount = 4;
        }

        for (int i = 0; i < polyBtnCount; i++)
        {
            polyButton = Instantiate(TextButtonPrefab, polyPanel);
            // if (i == 1)  // Add image panel after 2nd text button
            // {
                // var polyPrefab = Instantiate(ImagePanelPrefab, polyPanel);
                // var polyImg = polyPrefab.GetComponentInChildren<Image>();
                // string shapeName = "";
                // switch (_Poly.ShapeType)
                // {
                //     case PolyHydraEnums.ShapeTypes.Uniform:
                //         shapeName = $"uniform_{_Poly.UniformPolyType}";
                //         break;
                //     case PolyHydraEnums.ShapeTypes.Johnson:
                //         shapeName = $"johnson_{_Poly.JohnsonPolyType}";
                //         break;
                //     case PolyHydraEnums.ShapeTypes.Grid:
                //         shapeName = $"grid_{_Poly.GridType}";
                //         break;
                //     case PolyHydraEnums.ShapeTypes.Other:
                //         shapeName = $"other_{_Poly.OtherPolyType}";
                //         break;
                // }
                //polyImg.sprite = Resources.Load<Sprite>($"BaseShapes/poly_{shapeName}");
                // polyImg.GetComponentInChildren<RectTransform>().sizeDelta = new Vector2(110, 110);
            // }

            (string label, ButtonType buttonType) = GetButton(0, i);
            polyButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
            polyWidgets.Add(polyButton);
            polyWidgetTypes.Add(buttonType);
        }

        Widgets.Add(polyWidgets);
        ButtonTypeMapping.Add(polyWidgetTypes);

        for (var widgetIndex = 1; widgetIndex <= _Stack.Count; widgetIndex++)
        {
            var panelButtons = new List<Transform>();
            var panelWidgetTypes = new List<ButtonType>();
            GetPanelWidgets(widgetIndex, out panelButtons, out panelWidgetTypes);
            Widgets.Add(panelButtons);
            ButtonTypeMapping.Add(panelWidgetTypes);
        }

        for (int panelIndex=0; panelIndex < Widgets.Count; panelIndex++)
        {

            Color normalColor = panelIndex == 0 ? ShapeButtonColor : Color.white;

            for (int widgetIndex = 0; widgetIndex < Widgets[panelIndex].Count; widgetIndex++)
            {
                var widget = Widgets[panelIndex][widgetIndex];
                (string label, ButtonType buttonType) = GetButton(panelIndex, widgetIndex);
                widget.GetComponentInChildren<TextMeshProUGUI>().text = label;

                var colors = widget.GetComponent<Button>().colors;

                if (panelIndex == _PanelIndex && _PanelWidgetIndex == widgetIndex)
                {
                    colors.normalColor = HighlightColor;
                }
                else
                {
                    colors.normalColor = normalColor;
                }

                widget.GetComponent<Button>().colors = colors;
            }
        }
    }

    (string, ButtonType) GetButton(int currentPanelIndex, int widgetIndex)
    {
        string label = "";
        ButtonType buttonType = ButtonType.Unknown;

        if (currentPanelIndex == 0)
        {
            switch (widgetIndex)
            {
                case 0:
                    label = $"{Enum.GetNames(typeof(ShapeCategories))[_ShapeCategoryIndex]}";
                    break;
                    buttonType = ButtonType.ShapeCategory;
                case 1:
                    switch (_Poly.ShapeType)
                    {
                        case PolyHydraEnums.ShapeTypes.Uniform:
                            string uniformName = _Poly.UniformPolyType.ToString().Replace("_", " ");
                            label = $"{uniformName}"; break;
                            buttonType = ButtonType.UniformType;
                        case PolyHydraEnums.ShapeTypes.Grid:
                            label = $"{_Poly.GridType}"; break;
                            buttonType = ButtonType.GridType;
                        case PolyHydraEnums.ShapeTypes.Johnson:
                            label = $"{_Poly.JohnsonPolyType}"; break;
                            buttonType = ButtonType.JohnsonType;
                        case PolyHydraEnums.ShapeTypes.Waterman:
                            label = $""; break;
                            buttonType = ButtonType.WatermanType;
                        case PolyHydraEnums.ShapeTypes.Other:
                            label = $"{_Poly.OtherPolyType}"; break;
                            buttonType = ButtonType.OtherType;
                    }
                    break;
                case 2:
                    label = $"{_Poly.PrismP}"; break;
                case 3:
                    label = $"{_Poly.PrismQ}"; break;
            }
        }
        else
        {

            int stackIndex = currentPanelIndex - 1;

            var opConfig = PolyHydraEnums.OpConfigs[_Stack[stackIndex].opType];

            var lookup = (opConfig.usesAmount, opConfig.usesAmount2, opConfig.usesFaces, col: widgetIndex);

            // Handle all the permutations of config, column and button type
            var logicTable = new Dictionary<(bool, bool, bool, int), ButtonType>
            {
                {(false, false, false, 1), ButtonType.Unknown},
                {(false, false, false, 2), ButtonType.Unknown},
                {(false, false, false, 3), ButtonType.Unknown},
                {(false, false, false, 4), ButtonType.Unknown},

                {(false, false, true, 1), ButtonType.FaceSelection},
                {(false, false, true, 2), ButtonType.Tags},
                {(false, false, true, 3), ButtonType.Unknown},
                {(false, false, true, 4), ButtonType.Unknown},

                {(true, false, false, 1), ButtonType.Amount},
                {(true, false, false, 2), ButtonType.Unknown},
                {(true, false, false, 3), ButtonType.Unknown},
                {(true, false, false, 4), ButtonType.Unknown},

                {(true, false, true, 1), ButtonType.Amount},
                {(true, false, true, 2), ButtonType.FaceSelection},
                {(true, false, true, 3), ButtonType.Tags},
                {(true, false, true, 4), ButtonType.Unknown},

                {(true, true, false, 1), ButtonType.Amount},
                {(true, true, false, 2), ButtonType.Amount2},
                {(true, true, false, 3), ButtonType.Unknown},
                {(true, true, false, 4), ButtonType.Unknown},

                {(true, true, true, 1), ButtonType.Amount},
                {(true, true, true, 2), ButtonType.Amount2},
                {(true, true, true, 3), ButtonType.FaceSelection},
                {(true, true, true, 4), ButtonType.Tags},
            };


            if (widgetIndex == 0)
            {
                buttonType = ButtonType.OpType;
            }
            else
            {
                if (_Stack[stackIndex].opType == Ops.TagFaces)  // Special case
                {
                    switch (widgetIndex)
                    {
                        case 1: buttonType = ButtonType.Tags; break;
                        case 2: buttonType = ButtonType.FaceSelection; break;
                        case 3: buttonType = ButtonType.Unknown; break;
                        case 4: buttonType = ButtonType.Unknown; break;
                    }
                }
                else  // Use normal lookup
                {
                    buttonType = logicTable[lookup];
                }
            }

            (float amount, float amount2) = GetNormalisedAmountValues(_Stack[stackIndex]);

            switch (buttonType)
            {
                case ButtonType.OpType:
                    label =  $"{_Stack[stackIndex].opType}"; break;
                case ButtonType.Amount:
                    label = $"{amount}%"; break;
                case ButtonType.Amount2:
                    label = $"{amount2}%"; break;
                case ButtonType.FaceSelection:
                    label =  $"{_Stack[stackIndex].faceSelections}"; break;
            }
        }

        return (label, buttonType);
    }

    private (float, float) GetNormalisedAmountValues(VrUiPoly.ConwayOperator conwayOperator)
    {
        var config = PolyHydraEnums.OpConfigs[conwayOperator.opType];

        float rawVal = conwayOperator.amount;
        float minVal = config.amountSafeMin;
        float maxVal = config.amountSafeMax;

        float rawVal2 = conwayOperator.amount2;
        float minVal2 = config.amount2SafeMin;
        float maxVal2 = config.amount2SafeMax;
        return (
            Mathf.Floor(Mathf.InverseLerp(minVal, maxVal, rawVal) * 100f),
            Mathf.Floor(Mathf.InverseLerp(minVal2, maxVal2, rawVal2) * 100f)
        );
    }
}