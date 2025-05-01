using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

namespace TiltBrush
{
    [LuaDocsDescription("The user's brush")]
    [MoonSharpUserData]
    public static class BrushApiWrapper
    {
        [LuaDocsDescription("Time in seconds since the brush trigger was last pressed")]
        public static float timeSincePressed => Time.realtimeSinceStartup - InputManager.Brush.TimeBecameActive;

        [LuaDocsDescription("Time in seconds since the brush trigger was last released")]
        public static float timeSinceReleased => Time.realtimeSinceStartup - InputManager.Brush.TimeBecameInactive;

        [LuaDocsDescription("Check whether the brush trigger is currently pressed")]
        public static bool triggerIsPressed => InputManager.Brush.IsTrigger();

        [LuaDocsDescription("Check whether the brush trigger was pressed during the current frame")]
        public static bool triggerPressedThisFrame => InputManager.Brush.IsTriggerDown();

        [LuaDocsDescription("Check whether the brush trigger was released during the current frame")]
        public static bool triggerReleasedThisFrame => InputManager.Brush.BecameInactiveThisFrame;

        [LuaDocsDescription("The distance moved by the brush")]
        public static float distanceMoved => InputManager.Brush.DistanceMoved_CS;

        [LuaDocsDescription("The distance drawn by the brush (i.e. distance since the trigger was last pressed)")]
        public static float distanceDrawn => InputManager.Brush.DistanceDrawn_CS;

        [LuaDocsDescription("The 3D position of the Brush Controller's tip")]
        public static Vector3 position
        {
            get => LuaManager.Instance.GetPastBrushPos(0);
            set => PointerManager.m_Instance.MainPointer.transform.position = value;
        }

        [LuaDocsDescription("The 3D orientation of the Brush Controller's tip")]
        public static Quaternion rotation
        {
            get => LuaManager.Instance.GetPastBrushRot(0);
            set => PointerManager.m_Instance.MainPointer.transform.rotation = value;
        }

        [LuaDocsDescription("The vector representing the forward direction of the brush")]
        public static Vector3 direction => LuaManager.Instance.GetPastBrushRot(0) * Vector3.back;

        [LuaDocsDescription("The current brush size")]
        public static float size
        {
            get => PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            set => ApiMethods.BrushSizeSet(value);
        }

        [LuaDocsDescription("Brush pressure is determined by how far the trigger is pressed in")]
        public static float pressure
        {
            get => PointerManager.m_Instance.MainPointer.GetPressure();
            set => PointerManager.m_Instance.MainPointer.SetPressure(value);
        }

        [LuaDocsDescription("The current brush type")]
        public static string type
        {
            get => PointerManager.m_Instance.MainPointer.CurrentBrush.Description!;
            set => ApiMethods.Brush(value);
        }

        [LuaDocsDescription("All brush types available via the UI")]
        public static List<string> types => BrushCatalog.m_Instance.GetTagFilteredBrushList().Select(b => b.Description).ToList();

        [LuaDocsDescription("Brush types filtered by chosen tags")]
        [LuaDocsParameter("includeTags", "Include brushes that have any of these tags")]
        [LuaDocsParameter("excludeTags", "Exclude brushes that have any of these tags")]
        [LuaDocsExample("brushList = Brush:GetTypes({\"audioreactive\"}, {\"particle\"})")]
        [LuaDocsReturnValue("A filtered list of brush types")]
        public static List<string> GetTypes(List<string> includeTags, List<string> excludeTags)
        {
            return BrushCatalog.m_Instance.GetTagFilteredBrushList(includeTags, excludeTags)
                .Select(b => b.Description).ToList();
        }


        [LuaDocsDescription("How fast the brush is currently moving")]
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;

        [LuaDocsDescription("Gets or set brush color")]
        public static Color colorRgb
        {
            get => PointerManager.m_Instance.PointerColor;
            set => App.BrushColor.CurrentColor = value;
        }

