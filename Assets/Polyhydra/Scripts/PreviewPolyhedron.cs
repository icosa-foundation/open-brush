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
using System.Linq;
using System.Threading;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;
using Random = System.Random;

public class PreviewPolyhedron : MonoBehaviour
{
    public static PreviewPolyhedron m_Instance;

    public bool GenerateSubmeshes = false;

    public int RebuildSkipFrames = 4;
    public UniformTypes UniformPolyType;
    public RadialSolids.RadialPolyType RadialPolyType;
    public VariousSolidTypes VariousSolidsType;
    public ShapeTypes ShapeType;
    public GridEnums.GridTypes GridType;
    public GridEnums.GridShapes GridShape;
    public int Param1Int;
    public int Param2Int;
    public int Param3Int = 3;
    public float Param1Float;
    public float Param2Float;
    public float Param3Float = 1f;

    public bool SafeLimits;

    public PolyMesh m_PolyMesh;
    private MeshFilter meshFilter;

    public Material SymmetryWidgetMaterial;

    private PolyMesh.MeshData m_MeshData;
    private bool NeedsRebuild;

    private void Awake()
    {
        m_Instance = this;
    }

    void Start()
    {
        Operators = new List<OpDefinition>();
        Init();
        BackgroundMakePolyhedron();
    }

    public void Init()
    {
        meshFilter = gameObject.GetComponent<MeshFilter>();
    }

    void Update()
    {
        CheckAndRebuildIfNeeded();
    }

    [Serializable]
    public struct OpDefinition
    {
        public PolyMesh.Operation opType;
        public float amount;
        public bool amountRandomize;
        public float amount2;
        public bool amount2Randomize;
        public bool disabled;
        public FilterTypes filterType;
        public float filterParamFloat;
        public int filterParamInt;
        public Color paramColor;
        public bool filterNot;

        public OpDefinition ClampAmount(OpConfig config, bool safe = false)
        {
            float min = safe ? config.amountSafeMin : config.amountMin;
            float max = safe ? config.amountSafeMax : config.amountMax;
            amount = Mathf.Clamp(amount, min, max);
            return this;
        }

        public static Filter MakeFilterFromDict(Dictionary<string, object> opDict)
        {
            object filterType;
            object filterParamFloat;
            object filterParamInt;
            object filterNot;

            // Default to "All" if no filter is defined
            if (!opDict.TryGetValue("filterType", out filterType)) return Filter.All;

            opDict.TryGetValue("filterParamFloat", out filterParamFloat);
            opDict.TryGetValue("filterParamInt", out filterParamInt);
            opDict.TryGetValue("filterNot", out filterNot);

            return Filter.GetFilter(
                (FilterTypes)Convert.ToInt32(filterType),
                Convert.ToSingle(filterParamFloat),
                Convert.ToInt32(filterParamInt),
                Convert.ToBoolean(filterNot)
            );
        }

        public OpDefinition ClampAmount2(OpConfig config, bool safe = false)
        {
            float min = safe ? config.amount2SafeMin : config.amount2Min;
            float max = safe ? config.amount2SafeMax : config.amount2Max;
            amount2 = Mathf.Clamp(amount2, min, max);
            return this;
        }

        public OpDefinition ChangeAmount(float val)
        {
            amount += val;
            return this;
        }
        public OpDefinition ChangeAmount2(float val)
        {
            amount2 += val;
            return this;
        }

        public OpDefinition ChangeFilter(int val)
        {
            filterType += val;
            filterType = (FilterTypes)Mathf.Clamp(
                (int)filterType, 0, Enum.GetNames(typeof(FilterTypes)).Length - 1
            );
            return this;
        }

        public OpDefinition SetDefaultValues(OpConfig config)
        {
            amount = config.amountDefault;
            amount2 = config.amount2Default;
            return this;
        }
    }

    public List<OpDefinition> Operators;

    private Thread m_BuildMeshThread;
    private bool m_BuildMeshThreadIsFinished;
    private Coroutine m_BuildMeshCoroutine;

    public void RebuildPoly()
    {
        NeedsRebuild = true;
    }

