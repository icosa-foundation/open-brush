using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SVGMeshUnity;
using UnityEngine;

namespace TiltBrush
{
    // ReSharper disable once UnusedType.Global
    public static class ApiMethods
    {

        private static void ChangeBrushBearing(float angle, Vector3 axis)
        {
            Vector3 newBearing = Quaternion.AngleAxis(angle, axis) * ApiManager.Instance.BrushBearing;
            ApiManager.Instance.BrushBearing = newBearing;
        }
        private static void ChangeCameraBearing(float angle, Vector3 axis)
        {
            TrTransform lookPose = App.Scene.Pose;
            Quaternion qOffsetRotation = Quaternion.AngleAxis(angle, axis);
            Quaternion qNewRotation = qOffsetRotation * lookPose.rotation;
            lookPose.rotation = qNewRotation;
            App.Scene.Pose = lookPose;
        }

        [ApiEndpoint("draw.path", "Draws a series of lines at the current brush position [[[x1,y1,z1],[x2,y2,z2], etc...]]. Does not move the brush position")]
        public static void Draw(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var jsonData = JsonConvert.DeserializeObject<List<List<List<float>>>>(jsonString);
            DrawStrokes.PathsToStrokes(jsonData, origin);
        }

        [ApiEndpoint("showfolder.scripts", "Opens the user's Scripts folder on the desktop")]
        public static void OpenUserScriptsFolder()
        {
            OpenUserFolder(ApiManager.Instance.UserScriptsPath());
        }
        
        [ApiEndpoint("showfolder.exports", "Opens the user's Exports folder on the desktop")]
        public static void OpenExportFolder()
        {
            OpenUserFolder(App.UserExportPath());
        }