        [LuaDocsDescription("Gets or set brush color using a Vector3 representing hue, saturation and brightness")]
        public static Vector3 colorHsv
        {
            get
            {
                Color.RGBToHSV(App.BrushColor.CurrentColor, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
            set => App.BrushColor.CurrentColor = Color.HSVToRGB(value.x, value.y, value.z);
        }

        [LuaDocsDescription("The color of the brush as a valid HTML color string (either hex values or a color name)")]
        public static string colorHtml
        {
            get => ColorUtility.ToHtmlStringRGB(PointerManager.m_Instance.PointerColor);
            set => ApiMethods.SetColorHTML(value);
        }

        [LuaDocsDescription("Applies the current jitter settings to the brush color")]
        [LuaDocsExample("Brush:JitterColor()")]
        public static void JitterColor() => LuaApiMethods.JitterColor();

        [LuaDocsDescription("The last color picked by the brush.")]
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;

        [LuaDocsDescription("The last color picked by the brush in HSV.")]
        public static Vector3 LastColorPickedHsv
        {
            get
            {
                Color.RGBToHSV(lastColorPicked, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }

        [LuaDocsDescription("Clears the history and sets it's size")]
        [LuaDocsExample("Brush:ResizeHistory(10)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeBrushBuffer(size);

        [LuaDocsDescription("Sets the size of the history. Only clears it if the size has changed")]
        [LuaDocsExample("Brush:SetHistorySize(10)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void SetHistorySize(int size) => LuaManager.Instance.SetBrushBufferSize(size);

        [LuaDocsDescription("Recalls previous positions of the Brush from the history buffer")]
        [LuaDocsExample("Brush:GetPastPosition(3)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        [LuaDocsReturnValue("The position of the brush during the specified frame")]
        public static Vector3 GetPastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);

        [LuaDocsDescription("Recalls previous orientations of the Brush from the history buffer")]
        [LuaDocsExample("Brush:GetPastRotation(3)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        [LuaDocsReturnValue("The rotation of the brush during the specified frame")]
        public static Quaternion GetPastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);

        [LuaDocsDescription("If set to true then the brush will draw strokes even if the trigger isn't being pressed.")]
        [LuaDocsExample("Brush:ForcePaintingOn(true)")]
        [LuaDocsParameter("active", "True means forced painting, false is normal behaviour")]
        public static void ForcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);

        [LuaDocsDescription("If set to true then the brush will stop drawing strokes even if the trigger is still pressed.")]
        [LuaDocsExample("Brush:ForcePaintingOff(true)")]
        [LuaDocsParameter("active", "True means painting is forced off, false is normal behaviour")]
        public static void ForcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

        [LuaDocsDescription("Forces the start of a new stroke - will stop painting this frame and start again the next.")]
        [LuaDocsExample("Brush:ForceNewStroke()")]
        public static void ForceNewStroke() => ApiMethods.ForceNewStroke();

        [LuaDocsDescription("The current path of the brush. Assumes a stroke is in progress.")]
        public static PathApiWrapper currentPath
        {
            get => new(PointerManager.m_Instance.MainPointer.CurrentPath);
            set => PointerManager.m_Instance.MainPointer.CurrentPath = value._Path;
        }

        private static Dictionary<ShaderPropertyType, List<string>> _GetParamsDict()
        {
            var paramsDict = new Dictionary<ShaderPropertyType, List<string>>();
            paramsDict[ShaderPropertyType.Float] = new List<string>();
            paramsDict[ShaderPropertyType.Color] = new List<string>();
            paramsDict[ShaderPropertyType.Int] = new List<string>();
            paramsDict[ShaderPropertyType.Range] = new List<string>();
            paramsDict[ShaderPropertyType.Vector] = new List<string>();
            paramsDict[ShaderPropertyType.Texture] = new List<string>();
            var brushDescriptor = ApiMethods.LookupBrushDescriptor(type);
            var shader = brushDescriptor.Material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); ++i)
            {
                string propertyName = shader.GetPropertyName(i);
                paramsDict[shader.GetPropertyType(i)].Add(shader.GetPropertyName(i));
            }
            return paramsDict;
        }

        [LuaDocsDescription("Gets a list of float property names for a brush")]
        [LuaDocsExample("Brush:GetShaderFloatParameters(\"Ink\")")]
        [LuaDocsParameter("type", "The brush name")]
        [LuaDocsReturnValue("A list of float property names usable with Stroke:SetShaderFloat")]
        public static List<string> GetShaderFloatParameters(string type)
        {
            return _GetParamsDict()[ShaderPropertyType.Float];
        }

        [LuaDocsDescription("Gets a list of color property names for a brush")]
        [LuaDocsExample("Brush:GetShaderColorParameters(\"Ink\")")]
        [LuaDocsParameter("type", "The brush name")]
        [LuaDocsReturnValue("A list of color property names usable with Stroke:SetShaderColor")]
        public static List<string> GetShaderColorParameters(string type)
        {
            return _GetParamsDict()[ShaderPropertyType.Color];
        }

        [LuaDocsDescription("Gets a list of texture property names for a brush")]
        [LuaDocsExample("Brush:GetShaderTextureParameters(\"Ink\")")]
        [LuaDocsParameter("type", "The brush name")]
        [LuaDocsReturnValue("A list of texture property names usable with Stroke:SetShaderTexture")]
        public static List<string> GetShaderTextureParameters(string type)
        {
            return _GetParamsDict()[ShaderPropertyType.Texture];
        }

        [LuaDocsDescription("Gets a list of vector property names for a brush")]
        [LuaDocsExample("Brush:GetShaderVectorParameters(\"Ink\")")]
        [LuaDocsParameter("type", "The brush name")]
        [LuaDocsReturnValue("A list of vector property names usable with Stroke:SetShaderVector")]
        public static List<string> GetShaderVectorParameters(string type)
        {
            return _GetParamsDict()[ShaderPropertyType.Vector];
        }
    }
}
