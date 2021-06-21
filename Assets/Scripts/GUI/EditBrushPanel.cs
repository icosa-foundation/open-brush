using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Valve.Newtonsoft.Json.Utilities;
namespace TiltBrush
{
    public class EditBrushPanel : BasePanel
    {

        public GameObject StrokePreview;
        private List<GameObject> ParameterWidgets;
        public GameObject CloneMaterialButton;
        public GameObject SaveButton;

        public Transform SliderPrefab;
        public Transform VectorInputPrefab;
        public Transform ColorPickerPrefab;
        public Transform TexturePickerPrefab;

        public Material PreviewMaterial
        {
            get
            {
                return StrokePreview.GetComponent<MeshRenderer>().material;
            }
            set
            {
                StrokePreview.GetComponent<MeshRenderer>().material = value;
            }
        }
        void Awake()
        {
            PointerManager.m_Instance.OnMainPointerBrushChange += OnMainPointerBrushChange;
            // Nasty. We should construct a stroke mesh ourselves
            // var mf = StrokePreview.GetComponent<MeshFilter>();
            // mf.sharedMesh = App.ActiveCanvas.BatchManager.AllBatches().ToList()[0].GetComponent<MeshFilter>().sharedMesh;
            PreviewMaterial = StrokePreview.GetComponent<MeshRenderer>().material;
            OnMainPointerBrushChange(PointerManager.m_Instance.MainPointer.CurrentBrush);
        }

        private void OnDestroy()
        {
            PointerManager.m_Instance.OnMainPointerBrushChange -= OnMainPointerBrushChange;
        }

        public void CloneCurrentBrush()
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            UserVariantBrush.ExportDuplicateDescriptor(brush, $"{brush.DurableName}Copy");
            var newBrush = BrushCatalog.m_Instance.AllBrushes.Last();
            PointerManager.m_Instance.MainPointer.SetBrush(newBrush);
        }