        private static void OpenUserFolder(string path)
        {
            // Launch external window and tell the user we did so
            // TODO This call is windows only
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    "Folder opened on desktop", fPopScalar: 0.5f
                );
                System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
            }
        }

        [ApiEndpoint("draw.text", "Draws the characters supplied at the current brush position")]
        public static void Text(string text)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var font = Resources.Load<CHRFont>("arcade");
            var textToStroke = new TextToStrokes(font);
            var polyline2d = textToStroke.Build(text);
            DrawStrokes.PathsToStrokes(polyline2d, origin);
        }

        [ApiEndpoint("draw.svg", "Draws the path supplied as an SVG Path string at the current brush position")]
        public static void SvgPath(string svgPathString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            DrawStrokes.PathsToStrokes(svgPolyline.Polyline, origin, 0.01f, true);
        }

        [ApiEndpoint("brush.type", "Changes the brush. brushType can either be the brush name or it's guid. brushes are listed in the localhost:40074/help screen")]
        public static void Brush(string brushType)
        {
            BrushDescriptor brushDescriptor = null;
            try
            {
                var guid = new Guid(brushType);
                brushDescriptor = BrushCatalog.m_Instance.GetBrush(guid);
            }
            catch (FormatException e)
            {
            }

            if (brushDescriptor == null)
            {
                brushType = brushType.ToLower().Trim().Replace(" ", "");
                try
                {
                    brushDescriptor = BrushCatalog.m_Instance.AllBrushes.First(x => x.name.ToLower() == brushType);
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError($"No brush found called: {brushType}");
                }
            }

            if (brushDescriptor != null)
            {
                PointerManager.m_Instance.SetBrushForAllPointers(brushDescriptor);
            }
            else
            {
                Debug.LogError($"No brush found with the name or guid: {brushType}");
            }
        }

        [ApiEndpoint("color.add.hsv", "Adds the supplied values to the current color. Values are hue, saturation and value")]
        public static void AddColorHSV(Vector3 hsv)
        {
            float h, s, v;
            Color.RGBToHSV(App.BrushColor.CurrentColor, out h, out s, out v);
            App.BrushColor.CurrentColor = Color.HSVToRGB(
                (h + hsv.x) % 1f,
                (s + hsv.y) % 1f,
                (v + hsv.z) % 1f
            );
        }
        
        [ApiEndpoint("color.add.rgb", "Adds the supplied values to the current color. Values are red green and blue")]
        public static void AddColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor += new Color(rgb.x, rgb.y, rgb.z);
        }
        
        [ApiEndpoint("color.set.rgb", "Sets the current color. Values are hue, saturation and value")]
        public static void SetColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor = new Color(rgb.x, rgb.y, rgb.z);
        }
        
        [ApiEndpoint("color.set.hsv", "Sets the current color. Values are red, green and blue")]
        public static void SetColorHSV(Vector3 hsv)
        {
            App.BrushColor.CurrentColor = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
        }

        [ApiEndpoint("color.set.html", "Sets the current color. colorString can either be a hex value or a css color name.")]
        public static void SetColorHTML(string color)
        {
            Color c;
            if (CssColors.NamesToHex.ContainsKey(color)) color = CssColors.NamesToHex[color];
            if (!color.StartsWith("#")) color = $"#{color}";
            if (ColorUtility.TryParseHtmlString(color, out c))
            {
                App.BrushColor.CurrentColor = c;
            }
            else
            {
                Debug.LogError($"Invalid color: {color}");
            }
        }
        
        [ApiEndpoint("brush.size.set", "Sets the current brush size")]
        public static void BrushSizeSet(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 = size;
        }

        [ApiEndpoint("brush.size.add", "Changes the current brush size by 'amount'")]
        public static void BrushSizeAdd(float amount)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 += amount;
        }

        [ApiEndpoint("camera.move.to", "Moves the spectator or non-VR camera to the given position")]
        public static void MoveCameraTo(Vector3 position)
        {
            if (App.Config.m_SdkMode == SdkMode.Monoscopic)
            {
                TrTransform pose = App.Scene.Pose;
                pose.translation = position;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
            else
            {
                var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
                cam.transform.position = position;
            }
        }
        
        [ApiEndpoint("camera.move.by", "Moves the spectator or non-VR camera by the given amount")]
        public static void MoveCameraBy(Vector3 amount)
        {
            if (App.Config.m_SdkMode == SdkMode.Monoscopic)
            {
                TrTransform pose = App.Scene.Pose;
                pose.translation -= amount;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
            else
            {
                var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
                cam.transform.position += amount;
            }
        }

        [ApiEndpoint("camera.turn.y", "Turns the spectator or non-VR camera left or right.")]
        public static void Yaw(float angle)
        {
            ChangeCameraBearing(angle, Vector3.up);
        }

        [ApiEndpoint("camera.turn.x", "Changes the angle of the spectator or non-VR camera up or down.")]
        public static void Pitch(float angle)
        {
            ChangeCameraBearing(angle, Vector3.left);
        }

        // TODO doesn't actually make any difference at the moment
        // As we don't store orientation - only bearing.
        // [ApiEndpoint("camera.turn.z", "")]
        // public static void Roll(float angle)
        // {
        //     ChangeCameraBearing(angle, Vector3.forward);
        // }

        // TODO This should be lookat "position"
        [ApiEndpoint("camera.lookat", "Points the spectator or non-VR camera to look in the specified direction. Angles are given in x,y,z degrees")]
        public static void CameraDirection(Vector3 direction)
        {
            TrTransform lookPose = App.Scene.Pose;
            Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
            lookPose.rotation = qNewRotation;
            App.Scene.Pose = lookPose;
        }
        
        [ApiEndpoint("brush.move.to", "Moves the brush to the given coordinates")]
        public static void BrushMoveTo(Vector3 position)
        {
            ApiManager.Instance.BrushPosition = position;
        }
        
        [ApiEndpoint("brush.move.by", "Moves the brush by the given amount")]
        public static void BrushMoveBy(Vector3 offset)
        {
            ApiManager.Instance.BrushPosition += offset;
        }
        
        [ApiEndpoint("brush.move", "Moves the brush forward by 'distance' without drawing a line")]
        public static void BrushMove(float distance)
        {
            var currentPosition = ApiManager.Instance.BrushPosition;
            var directionVector = ApiManager.Instance.BrushBearing;
            var newPosition = currentPosition + (directionVector * distance);
            ApiManager.Instance.BrushPosition = newPosition;
        }

        [ApiEndpoint("brush.draw", "Moves the brush forward by 'distance' and draws a line")]
        public static void BrushDraw(float distance)
        {
            var directionVector = ApiManager.Instance.BrushBearing;
            var end = directionVector * distance;
            var path = new List<List<Vector3>>
            {
                new List<Vector3>{Vector3.zero, end}
            };
            var origin = ApiManager.Instance.BrushPosition;
            DrawStrokes.PathsToStrokes(path, origin);
            ApiManager.Instance.BrushPosition += end;
        }
        
        [ApiEndpoint("brush.turn.y", "Changes the brush direction to the left or right. Angle is measured in degrees")]
        public static void BrushYaw(float angle)
        {
            ChangeBrushBearing(angle, Vector3.up);
        }
        
        [ApiEndpoint("brush.turn.x", "Changes the brush direction up or down. Angle is measured in degrees")]
        public static void BrushPitch(float angle)
        {
            ChangeBrushBearing(angle, Vector3.left);
        }
        
        [ApiEndpoint("brush.turn.z", "Rotates the brush clockwise or anticlockwise. Angle is measured in degrees")]
        public static void BrushRoll(float angle)
        {
            ChangeBrushBearing(angle, Vector3.forward);
        }

        [ApiEndpoint("brush.lookat", "Changes the brush direction to look at the specified point")]
        public static void BrushLookAt(Vector3 direction)
        {
            ApiManager.Instance.BrushBearing = direction.normalized;
        }
    }
}
