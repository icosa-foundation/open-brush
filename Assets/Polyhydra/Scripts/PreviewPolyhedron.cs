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
using TiltBrush;
using TiltBrush.MeshEditing;
using UnityEngine;
using UnityEngine.Serialization;


public class PreviewPolyhedron : MonoBehaviour
{
    public bool GenerateSubmeshes = false;

    public GeneratorTypes GeneratorType;
    public UniformTypes UniformPolyType;
    public RotationalSolids.RotationalPolyType rotationalPolyType;
    public OtherPolyTypes OtherPolyType;
    public GridEnums.GridTypes GridType;
    public GridEnums.GridShapes GridShape;
    public int PrismP;
    public int PrismQ;

    public Gradient colors;
    public float ColorRange;
    public float ColorOffset;

    public PolyMesh _conwayPoly;
    public bool Rescale;
    private MeshFilter meshFilter;
    private Color[] previewColors;
    public Color MainColor;
    public float ColorBlend = 0.5f;
    public ColorMethods PreviewColorMethod;

    public Material SymmetryWidgetMaterial;
    private Dictionary<string, object> Parameters;

    public enum AvailableFilters
    {
        All,

        // Sides
        ThreeSided,
        FourSided,
        FiveSided,
        SixSided,
        SevenSided,
        EightSided,
        NineSided,
        TenSided,
        ElevenSided,
        TwelveSided,
        PSided,
        QSided,
        EvenSided,
        OddSided,

        // Direction
        FacingUp,
        FacingStraightUp,
        FacingDown,
        FacingStraightDown,
        FacingForward,
        FacingBackward,
        FacingStraightForward,
        FacingStraightBackward,
        FacingLevel,
        FacingCenter,
        FacingIn,
        FacingOut,

        // Role
        Ignored,
        Existing,
        New,
        NewAlt,
        AllNew,

        // Index
        Odd,
        Even,
        OnlyFirst,
        ExceptFirst,
        OnlyLast,
        ExceptLast,
        Random,

        // Edges
        Inner,
        Outer,

        // Distance or position
        TopHalf,

        // Area
        Smaller,
        Larger,

        None,
    }

    public enum MainCategories
    {
        Platonic,
        Archimedean,
        KeplerPoinsot,
        // UniformConvex,
        // UniformStar,
        Rotational,
        Waterman,
        Grids,
        Various
    }

    public enum OtherPolyTypes
    {
        Polygon,
        UvSphere,
        UvHemisphere,
        Box,

        C_Shape,
        L_Shape,
        L_Alt_Shape,
        H_Shape,
    }
    
    void Start()
    {
        Init();
    }

    void Init()
    {
        ColorSetup();
        meshFilter = gameObject.GetComponent<MeshFilter>();
        MakePolyhedron();
    }

    private void ColorSetup()
    {
        int numColors = 12; // What is the maximum colour index we want to support? 
        var colorButtons = FindObjectsOfType<CustomColorButton>();
        // Use custom colors if they are present
        if (colorButtons.Length > 1)
        {
            var colorSet = new List<Color>();
            while (colorSet.Count < numColors)
            {
                for (var i = 0; i < colorButtons.Length; i++)
                {
                    var color = colorButtons[i].CustomColor;
                    colorSet.Add(color);
                }
            }
            previewColors = colorSet.ToArray();
            // Custom color buttons are right to left so reverse them
            previewColors.Reverse();
            // Intuitively the first face role isn't often seen so move it to the end
            var firstColor = previewColors[0];
            previewColors = previewColors.Skip(1).ToArray();
            previewColors.Append(firstColor);
        }
        // Otherwise use the default palette
        else
        {
            previewColors = Enumerable.Range(0, numColors).Select(x => colors.Evaluate(((x / 8f) * ColorRange + ColorOffset) % 1)).ToArray();
        }
        previewColors = previewColors.Select(col => Color.Lerp(MainColor, col, ColorBlend)).ToArray();
    }