        public void SaveEditedBrush()
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            // TODO - this probably doesn't work
            var fileName = Path.Combine(brush.UserVariantBrush.Location, brush.name);
            UserVariantBrush.ExportDescriptor(brush, fileName);
            UpdateSceneMaterials();
        }


        public void SliderChanged(string name, float value)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetFloat(name, value);
            // TODO - do we set this here or on save?
            // How are we handling unsaved changes?
            brush.Material.SetFloat(name, value);
        }

        private void OnMainPointerBrushChange(BrushDescriptor brush)
        {
            if (brush == null) return;
            GeneratePreviewMesh(brush);
            if (ParameterWidgets != null)
            {
                foreach (var widget in ParameterWidgets)
                {
                    Destroy(widget);
                }
            }
            ParameterWidgets = new List<GameObject>();
            if (brush.UserVariantBrush == null)
            {
                // A built in brush
                CloneMaterialButton.GetComponent<ActionButton>().SetDescriptionText($"Copy {brush.DurableName} as a new user brush");
                CloneMaterialButton.SetActive(true);
                SaveButton.SetActive(false);
            }
            else
            {
                // An editable brush
                CloneMaterialButton.SetActive(false);

                var shader = brush.Material.shader;
                int index = 0;
                for (int i = 0; i < shader.GetPropertyCount(); ++i)
                {
                    string name = shader.GetPropertyName(i);
                    switch (shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            AddSlider(name, brush.Material.GetFloat(name), index);
                            index++;
                            break;
                        case ShaderPropertyType.Color:
                            AddColorPicker(name, brush.Material.GetColor(name), index);
                            // index++;
                            break;
                        case ShaderPropertyType.Vector:
                            AddVectorInput(name, brush.Material.GetVector(name), index);
                            // index++;
                            break;
                        case ShaderPropertyType.Texture:
                            AddTexturePicker(name, brush.Material.GetTexture(name), index);
                            // index++;
                            break;
                        default:
                            Debug.LogWarning($"Brush {brush.DurableName} has property {name} of unsupported type {shader.GetPropertyType(i)}.");
                            break;
                    }
                }
                SaveButton.SetActive(true);
            }
        }

        private void PositionWidgetByIndex(Transform tr, int index)
        {
            tr.localRotation = Quaternion.identity;

            float initialY = .25f;
            var pos = tr.localPosition;
            pos.x = 0;
            pos.z = -0.075f;
            pos.y = initialY - (index * 0.25f);
            tr.localPosition = pos;
        }

        private void AddSlider(string name, float value, int index)
        {
            var sliderTr = Instantiate(SliderPrefab);
            var slider = sliderTr.GetComponent<EditBrushSlider>();
            sliderTr.parent = gameObject.transform;
            slider.ParentPanel = this;
            slider.SetDescriptionText(name);
            slider.ShaderPropertyName = name;
            slider.UpdateValue(value);
            PositionWidgetByIndex(slider.transform, index);
            ParameterWidgets.Add(slider.gameObject);
            slider.RegisterComponent();
            Debug.Log($"float param: {name} = {value}");
        }

        private void AddVectorInput(string name, Vector4 value, int index)
        {
            // var vectorInputTr = Instantiate(VectorInputPrefab);
            // var vectorInput = vectorInputTr.GetComponent<>();
            // vectorInputTr.parent = gameObject.transform;
            // vectorInput.SetDescriptionText(name);
            // vectorInput.ShaderPropertyName = name;
            // vectorInput.UpdateValue(value);
            // PositionWidgetByIndex(slider.transform, index);
            // ParameterWidgets.Add(vectorInput.gameObject);
            Debug.Log($"Vector param: {name} = {value}");
        }

        private void AddColorPicker(string name, Color value, int index)
        {
            // var colorPickerTr = Instantiate(ColorPickerPrefab);
            // var picker = colorPickerTr.GetComponent<>();
            // colorPickerTr.parent = gameObject.transform;
            // picker.SetDescriptionText(name);
            // picker.ShaderPropertyName = name;
            // picker.UpdateValue(value);
            // PositionWidgetByIndex(picker.transform, index);
            // ParameterWidgets.Add(picker.gameObject);
            Debug.Log($"Color param: {name} = {value}");
        }

        private void AddTexturePicker(string name, Texture value, int index)
        {
            // var texturePickerTr = Instantiate(ColorPickerPrefab);
            // var picker = texturePickerTr.GetComponent<>();
            // texturePickerTr.parent = gameObject.transform;
            // picker.SetDescriptionText(name);
            // picker.ShaderPropertyName = name;
            // picker.UpdateValue(value);
            // PositionWidgetByIndex(picker.transform, index);
            // ParameterWidgets.Add(picker.gameObject);
            Debug.Log($"Texture param: {name} = {value}");
        }

        private void UpdateSceneMaterials()
        {
            // TODO
            foreach (var batch in App.Scene.ActiveCanvas.BatchManager.AllBatches())
            {
            }

        }

        public void GeneratePreviewMesh(BrushDescriptor brush)
        {
            var origin = Vector3.zero;
            var scale = 2f;
            uint time = 0;
            if (brush == null) return;
            float minPressure = brush.PressureSizeMin(false);
            float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);
            var group = App.GroupManager.NewUnusedGroup();
            var path = new List<Vector3>
            {
                new Vector3(-1f, -.2f, 0),
                new Vector3(-.75f, -.1f, 0),
                new Vector3(-.5f, .2f, 0),
                Vector3.zero,
                new Vector3(.5f, -.2f, 0),
                new Vector3(1f, .2f, 0),
            };
            float lineLength = 0;
            var controlPoints = new List<PointerManager.ControlPoint>();
            for (var vertexIndex = 0; vertexIndex < path.Count - 1; vertexIndex++)
            {
                var coordList0 = path[vertexIndex];
                var vert = new Vector3(coordList0[0], coordList0[1], coordList0[2]);
                var coordList1 = path[(vertexIndex + 1) % path.Count];
                var nextVert = new Vector3(coordList1[0], coordList1[1], coordList1[2]);

                for (float step = 0; step <= 1f; step += .25f)
                {
                    controlPoints.Add(new PointerManager.ControlPoint
                    {
                        m_Pos = (vert + (nextVert - vert) * step) * scale + origin,
                        m_Orient = Quaternion.identity, //.LookRotation(face.Normal, Vector3.up),
                        m_Pressure = pressure,
                        m_TimestampMs = time++
                    });
                }

                lineLength += (nextVert - vert).magnitude; // TODO Does this need scaling? Should be in Canvas space
            }
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = App.Scene.ActiveCanvas,
                m_BrushGuid = brush.m_Guid,
                m_BrushScale = 1f,
                m_BrushSize = .5f,
                m_Color = App.BrushColor.CurrentColor,
                m_Seed = 0,
                m_ControlPoints = controlPoints.ToArray(),
            };
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Group = group;
            stroke.Recreate(null, App.Scene.ActiveCanvas);
            var mesh = stroke.m_BatchSubset.m_ParentBatch.gameObject.GetComponent<MeshFilter>().sharedMesh;
            StrokePreview.GetComponent<MeshFilter>().mesh = mesh;
            StrokePreview.GetComponent<MeshRenderer>().material = brush.Material;
            Debug.Log($"Preview mesh: {mesh.vertices.Length} verts");
            //stroke.DestroyStroke();
        }
    }
}
