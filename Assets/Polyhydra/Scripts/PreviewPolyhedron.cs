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
using TiltBrush;
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
            try
            {
                toRun();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                m_BuildMeshThreadIsFinished = true;
            }
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
        m_BuildMeshCoroutine = StartCoroutine(RunOffMainThread(DoMakePolyHedron, AssignMesh));
    }

    public void ImmediateMakePolyhedron()
    {
        DoMakePolyHedron();
        AssignMesh();
    }

    private void DoMakePolyHedron()
    {
        PolyDefinition def = new PolyDefinition
        {
            GeneratorType = EditableModelManager.CurrentModel.GeneratorType,
            UniformPolyType = UniformPolyType,
            RadialPolyType = RadialPolyType,
            VariousSolidsType = VariousSolidsType,
            ShapeType = ShapeType,
            GridType = GridType,
            GridShape = GridShape,
            Param1Int = Param1Int,
            Param2Int = Param2Int,
            Param3Int = Param3Int,
            Param1Float = Param1Float,
            Param2Float = Param2Float,
            Param3Float = Param3Float,
            Operators = Operators,
        };
        (m_PolyMesh, m_MeshData) = PolyBuilder.BuildFromPolyDef(def);
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

        foreach (var widget in EditableModelManager.m_Instance.LinkedWidgets)
        {
            EditableModelManager.UpdateWidgetFromPolyMesh(widget, m_PolyMesh);
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