    public void CheckAndRebuildIfNeeded()
    {
        if (!NeedsRebuild) return;
        // Don't build every frame
        if (Time.frameCount % RebuildSkipFrames == 0) return;
        Validate();
        BackgroundMakePolyhedron();
        NeedsRebuild = false;
    }

    public void Validate()
    {
        if (EditableModelManager.CurrentModel.GeneratorType == GeneratorTypes.Uniform)
        {
            if (Param1Int < 3) { Param1Int = 3; }
            if (Param1Int > 16) Param1Int = 16;
            if (Param2Int > Param1Int - 2) Param2Int = Param1Int - 2;
            if (Param2Int < 2) Param2Int = 2;
        }

        // Control the amount variables to some degree
        for (var i = 0; i < Operators.Count; i++)
        {
            if (OpConfigs.Configs == null) continue;
            var op = Operators[i];
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
            Operators[i] = op;
        }
    }

    public Color GetFaceColorForStrokes(int faceIndex)
    {
        return m_PolyMesh.CalcFaceColor(
            EditableModelManager.CurrentModel.Colors,
            EditableModelManager.CurrentModel.ColorMethod,
            faceIndex
        );
    }

    // This is a helper coroutine
    IEnumerator RunOffMainThread(Action toRun, Action callback)
    {
        if (m_BuildMeshThread != null && m_BuildMeshThread.IsAlive)
        {
            Debug.LogWarning("Waiting for existing geometry thread");
            yield break;
        }
        m_BuildMeshThreadIsFinished = false;
        m_BuildMeshThread = null;

        m_BuildMeshThread = new Thread(() =>
        {
            toRun();
            m_BuildMeshThreadIsFinished = true;
        });
        m_BuildMeshThread.Start();
        while (!m_BuildMeshThreadIsFinished)
            yield return null;
        callback();
    }

    public void BackgroundMakePolyhedron()
    {
        if (m_BuildMeshCoroutine != null)
        {
            Debug.LogWarning("Coroutine already exists. Aborting.");
            return;
        }
        m_BuildMeshCoroutine = StartCoroutine(RunOffMainThread(DoMakePolyhedron, AssignMesh));
    }

    public void ImmediateMakePolyhedron()
    {
        DoMakePolyhedron();
        AssignMesh();
    }