    public void UpdateColorBlend(float blend)
    {
        MainColor = PointerManager.m_Instance.PointerColor;
        ColorBlend = blend;
        RebuildPoly();
    }

    [Serializable]
    public struct ConwayOperator
    {
        public PolyMesh.Operation opType;
        [FormerlySerializedAs("faceSelections")] public AvailableFilters filters;
        public float amount;
        public float amount2;
        public bool disabled;

        public ConwayOperator ClampAmount(OpConfig config, bool safe = false)
        {
            float min = safe ? config.amountSafeMin : config.amountMin;
            float max = safe ? config.amountSafeMax : config.amountMax;
            amount = Mathf.Clamp(amount, min, max);
            return this;
        }

        public ConwayOperator ClampAmount2(OpConfig config, bool safe = false)
        {
            float min = safe ? config.amount2SafeMin : config.amount2Min;
            float max = safe ? config.amount2SafeMax : config.amount2Max;
            amount2 = Mathf.Clamp(amount2, min, max);
            return this;
        }

        public ConwayOperator ChangeAmount(float val)
        {
            amount += val;
            return this;
        }
        public ConwayOperator ChangeAmount2(float val)
        {
            amount2 += val;
            return this;
        }
        public ConwayOperator ChangeOpType(int val)
        {
            opType += val;
            opType = (PolyMesh.Operation)Mathf.Clamp(
                (int)opType, 1, Enum.GetNames(typeof(PolyMesh.Operation)).Length - 1
            );
            return this;
        }
        public ConwayOperator ChangeFaceSelection(int val)
        {
            filters += val;
            filters = (AvailableFilters)Mathf.Clamp(
                (int)filters, 0, Enum.GetNames(typeof(AvailableFilters)).Length - 1
            );
            return this;
        }

        public ConwayOperator SetDefaultValues(OpConfig config)
        {
            amount = config.amountDefault;
            amount2 = config.amount2Default;
            return this;
        }
    }
    public List<ConwayOperator> ConwayOperators;

    public void RebuildPoly()
    {
        Validate();
        PreviewColorMethod = (GeneratorType == GeneratorTypes.Waterman)
            ? ColorMethods.ByFaceDirection
            : ColorMethods.ByRole;
        MakePolyhedron();

        Mesh polyMesh;
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        if (Application.isPlaying)
        {
            polyMesh = GetComponent<MeshFilter>().mesh;
            meshFilter.mesh = polyMesh;
        }
        else
        {
            polyMesh = GetComponent<MeshFilter>().sharedMesh;
            meshFilter.sharedMesh = polyMesh;
        }
        PointerManager.m_Instance.SetSymmetryMode(PointerManager.m_Instance.CurrentSymmetryMode);
    }

    public void Validate()
    {
        ColorSetup();

        if (GeneratorType == GeneratorTypes.Uniform)
        {
            if (PrismP < 3) { PrismP = 3; }
            if (PrismP > 16) PrismP = 16;
            if (PrismQ > PrismP - 2) PrismQ = PrismP - 2;
            if (PrismQ < 2) PrismQ = 2;
        }

        // Control the amount variables to some degree
        for (var i = 0; i < ConwayOperators.Count; i++)
        {
            if (OpConfigs.Configs == null) continue;
            var op = ConwayOperators[i];
            if (OpConfigs.Configs[op.opType].usesAmount)
            {
                op.amount = Mathf.Round(op.amount * 1000) / 1000f;
                op.amount2 = Mathf.Round(op.amount2 * 1000) / 1000f;

                float opMin, opMax;
                if (SafeLimits)
                {
                    opMin = OpConfigs.Configs[op.opType].amountSafeMin;
                    opMax = OpConfigs.Configs[op.opType].amountSafeMax;
                }
                else
                {
                    opMin = OpConfigs.Configs[op.opType].amountMin;
                    opMax = OpConfigs.Configs[op.opType].amountMax;
                }
                if (op.amount < opMin) op.amount = opMin;
                if (op.amount > opMax) op.amount = opMax;
            }
            else
            {
                op.amount = 0;
            }
            ConwayOperators[i] = op;
        }
    }

