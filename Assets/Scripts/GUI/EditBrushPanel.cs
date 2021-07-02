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
        Stroke currentStroke;
        Stroke previousStroke;
        
        public Material PreviewMaterial
        {
            get => StrokePreview.GetComponent<MeshRenderer>().material;
            set => StrokePreview.GetComponent<MeshRenderer>().material = value;
        }
        
        public List<Texture2D> AvailableTextures
        {
            get
            {
                if (m_AvailableTextures == null) RegenerateTextureLists();
                return m_AvailableTextures;
            }
            set => m_AvailableTextures = value;
        }
        public List<string> TextureNames
        {
            get => m_TextureNames;
            set => m_TextureNames = value;
        }
        public List<string> TexturePaths
        {
            get => m_TexturePaths;
            set => m_TexturePaths = value;
        }

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

        private void OnDrawGizmosSelected()
        {
            // Debug.Log($"mesh has {StrokePreview.GetComponent<MeshFilter>().sharedMesh.vertexCount} verts and {StrokePreview.GetComponent<MeshFilter>().sharedMesh.triangles.Length} tris");
        }

        private Dictionary<string, string> TextureRefsFromPanelWidgets()
        {
            var textureRefs = new Dictionary<string, string>();
            var btns = GetComponentsInChildren<BrushEditorTexturePickerButton>();
            foreach (var textureButton in btns)
            {
                var texturePropertyName = textureButton.TexturePropertyName;
                var textureFullPath = textureButton.TexturePath;
                if (!textureFullPath.StartsWith("__Resources__"))
                {
                    textureFullPath = Path.Combine(UserVariantBrush.GetBrushesPath(), textureFullPath);
                }
                textureRefs[texturePropertyName] = textureFullPath;
            }
            return textureRefs;
        }

        private Dictionary<string, string> TextureRefsFromMaterial(Material material)
        {
            var textureRefs = new Dictionary<string, string>();
            foreach (var texturePropertyName in material.GetTexturePropertyNames())
            {
                var tex = material.GetTexture(texturePropertyName);
                if (tex == null)
                {
                    Debug.LogWarning($"No texture for {texturePropertyName}");
                    continue;
                }
                var textureName = tex.name + ".png";  // This is fine as long as we always save as pngs
                var textureDirectory = "__Resources__"; // Texture will be saved and path will be replaced later
                textureRefs[texturePropertyName] = Path.Combine(textureDirectory, textureName);
            }
            return textureRefs;
        }

        public void CloneCurrentBrush()
        {
            var oldBrush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            var textureRefs = TextureRefsFromMaterial(oldBrush.Material);
            
            string newBrushPath = UserVariantBrush.ExportDuplicateDescriptor(oldBrush, $"{oldBrush.DurableName}Copy");
            
            BrushCatalog.m_Instance.UpdateCatalog(newBrushPath);
            BrushCatalog.m_Instance.HandleChangedBrushes();
            BrushDescriptor newBrush = BrushCatalog.m_Instance.AllBrushes.Last();
            SaveNewBrush(newBrush, textureRefs);
            
            // BrushCatalog.m_Instance.UpdateCatalog(newBrushPath);
            // BrushCatalog.m_Instance.HandleChangedBrushes();
            // newBrush = BrushCatalog.m_Instance.AllBrushes.Last();

            PointerManager.m_Instance.MainPointer.SetBrush(newBrush);
        }

        private void AddUserTextures(string path)
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
                string textureName = fileinfo[i].Name;
                TextureNames.Add(textureName);
                TexturePaths.Add(fileinfo[i].FullName);
                var bytes = File.ReadAllBytes(fileinfo[i].FullName);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                AvailableTextures.Add(tex);
            }
        }
        
        private void AddResourceTextures(string resourcePath)
        {
            var brushTextures = Resources.LoadAll<Texture2D>(resourcePath);
            
            foreach (var tex in brushTextures)
            {
                string textureName = tex.name;
                if (textureName == "buttonimage") continue;
                if (textureName.Contains("icon")) continue;  // This sucks. Need a better way to exclude built-in brush icons
                if (!tex.isReadable)
                {
                    Debug.LogWarning($"Please make texture: {textureName} readable");
                    continue;
                }
                TextureNames.Add(textureName);
                TexturePaths.Add(Path.Combine("__Resources__", textureName + ".png"));
                AvailableTextures.Add(tex);
            }
        }
        
        private void RegenerateTextureLists()
        {
            AvailableTextures = new List<Texture2D>();
            TextureNames = new List<string>();
            TexturePaths = new List<string>();

            AddUserTextures(App.MediaLibraryPath());
            AddUserTextures(App.UserBrushesPath());
            AddResourceTextures("Brushes");
            AddResourceTextures("X/Brushes");
        }

        private void SaveNewBrush(BrushDescriptor newBrush, Dictionary<string, string> textureRefs)
        {
            var fileName = Path.Combine(UserVariantBrush.GetBrushesPath(), newBrush.UserVariantBrush.Location, "brush.cfg");
            UserVariantBrush.SaveDescriptor(newBrush, fileName, textureRefs);
            UpdateSceneMaterials();
            m_needsSaving = false;
            SaveButton.GetComponent<ActionButton>().SetButtonAvailable(false);
        }

        private void SaveEditedBrush()
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            var fileName = Path.Combine(UserVariantBrush.GetBrushesPath(), brush.UserVariantBrush.Location, "brush.cfg");
            var textureRefs = TextureRefsFromPanelWidgets();
            UserVariantBrush.SaveDescriptor(brush, fileName, textureRefs);
            UpdateSceneMaterials();
            m_needsSaving = false;
            SaveButton.GetComponent<ActionButton>().SetButtonAvailable(false);
        }

        public void SliderChanged(string propertyName, float value)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetFloat(propertyName, value);
            brush.Material.SetFloat(propertyName, value);
        }
        
        public void TextureChanged(string propertyName, int textureIndex, BrushEditorTexturePickerButton btn)
        {
            if (textureIndex >= 0)
            {
                var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
                var tex = AvailableTextures[textureIndex];
                var texName = TextureNames[textureIndex];
                var textureFullPath = TexturePaths[textureIndex];
                PreviewMaterial.SetTexture(propertyName, tex);
                brush.Material.SetTexture(propertyName, tex);
                btn.UpdateValue(tex, propertyName, textureFullPath);
            }
        }

        public void ColorChanged(string propertyName, Color color, BrushEditorColorPickerButton btn)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetColor(propertyName, color);
            brush.Material.SetColor(propertyName, color);
        }

        private void OnMainPointerBrushChange(BrushDescriptor brush)
        {
            Debug.Log($"starting OnMainPointerBrushChange");
            if (brush == null) return;
            // if (m_needsSaving) return;
            
            Debug.Log($"OnMainPointerBrushChange");
            GeneratePreviewMesh(brush);
            if (AvailableTextures == null) RegenerateTextureLists();
            
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
                            string textureName = "";
                            string textureFullPath = "";
                            if (tex != null)
                            {
                                textureName = tex.name + ".png"; // TODO - Really? what about jpgs? how do we track the original texture filename?
                                textureFullPath = Path.Combine(
                                    brush.UserVariantBrush.Location,
                                    textureName
                                );
                            }
                            AddTexturePicker(propertyName, tex, widgetIndex, textureName, textureFullPath);
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

        void Update()
        {
            BaseUpdate();
            var previewMesh = StrokePreview.GetComponent<MeshFilter>().sharedMesh;
            if (currentStroke!= null && (previewMesh == null || previewMesh.vertexCount < 3))
            {
                try
                {
                    StrokePreview.GetComponent<MeshFilter>().sharedMesh = currentStroke.m_BatchSubset.m_ParentBatch.gameObject.GetComponent<MeshFilter>().sharedMesh;
                }
                catch(Exception e)
                {
                    // TODO it's harmless NREs that fix themselves next call but should probably still do something better here.
                }
            }
        }

        private void PositionWidgetByIndex(Transform tr, int index)
        {
            tr.localRotation = Quaternion.identity;

            float initialY = .25f;
            var pos = tr.localPosition;
            pos.x = 0;
            pos.z = -0.075f;
            pos.y = initialY - (index * 0.27f);
            tr.localPosition = pos;
        }

        private void AddSlider(string propertyName, float value, int widgetIndex)
        {
            var sliderTr = Instantiate(SliderPrefab, gameObject.transform, true);
            var slider = sliderTr.GetComponent<EditBrushSlider>();
            slider.ParentPanel = this;
            slider.SetDescriptionText(propertyName);
            slider.FloatPropertyName = propertyName;
            slider.UpdateValue(value);
            slider.SetSliderPositionToReflectValue();
            PositionWidgetByIndex(slider.transform, widgetIndex);
            ParameterWidgets.Add(slider.gameObject);
            slider.RegisterComponent();
        }

        private void AddVectorInput(string propertyName, Vector4 value, int widgetIndex)
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
            Debug.Log($"Vector param: {propertyName} = {value}");
        }

        private void AddColorPickerButton(string propertyName, Color color, int widgetIndex)
        {
            var colorPickerButtonTr = Instantiate(ColorPickerButtonPrefab, gameObject.transform, true);
            var pickerButton = colorPickerButtonTr.GetComponent<BrushEditorColorPickerButton>();
            pickerButton.ParentPanel = this;
            pickerButton.SetDescriptionText(propertyName);
            pickerButton.ChosenColor = color;
            pickerButton.ColorPropertyName = propertyName;
            PositionWidgetByIndex(pickerButton.transform, widgetIndex);
            ParameterWidgets.Add(pickerButton.gameObject);
            pickerButton.RegisterComponent();
        }

        private void AddTexturePicker(string propertyName, Texture2D tex, int widgetIndex, string textureName, string textureFullPath)
        {
            var texturePickerButtonTr = Instantiate(TexturePickerPrefab, gameObject.transform, true);
            var pickerButton = texturePickerButtonTr.GetComponent<BrushEditorTexturePickerButton>();
            pickerButton.ParentPanel = this;
            pickerButton.UpdateValue(tex, propertyName, textureFullPath);
            // textureIndex will become invalid if we refresh the texture list
            // The panel should be refreshes whenever we do this
            if (tex != null)
            {
                // TODO - how do we test for equality?
                int textureIndex = AvailableTextures.IndexOf(x => (x.name == tex.name));
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
            Debug.Log($"GeneratePreviewMesh");
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
            }
            
            currentStroke = new Stroke
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
            currentStroke.m_ControlPointsToDrop = Enumerable.Repeat(false, currentStroke.m_ControlPoints.Length).ToArray();
            currentStroke.Group = group;
            currentStroke.Recreate(null, App.Scene.ActiveCanvas);
            
            // TODO 
            // Cheat and keep a stroke history as stroke geometry isn't created immediately
            // so destroying the current stroke destroys the preview stroke mesh
            // if (previousStroke != null) previousStroke.DestroyStroke();
            
            previousStroke = currentStroke;
            StrokePreview.GetComponent<MeshRenderer>().material = brush.Material;
            StrokePreview.GetComponent<MeshFilter>().sharedMesh = null; // And set it on Update

        }
    }
}