    private void DoMakePolyhedron()
    {
        // TODO Unify this with similar code in SaveLoadScript.cs

        switch (EditableModelManager.CurrentModel.GeneratorType)
        {
            case GeneratorTypes.Uniform:

                var wythoff = new WythoffPoly(UniformPolyType);
                m_PolyMesh = wythoff.Build();
                m_PolyMesh = m_PolyMesh.SitLevel();
                EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                {
                    {"type", UniformPolyType},
                };
                m_PolyMesh.ScalingFactor = 0.864f;
                break;
            case GeneratorTypes.Waterman:
                m_PolyMesh = WatermanPoly.Build(root: Param1Int, c: Param2Int, mergeFaces: true);
                EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                {
                    {"root", Param1Int},
                    {"c", Param2Int},
                };
                break;
            case GeneratorTypes.Grid:
                m_PolyMesh = Grids.Build(GridType, GridShape, Param1Int, Param2Int);
                EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                {
                    {"type", GridType},
                    {"shape", GridShape},
                    {"x", Param1Int},
                    {"y", Param2Int},
                };
                m_PolyMesh.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                break;
            case GeneratorTypes.Radial:
                Param1Int = Mathf.Max(Param1Int, 3);
                float height, capHeight;
                switch (RadialPolyType)
                {
                    case RadialSolids.RadialPolyType.Prism:
                    case RadialSolids.RadialPolyType.Antiprism:
                    case RadialSolids.RadialPolyType.Pyramid:
                    case RadialSolids.RadialPolyType.Dipyramid:
                    case RadialSolids.RadialPolyType.OrthoBicupola:
                    case RadialSolids.RadialPolyType.GyroBicupola:
                    case RadialSolids.RadialPolyType.Cupola:
                        height = Param2Float;
                        capHeight = Param2Float;
                        break;
                    default:
                        height = Param2Float;
                        capHeight = Param3Float;
                        break;
                }

                m_PolyMesh = RadialSolids.Build(RadialPolyType, Param1Int, height, capHeight);
                EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                {
                    {"type", RadialPolyType},
                    {"sides", Param1Int},
                    {"height", height},
                    {"capheight", capHeight},
                };
                m_PolyMesh.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                break;
            case GeneratorTypes.Shapes:
                switch (ShapeType)
                {
                    case ShapeTypes.Polygon:
                        Param1Int = Mathf.Max(Param1Int, 3);
                        m_PolyMesh = Shapes.Build(ShapeTypes.Polygon, Param1Int);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", ShapeTypes.Polygon},
                            {"sides", Param1Int},
                        };
                        // Intentionally different to radial scaling.
                        // Set so side lengths will match for any polygon
                        m_PolyMesh.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / Param1Int));
                        break;
                    case ShapeTypes.Star:
                        Param1Int = Mathf.Max(Param1Int, 3);
                        m_PolyMesh = Shapes.Build(ShapeTypes.Star, Param1Int, Param2Float);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", ShapeTypes.Star},
                            {"sides", Param1Int},
                            {"sharpness", Param2Float},
                        };
                        m_PolyMesh.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / Param1Int)); ;
                        break;
                    case ShapeTypes.L_Shape:
                        m_PolyMesh = Shapes.Build(ShapeTypes.L_Shape, Param1Float, Param2Float, Param3Float, Shapes.Method.Convex);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", ShapeTypes.L_Shape},
                            {"a", Param1Float},
                            {"b", Param2Float},
                            {"c", Param3Float},
                        };
                        break;
                    case ShapeTypes.C_Shape:
                        m_PolyMesh = Shapes.Build(ShapeTypes.C_Shape, Param1Float, Param2Float, Param3Float, Shapes.Method.Convex);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", ShapeTypes.C_Shape},
                            {"a", Param1Float},
                            {"b", Param2Float},
                            {"c", Param3Float},
                        };
                        break;
                    case ShapeTypes.H_Shape:
                        m_PolyMesh = Shapes.Build(ShapeTypes.H_Shape, Param1Float, Param2Float, Param3Float, Shapes.Method.Convex);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", ShapeTypes.H_Shape},
                            {"a", Param1Float},
                            {"b", Param2Float},
                            {"c", Param3Float},
                        };
                        break;
                }
                break;
            case GeneratorTypes.Various:
                switch (VariousSolidsType)
                {
                    case VariousSolidTypes.Box:
                        m_PolyMesh = VariousSolids.Box(Param1Int, Param2Int, Param3Int);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", VariousSolidTypes.Box},
                            {"x", Param1Int},
                            {"y", Param2Int},
                            {"z", Param3Int},
                        };
                        m_PolyMesh.ScalingFactor = 1f / Mathf.Sqrt(2f);
                        break;
                    case VariousSolidTypes.UvSphere:
                        m_PolyMesh = VariousSolids.UvSphere(Param1Int, Param2Int);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", VariousSolidTypes.UvSphere},
                            {"x", Param1Int},
                            {"y", Param2Int},
                        };
                        m_PolyMesh.ScalingFactor = 0.5f;
                        break;
                    case VariousSolidTypes.UvHemisphere:
                        m_PolyMesh = VariousSolids.UvHemisphere(Param1Int, Param2Int);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", VariousSolidTypes.UvHemisphere},
                            {"x", Param1Int},
                            {"y", Param2Int},
                        };
                        m_PolyMesh.ScalingFactor = 0.5f;
                        break;
                    case VariousSolidTypes.Torus:
                        m_PolyMesh = VariousSolids.Torus(Param1Int, Param2Int, Param3Float);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", VariousSolidTypes.Torus},
                            {"x", Param1Int},
                            {"y", Param2Int},
                            {"z", Param3Float},
                        };
                        m_PolyMesh.ScalingFactor = 1f / Mathf.Sqrt(2f);
                        break;
                    case VariousSolidTypes.Stairs:
                        m_PolyMesh = VariousSolids.Stairs(Param1Int, Param2Float, Param3Float);
                        EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                        {
                            {"type", VariousSolidTypes.Stairs},
                            {"x", Param1Int},
                            {"y", Param2Float},
                            {"z", Param3Float},
                        };
                        m_PolyMesh.ScalingFactor = 1f / Mathf.Sqrt(2f);
                        break;
                }
                break;
        }

        if (m_PolyMesh == null) Debug.LogError($"No initial poly generated for: GeneratorType: {EditableModelManager.CurrentModel.GeneratorType}");

        EditableModelManager.CurrentModel.Operations = new List<Dictionary<string, object>>();

        // EditableModelManager.CurrentModel.ColorMethod = ColorMethods.ByRole;

        foreach (var op in Operators.ToList())
        {
            // If we've set any tags then assume we want to color by tags
            // if (op.opType == PolyMesh.Operation.AddTag) EditableModelManager.CurrentModel.ColorMethod = ColorMethods.ByTags;

            EditableModelManager.CurrentModel.Operations.Add(new Dictionary<string, object>
            {
                {"operation", op.opType},
                {"param1", op.amount},
                {"param1Randomize", op.amountRandomize},
                {"param2", op.amount2},
                {"param2Randomize", op.amount2Randomize},
                {"paramColor", op.paramColor},
                {"disabled", op.disabled},
                {"filterType", op.filterType},
                {"filterParamFloat", op.filterParamFloat},
                {"filterParamInt", op.filterParamInt},
                {"filterNot", op.filterNot},
            });
            if (op.disabled || op.opType == PolyMesh.Operation.Identity) continue;
            m_PolyMesh = ApplyOp(m_PolyMesh, op);
        }

        m_MeshData = m_PolyMesh.BuildMeshData(
            GenerateSubmeshes,
            EditableModelManager.CurrentModel.Colors,
            EditableModelManager.CurrentModel.ColorMethod
        );

    }

    private void AssignMesh()
    {
        m_BuildMeshCoroutine = null;

        var mesh = m_PolyMesh.BuildUnityMesh(m_MeshData);

        if (mesh == null)
        {
            Debug.LogError($"Failed to generate preview mesh");
            return;
        }

        if (meshFilter != null)
        {
            if (Application.isPlaying)
            {
                meshFilter.mesh = mesh;
                ScalePreviewMesh();
            }
            else
            {
                meshFilter.sharedMesh = mesh;
            }
        }
    }

    private void ScalePreviewMesh()
    {
        // Scale the gameobject so the preview isn't huge or tiny
        float meshMagnitude = meshFilter.mesh.bounds.max.magnitude;
        if (meshMagnitude != 0)
        {
            transform.localScale = Vector3.one * .75f * (1f / meshMagnitude);
        }
    }

    public static PolyMesh ApplyOp(PolyMesh conway, OpDefinition op)
    {

        // Store the previous scaling factor to reapply afterwards
        float previousScalingFactor = conway.ScalingFactor;

        var _random = new Random();
        var filter = Filter.GetFilter(op.filterType, op.filterParamFloat, op.filterParamInt, op.filterNot);

        var opFunc1 = new OpFunc(_ => Mathf.Lerp(0, op.amount, (float)_random.NextDouble()));
        var opFunc2 = new OpFunc(_ => Mathf.Lerp(0, op.amount2, (float)_random.NextDouble()));

        OpParams opParams = (op.amountRandomize, op.amount2Randomize) switch
        {
            (false, false) => new OpParams(
                op.amount,
                op.amount2,
                $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                filter
            ),
            (true, false) => new OpParams(
                opFunc1,
                op.amount2,
                $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                filter
            ),
            (false, true) => new OpParams(
                op.amount,
                opFunc2,
                $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                filter
            ),
            (true, true) => new OpParams(
                opFunc1,
                opFunc2,
                $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                filter
            ),
        };

        conway = conway.AppyOperation(op.opType, opParams);

        // Reapply the original scaling factor
        conway.ScalingFactor = previousScalingFactor;

        return conway;
    }
}