    public bool SafeLimits;
    private PolyMesh stashed;

    public Color GetFaceColor(int faceIndex)
    {
        return _conwayPoly.CalcFaceColor(
            previewColors,
            PreviewColorMethod,
            faceIndex
        );
    }

    

    public void MakePolyhedron()
    {
        switch (GeneratorType)
        {
            case GeneratorTypes.Uniform:
                var wythoff = new WythoffPoly(UniformPolyType, PrismP, PrismQ);
                _conwayPoly = wythoff.Build();
                _conwayPoly = _conwayPoly.SitLevel();
                break;
            case GeneratorTypes.Rotational:
                _conwayPoly = RotationalSolids.Build(rotationalPolyType, PrismP);
                break;
            case GeneratorTypes.Grid:
                _conwayPoly = Grids.Build(GridType, GridShape, PrismP, PrismQ);
                break;
            case GeneratorTypes.Waterman:
                _conwayPoly = WatermanPoly.Build(1f, PrismP, PrismQ);
                break;
            case GeneratorTypes.Various:
                switch (OtherPolyType)
                {
                    case OtherPolyTypes.Box:
                        _conwayPoly = VariousSolids.Build(VariousSolidTypes.Box, PrismP, PrismP, PrismQ);
                        break;
                    case OtherPolyTypes.UvSphere:
                        _conwayPoly = VariousSolids.Build(VariousSolidTypes.UvSphere, PrismP, PrismP, PrismQ);
                        break;
                    case OtherPolyTypes.UvHemisphere:
                        _conwayPoly = VariousSolids.Build(VariousSolidTypes.UvHemisphere, PrismP, PrismP, PrismQ);
                        break;
                    case OtherPolyTypes.Polygon:
                        _conwayPoly = Shapes.Build(ShapeTypes.Polygon, PrismP);
                        break;
                }
                break;
        }
        ////_conwayPoly.basePolyhedraInfo = new PolyMesh.BasePolyhedraInfo { P = PrismP, Q = PrismQ };
        if (_conwayPoly==null) Debug.LogError($"No initial poly generated for: GeneratorType: {GeneratorType}");

        foreach (var op in ConwayOperators.ToList())
        {
            if (op.disabled || op.opType == PolyMesh.Operation.Identity) continue;
            _conwayPoly = ApplyOp(_conwayPoly, ref stashed, op);
            ////_conwayPoly.basePolyhedraInfo = new PolyMesh.BasePolyhedraInfo { P = PrismP, Q = PrismQ };
        }

        var mesh = _conwayPoly.BuildUnityMesh(GenerateSubmeshes, previewColors, PreviewColorMethod);
        if (mesh != null)
        {
            AssignFinishedMesh(mesh);
        }
        else
        {
            Debug.LogError($"Failed to generate preview mesh");
        }
    }

    public void AssignFinishedMesh(Mesh mesh)
    {

        if (Rescale)
        {
            var size = mesh.bounds.size;
            var maxDimension = Mathf.Max(size.x, size.y, size.z);
            var scale = (1f / maxDimension) * 2f;
            if (scale > 0 && scale != Mathf.Infinity)
            {
                transform.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                Debug.LogError("Failed to rescale");
            }
        }

        if (meshFilter != null)
        {
            if (Application.isPlaying) { meshFilter.mesh = mesh; }
            else { meshFilter.sharedMesh = mesh; }
        }
    }

    public static PolyMesh ApplyOp(PolyMesh conway, ref PolyMesh stash, ConwayOperator op)
    {
        //// TODO filter based on op
        conway = conway.AppyOperation(op.opType, new OpParams(op.amount, op.amount2));
        return conway;
    }
}
