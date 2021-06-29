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
        public Transform ColorPickerButtonPrefab;
        public Transform TexturePickerPrefab;

        [NonSerialized] public bool m_needsSaving = false;
        private List<Texture2D> m_AvailableTextures;
        private List<string> m_TextureNames;
        private List<string> m_TexturePaths;

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
        public List<Texture2D> AvailableTextures
        {
            get
            {
                if (m_AvailableTextures == null) RegenerateTextureLists();
                return m_AvailableTextures;
            }
        }
        public List<string> TextureNames => m_TextureNames;
        public List<string> TexturePaths => m_TexturePaths;
        
        void Awake()
        {
            PointerManager.m_Instance.OnMainPointerBrushChange += OnMainPointerBrushChange;
            PreviewMaterial = StrokePreview.GetComponent<MeshRenderer>().material;
            OnMainPointerBrushChange(PointerManager.m_Instance.MainPointer.CurrentBrush);
            RegenerateTextureLists();
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

        public void AddUserTextures(string path)
        {
            var validExtensions = new List<string>
            {
                "jpg", "jpeg", "png",
            };

            path= Path.Combine(Application.persistentDataPath, path);
            DirectoryInfo dataDir = new DirectoryInfo(path);
            FileInfo[] fileinfo = dataDir.GetFiles();
            
            for (int i = 0; i < fileinfo.Length; i++)
            {
                if (!validExtensions.Contains(fileinfo[i].Extension.ToLower())) continue;
                string name = fileinfo[i].Name;
                TextureNames.Add(name);
                TexturePaths.Add(fileinfo[i].FullName);
                var bytes = File.ReadAllBytes(fileinfo[i].FullName);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                AvailableTextures.Add(tex);
            }
        }
        
        private void AddResourceTextures(string path)
        {
            var brushTextures = Resources.LoadAll<Texture2D>(path);
            
            foreach (var tex in brushTextures)
            {
                string name = tex.name;
                TextureNames.Add(name);
                TexturePaths.Add(Path.Combine(path, name));
                AvailableTextures.Add(tex);
            }
        }
        
        public void RegenerateTextureLists()
        {
            m_AvailableTextures = new List<Texture2D>();
            m_TextureNames = new List<string>();
            m_TexturePaths = new List<string>();

            AddUserTextures(App.MediaLibraryPath());
            AddUserTextures(App.UserBrushesPath());
            AddResourceTextures("Brushes");
            AddResourceTextures("X/Brushes");
        }

        public void SaveEditedBrush()
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            // TODO - this probably doesn't work
            var fileName = Path.Combine(brush.UserVariantBrush.Location, brush.name);
            UserVariantBrush.ExportDescriptor(brush, fileName);
            UpdateSceneMaterials();
            m_needsSaving = false;
            SaveButton.GetComponent<ActionButton>().SetButtonAvailable(false);
        }

        public void SliderChanged(string propertyName, float value)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetFloat(propertyName, value);
        }
        
        public void TextureChanged(string propertyName, int textureIndex)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            if (textureIndex >= 0)
            {
                var tex = AvailableTextures[textureIndex];
                PreviewMaterial.SetTexture(propertyName, tex);
            }
        }

        public void ColorChanged(string propertyName, Color color)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetColor(propertyName, color);
            brush.Material.SetColor(propertyName, color);
        }

        private void OnMainPointerBrushChange(BrushDescriptor brush)
        {
            if (brush == null) return;
            if (m_needsSaving) return;
            
            GeneratePreviewMesh(brush);
            if (m_AvailableTextures == null) RegenerateTextureLists();
            
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
                int widgetIndex = 0;
                for (int i = 0; i < shader.GetPropertyCount(); ++i)
                {
                    string propertyName = shader.GetPropertyName(i);
                    switch (shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            AddSlider(propertyName, brush.Material.GetFloat(propertyName), widgetIndex);
                            widgetIndex++;
                            break;
                        case ShaderPropertyType.Color:
                            AddColorPickerButton(propertyName, brush.Material.GetColor(propertyName), widgetIndex);
                            widgetIndex++;
                            break;
                        case ShaderPropertyType.Vector:
                            AddVectorInput(propertyName, brush.Material.GetVector(propertyName), widgetIndex);
                            // index++;
                            break;
                        case ShaderPropertyType.Texture:
                            var tex = (Texture2D)brush.Material.GetTexture(propertyName);
                            var textureName = brush.UserVariantBrush.GetFilenameForTextureProperty(propertyName);
                            string texturePath = Path.Combine(
                                brush.UserVariantBrush.Location,
                                textureName
                            );
                            AddTexturePicker(propertyName, tex, widgetIndex, textureName, texturePath);
                            widgetIndex++;
                            break;
                        default:
                            Debug.LogWarning($"Brush {brush.DurableName} has property {propertyName} of unsupported type {shader.GetPropertyType(i)}.");
                            break;
                    }
                }
                m_needsSaving = true;
                SaveButton.GetComponent<ActionButton>().SetButtonAvailable(true);
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

        private void AddSlider(string name, float value, int widgetIndex)
        {
            var sliderTr = Instantiate(SliderPrefab);
            var slider = sliderTr.GetComponent<EditBrushSlider>();
            sliderTr.parent = gameObject.transform;
            slider.ParentPanel = this;
            slider.SetDescriptionText(name);
            slider.FloatPropertyName = name;
            slider.UpdateValue(value);
            PositionWidgetByIndex(slider.transform, widgetIndex);
            ParameterWidgets.Add(slider.gameObject);
            slider.RegisterComponent();
        }

        private void AddVectorInput(string name, Vector4 value, int widgetIndex)
        {
            // TODO
            // var vectorInputTr = Instantiate(VectorInputPrefab);
            // var vectorInput = vectorInputTr.GetComponent<>();
            // vectorInputTr.parent = gameObject.transform;
            // vectorInput.ParentPanel = this;
            // vectorInput.SetDescriptionText(name);
            // vectorInput.ShaderPropertyName = name;
            // vectorInput.UpdateValue(value);
            // PositionWidgetByIndex(slider.transform, index);
            // ParameterWidgets.Add(vectorInput.gameObject);
            Debug.Log($"Vector param: {name} = {value}");
        }

        private void AddColorPickerButton(string name, Color color, int widgetIndex)
        {
            var colorPickerButtonTr = Instantiate(ColorPickerButtonPrefab);
            var pickerButton = colorPickerButtonTr.GetComponent<BrushEditorColorPickerButton>();
            colorPickerButtonTr.parent = gameObject.transform;
            pickerButton.ParentPanel = this;
            pickerButton.SetDescriptionText(name);
            pickerButton.ColorPropertyName = name;
            pickerButton.SetColor(color);
            PositionWidgetByIndex(pickerButton.transform, widgetIndex);
            ParameterWidgets.Add(pickerButton.gameObject);
            pickerButton.RegisterComponent();
        }

        private void AddTexturePicker(string propertyName, Texture2D tex, int widgetIndex, string textureName, string texturePath)
        {
            var texturePickerButtonTr = Instantiate(TexturePickerPrefab);
            var pickerButton = texturePickerButtonTr.GetComponent<BrushEditorTexturePickerButton>();
            texturePickerButtonTr.parent = gameObject.transform;
            pickerButton.ParentPanel = this;
            pickerButton.SetDescriptionText(propertyName);
            pickerButton.TexturePropertyName = propertyName;
            pickerButton.SetPreset(tex, textureName, texturePath);
            // textureIndex will become invalid if we refresh the texture list
            // The panel should be refreshes whenever we do this
            if (tex != null)
            {
                // TODO - how do we test for equality?
                int textureIndex = m_AvailableTextures.IndexOf(x => (x.name == tex.name));
                pickerButton.TextureIndex = textureIndex;
            }
            else
            {
                pickerButton.TextureIndex = -1;
            }
            PositionWidgetByIndex(pickerButton.transform, widgetIndex);
            ParameterWidgets.Add(pickerButton.gameObject);
            pickerButton.RegisterComponent();
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
            
            // TODO 
            StrokePreview.GetComponent<MeshRenderer>().material = brush.Material;
            
            // TODO - how do we clean up as this breaks the preview mesh
            //stroke.DestroyStroke();
        }
    }
}
