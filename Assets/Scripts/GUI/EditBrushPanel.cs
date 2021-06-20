using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace TiltBrush
{
    class EditBrushPanel : BasePanel
    {

        public GameObject StrokePreview;
        private GameObject[] MaterialUiWidgets; 
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
                return PreviewMaterial;
            }
            set
            {
                PreviewMaterial = value;
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
            GeneratePreviewMesh();
            if (MaterialUiWidgets != null)
            {
                foreach (var widget in MaterialUiWidgets)
                {
                    Destroy(widget);
                }
            }
            MaterialUiWidgets = new GameObject[0];
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
                
                for (int i = 0; i < shader.GetPropertyCount(); ++i)
                {
                    string name = shader.GetPropertyName(i);
                    switch (shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            AddSlider(name, brush.Material.GetFloat(name));
                            break;
                        case ShaderPropertyType.Color:
                            AddColorPicker(name, brush.Material.GetColor(name));
                            break;
                        case ShaderPropertyType.Vector:
                            AddVectorInput(name, brush.Material.GetVector(name));
                            break;
                        case ShaderPropertyType.Texture:
                            AddTexturePicker(name, brush.Material.GetTexture(name));
                            break;
                        default:
                            Debug.LogWarning($"Brush {brush.DurableName} has property {name} of unsupported type {shader.GetPropertyType(i)}.");
                            break;
                    }
                }
                SaveButton.SetActive(true);
            }
        }

        private void AddSlider(string name, float value)
        {
            var sliderTr = Instantiate(SliderPrefab);
            var slider = sliderTr.GetComponent<EditBrushSlider>();
            sliderTr.parent = gameObject.transform;
            slider.SetDescriptionText(name);
            slider.ShaderPropertyName = name;
            slider.UpdateValue(value);
            Debug.Log($"float param: {name} = {value}");
        }

        private void AddVectorInput(string name, Vector4 value)
        {
            // var vectorInputTr = Instantiate(VectorInputPrefab);
            // var vectorInput = vectorInputTr.GetComponent<>();
            // vectorInputTr.parent = gameObject.transform;
            // vectorInput.SetDescriptionText(name);
            // vectorInput.ShaderPropertyName = name;
            // vectorInput.UpdateValue(value);
            Debug.Log($"Vector param: {name} = {value}");            
        }

        private void AddColorPicker(string name, Color value)
        {
            // var colorPickerTr = Instantiate(ColorPickerPrefab);
            // var picker = colorPickerTr.GetComponent<>();
            // colorPickerTr.parent = gameObject.transform;
            // picker.SetDescriptionText(name);
            // picker.ShaderPropertyName = name;
            // picker.UpdateValue(value);
            Debug.Log($"Color param: {name} = {value}");
        }

        private void AddTexturePicker(string name, Texture value)
        {
            // var texturePickerTr = Instantiate(ColorPickerPrefab);
            // var picker = texturePickerTr.GetComponent<>();
            // texturePickerTr.parent = gameObject.transform;
            // picker.SetDescriptionText(name);
            // picker.ShaderPropertyName = name;
            // picker.UpdateValue(value);
            Debug.Log($"Texture param: {name} = {value}");
        }
        
        private void UpdateSceneMaterials()
        {
            // TODO
            foreach (var batch in App.Scene.ActiveCanvas.BatchManager.AllBatches())
            {
            }

        }
        
        public void GeneratePreviewMesh()
        {
            var origin = Vector3.zero;
            var scale = 1f;
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            if (brush == null) return;
            float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
            float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);
            var group = App.GroupManager.NewUnusedGroup();
            var path = new List<Vector3>
            {
                new Vector3(-1f, -1f, 0),
                new Vector3(-.5f, 1f, 0),
                Vector3.zero,
                new Vector3(.5f, -1f, 0),
                new Vector3(1f, 1f, 0),
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
                m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                m_Color = App.BrushColor.CurrentColor,
                m_Seed = 0,
                m_ControlPoints = controlPoints.ToArray(),
            };
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Group = @group;
            stroke.Recreate(null, App.Scene.ActiveCanvas);
            var mesh = stroke.m_BatchSubset.m_ParentBatch.gameObject.GetComponent<MeshFilter>().mesh;
            StrokePreview.GetComponent<MeshFilter>().mesh = mesh;
            stroke.DestroyStroke();
        }
    }
}
