using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{
    public class EditBrushPanel : BasePanel
    {

        public GameObject StrokePreview;
        private List<GameObject> ParameterWidgets;
        public GameObject CloneMaterialButton;
        public GameObject SaveButton;

        public Transform SliderPrefab;
        public Transform ColorPickerButtonPrefab;
        public Transform TexturePickerPrefab;

        [NonSerialized] public bool m_needsSaving = false;
        private List<Texture2D> m_AvailableTextures;
        private List<string> m_TextureNames;
        private List<string> m_TexturePaths;
        Stroke currentStroke;
        Stroke previousStroke;
        private Dictionary<int, string> ButtonsToTexturePaths;

        public Material PreviewMaterial
        {
            get => StrokePreview.GetComponent<MeshRenderer>().sharedMaterial;
            set => StrokePreview.GetComponent<MeshRenderer>().sharedMaterial = value;
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

        protected override void Awake()
        {
            PointerManager.m_Instance.OnMainPointerBrushChange += OnMainPointerBrushChange;
            PreviewMaterial = StrokePreview.GetComponent<MeshRenderer>().sharedMaterial;
            OnMainPointerBrushChange(PointerManager.m_Instance.MainPointer.CurrentBrush);
            RegenerateTextureLists();
            base.Awake();
        }

        private void OnDestroy()
        {
            PointerManager.m_Instance.OnMainPointerBrushChange -= OnMainPointerBrushChange;
        }

        private void OnDrawGizmosSelected()
        {
            // Debug.Log($"mesh has {StrokePreview.GetComponent<MeshFilter>().sharedMesh.vertexCount} verts and {StrokePreview.GetComponent<MeshFilter>().sharedMesh.triangles.Length} tris");
        }


        public void CloneCurrentBrush()
        {
            var oldBrush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            var textureRefs = new Dictionary<string, string>();
            if (BrushCatalog.m_Instance.IsBrushBuiltIn(oldBrush))
            {
                foreach (var texturePropertyName in oldBrush.Material.GetTexturePropertyNames())
                {
                    var tex = oldBrush.Material.GetTexture(texturePropertyName);
                    if (tex == null)
                    {
                        continue;
                    }
                    var textureName = tex.name + ".png";    // This is fine as long as we always save as pngs
                    var textureDirectory = "__Resources__"; // Placeholder to be replaced when brush is saved and textures are copied
                    textureRefs[texturePropertyName] = Path.Combine(textureDirectory, textureName);
                }
            }

            string newBrushPath = UserVariantBrush.ExportDuplicateDescriptor(oldBrush, $"{oldBrush.DurableName}Copy");

            BrushCatalog.m_Instance.UpdateCatalog(newBrushPath);
            BrushCatalog.m_Instance.HandleChangedBrushes();
            BrushDescriptor newBrush = BrushCatalog.m_Instance.AllBrushes.Last();

            var fileName = Path.Combine(UserVariantBrush.GetBrushesPath(), newBrush.UserVariantBrush.Location, "brush.cfg");
            UserVariantBrush.SaveDescriptor(newBrush, fileName, textureRefs);
            m_needsSaving = false;
            SaveButton.GetComponent<ActionButton>().SetButtonAvailable(false);

            PointerManager.m_Instance.MainPointer.SetBrush(newBrush);
            BrushCatalog.m_Instance.UpdateCatalog(newBrushPath);
            BrushCatalog.m_Instance.HandleChangedBrushes();

            var brushPanel = PanelManager.m_Instance.GetPanelByType(PanelType.Brush);
            brushPanel.GetComponentInChildren<BrushGrid>().GotoPageForBrush(newBrush);
        }

        private void AddUserTextures(string path)
        {
            var validExtensions = new List<string>
            {
                ".jpg", ".jpeg", ".png",
            };

            var filteredFiles = Directory
                .EnumerateFiles(
                    Path.Combine(Application.persistentDataPath, path),
                    "*.*",
                    SearchOption.AllDirectories)
                .Where(file => validExtensions.Any(file.ToLower().EndsWith))
                .ToList();

            for (int i = 0; i < filteredFiles.Count; i++)
            {
                var fileInfo = new FileInfo(filteredFiles[i]);
                if (!validExtensions.Contains(fileInfo.Extension.ToLower())) continue;
                string textureName = fileInfo.Name;
                TextureNames.Add(textureName);
                TexturePaths.Add(fileInfo.FullName);
                var bytes = File.ReadAllBytes(fileInfo.FullName);
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

            AddUserTextures(App.ReferenceImagePath());
            AddUserTextures(App.UserBrushesPath());
            AddResourceTextures("Brushes");
            AddResourceTextures("X/Brushes");
        }

        private void SaveEditedBrush()
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            var fileName = Path.Combine(UserVariantBrush.GetBrushesPath(), brush.UserVariantBrush.Location, "brush.cfg");
            var textureRefs = new Dictionary<string, string>();
            var btns = GetComponentsInChildren<BrushEditorTexturePickerButton>();

            for (var i = 0; i < btns.Length; i++)
            {
                var textureButton = btns[i];
                if (textureButton.TextureIndex == -1) continue;  // null texture
                var texturePropertyName = textureButton.TexturePropertyName;

                var textureFullPath = TexturePaths[textureButton.TextureIndex];
                if (!textureFullPath.StartsWith("__Resources__"))
                {
                    textureFullPath = Path.Combine(UserVariantBrush.GetBrushesPath(), textureFullPath);
                }
                textureRefs[texturePropertyName] = textureFullPath;
            }

            UserVariantBrush.SaveDescriptor(brush, fileName, textureRefs);
            m_needsSaving = false;
            SaveButton.GetComponent<ActionButton>().SetButtonAvailable(false);
        }

        public void SliderChanged(string propertyName, float value, int? vectorComponent)
        {
            // Value has already been scaled from 0..1 back to the full range
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            if (vectorComponent.HasValue)
            {
                var vecVal = PreviewMaterial.GetVector(propertyName);
                vecVal[vectorComponent.Value] = value;
                PreviewMaterial.SetVector(propertyName, vecVal);
                brush.Material.SetVector(propertyName, vecVal);
            }
            else
            {
                PreviewMaterial.SetFloat(propertyName, value);
                brush.Material.SetFloat(propertyName, value);
            }
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
                btn.UpdateValue(tex, propertyName, textureIndex);
            }
        }

        public void ColorChanged(string propertyName, Color color, BrushEditorColorPickerButton btn)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            PreviewMaterial.SetColor(propertyName, color);
            brush.Material.SetColor(propertyName, color);
        }

        private Vector2 GuessRange(float val)
        {
            // We need slider ranges even for float params
            // Make a rough guess at a plausible range based on the current value
            // TODO Find a VR friendly UI widget that doesn't need ranges
            return val == 0 ?
                new Vector2(0, 1) : // We can't guess a range for 0 so use 0..1
                new Vector2(val / 10f, val * 10);

        }

        private void OnMainPointerBrushChange(BrushDescriptor brush)
        {
            if (brush == null) return;
            // if (m_needsSaving) return;

            GeneratePreviewMesh(brush);

            // TODO - directory watcher
            if (AvailableTextures == null) RegenerateTextureLists();

            if (ParameterWidgets != null)
            {
                foreach (var widget in ParameterWidgets)
                {
                    Destroy(widget);
                }
            }
            ParameterWidgets = new List<GameObject>();
            if (BrushCatalog.m_Instance.IsBrushBuiltIn(brush))
            {
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
                ButtonsToTexturePaths = new Dictionary<int, string>();
                for (int i = 0; i < shader.GetPropertyCount(); ++i)
                {
                    string propertyName = shader.GetPropertyName(i);

                    // Skip some properties that we don't want to expose
                    if (propertyName is "_OverrideTime" or "_TimeOverrideValue" or "_TimeBlend" or "_TimeSpeed" or "_ClipStart" or "_ClipEnd")
                    {
                        continue;
                    }

                    switch (shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Float:
                            float value = brush.Material.GetFloat(propertyName);
                            // Invent a plausible range for float values
                            Vector2 range = GuessRange(value);
                            AddSlider(propertyName, value, widgetIndex, range);
                            widgetIndex++;
                            break;
                        case ShaderPropertyType.Range:
                            AddSlider(propertyName, brush.Material.GetFloat(propertyName), widgetIndex, shader.GetPropertyRangeLimits(i));
                            widgetIndex++;
                            break;
                        case ShaderPropertyType.Color:
                            AddColorPickerButton(propertyName, brush.Material.GetColor(propertyName), widgetIndex);
                            widgetIndex++;
                            break;
                        case ShaderPropertyType.Vector:
                            var v = brush.Material.GetVector(propertyName);
                            AddSlider($"{propertyName}", v.x, widgetIndex, GuessRange(v.x), 0);
                            widgetIndex++;
                            AddSlider($"{propertyName}", v.y, widgetIndex, GuessRange(v.y), 1);
                            widgetIndex++;
                            AddSlider($"{propertyName}", v.z, widgetIndex, GuessRange(v.z), 2);
                            widgetIndex++;
                            // No current shader uses the 4th component
                            // AddSlider($"{propertyName}", v.w, widgetIndex, GuessRange(v.w), 3);
                            // widgetIndex++;
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
                            ButtonsToTexturePaths[widgetIndex] = textureFullPath;
                            AddTexturePicker(propertyName, tex, widgetIndex);
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
            var previewMesh = StrokePreview.GetComponent<MeshFilter>().mesh;
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
            pos.y = initialY - (index * 0.26f);
            tr.localPosition = pos;
        }

        private void AddSlider(string propertyName, float unscaledValue, int widgetIndex, Vector2 range, int? vectorComponent=null)
        {
            var sliderTr = Instantiate(SliderPrefab, gameObject.transform, true);
            var slider = sliderTr.GetComponent<EditBrushSlider>();
            slider.ParentPanel = this;
            slider.FloatPropertyName = propertyName;
            slider.SetMin(range.x);
            slider.SetMax(range.y);
            slider.UpdateValueIgnoreParent(unscaledValue);
            slider.SetSliderPositionToReflectValue();
            slider.VectorComponent = vectorComponent;
            slider.SetDescriptionText(slider.GenerateDescription(unscaledValue));
            PositionWidgetByIndex(slider.transform, widgetIndex);
            ParameterWidgets.Add(slider.gameObject);
            slider.RegisterComponent();
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

        private void AddTexturePicker(string propertyName, Texture2D tex, int widgetIndex)
        {
            var texturePickerButtonTr = Instantiate(TexturePickerPrefab, gameObject.transform, true);
            var pickerButton = texturePickerButtonTr.GetComponent<BrushEditorTexturePickerButton>();
            pickerButton.ParentPanel = this;
            pickerButton.UpdateValue(tex, propertyName, widgetIndex);
            // textureIndex will become invalid if we refresh the texture list
            // The panel should be refreshes whenever we do this
            if (tex != null)
            {
                // TODO - how do we test for equality?
                var textureIndex = AvailableTextures.IndexOf(tex);
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
                new Vector3(-.7f, -.1f, 0),
                new Vector3(-.6f, -.1f, 0),
                new Vector3(-.5f, .2f, 0),
                Vector3.zero,
                new Vector3(.5f, -.2f, 0),
                new Vector3(.6f, -.2f, 0),
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
            StrokePreview.GetComponent<MeshRenderer>().sharedMaterial = brush.Material;
            StrokePreview.GetComponent<MeshFilter>().mesh = null; // And set it on Update

        }
    }
}
