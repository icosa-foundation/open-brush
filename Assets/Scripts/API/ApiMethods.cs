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

        [ApiEndpoint("draw.path")]
        public static void Draw(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var jsonData = JsonConvert.DeserializeObject<List<List<List<float>>>>(jsonString);
            DrawStrokes.PathsToStrokes(jsonData, origin);
        }

        [ApiEndpoint("draw.text")]
        public static void Text(string text)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var font = Resources.Load<CHRFont>("arcade");
            var textToStroke = new TextToStrokes(font);
            var polyline2d = textToStroke.Build(text);
            DrawStrokes.PathsToStrokes(polyline2d, origin);
        }

        [ApiEndpoint("draw.svg")]
        public static void SvgPath(string pathString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            SVGData svgData = new SVGData();
            svgData.Path(pathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            DrawStrokes.PathsToStrokes(svgPolyline.Polyline, origin, 0.01f, true);
        }
        
        [ApiEndpoint("brush.type")]
        public static void Brush(string brushId)
        {
            BrushDescriptor brushDescriptor = null;
            try
            {
                var guid = new Guid(brushId);
                brushDescriptor = BrushCatalog.m_Instance.GetBrush(guid);
            }
            catch (FormatException e)
            {
            }

            if (brushDescriptor == null)
            {
                brushId = brushId.ToLower();
                brushDescriptor = BrushCatalog.m_Instance.AllBrushes.First(x => x.name.ToLower() == brushId);
            }
            
            if (brushDescriptor != null)
            {
                PointerManager.m_Instance.SetBrushForAllPointers(brushDescriptor);
            }
            else
            {
                Debug.LogError($"No brush found with the name or guid: {brushId}");
            }
        }

        [ApiEndpoint("brush.color.shift")]
        public static void ShiftColor(Vector3 hsv)
        {
            float h, s, v;
            Color.RGBToHSV(App.BrushColor.CurrentColor, out h, out s, out v);
            App.BrushColor.CurrentColor = Color.HSVToRGB(h + hsv.x, s + hsv.y, v + hsv.z);
        }
        
        [ApiEndpoint("brush.color")]
        public static void SetColor(string colorString)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(colorString, out color) ||
                ColorUtility.TryParseHtmlString($"#{colorString}", out color))
            {
                App.BrushColor.CurrentColor = color;
            }
        }
        
        [ApiEndpoint("brush.size")]
        public static void BrushSize(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 = size;
        }

        [ApiEndpoint("brush.enlarge")]
        public static void BrushEnlarge(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 += size;
        }
        
        [ApiEndpoint("brush.reduce")]
        public static void BrushReduce(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 -= size;
        }
        
        [ApiEndpoint("camera.teleport")]
        public static void TeleportCamera(Vector3 translation)
        {

            TrTransform pose = App.Scene.Pose;
            pose.translation -= translation;
            float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
            App.Scene.Pose = pose;
        }

        [ApiEndpoint("camera.turn")]
        [ApiEndpoint("camera.turn.y")]
        [ApiEndpoint("camera.yaw")]
        public static void Yaw(float angle)
        {
            ChangeCameraBearing(angle, Vector3.up);
        }

        [ApiEndpoint("camera.pitch")]
        [ApiEndpoint("camera.turn.x")]
        public static void Pitch(float angle)
        {
            ChangeCameraBearing(angle, Vector3.left);
        }

        // TODO doesn't actually make any difference at the moment
        // As we don't store orientation - only bearing.
        [ApiEndpoint("camera.roll")]
        [ApiEndpoint("camera.turn.z")]
        public static void Roll(float angle)
        {
            ChangeCameraBearing(angle, Vector3.forward);
        }

        [ApiEndpoint("camera.lookat")]
        public static void CameraDirection(Vector3 direction)
        {
            TrTransform lookPose = App.Scene.Pose;
            Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
            lookPose.rotation = qNewRotation;
            App.Scene.Pose = lookPose;
        }
        
        [ApiEndpoint("brush.moveto")]
        public static void BrushMoveTo(Vector3 position)
        {
            ApiManager.Instance.BrushPosition = position;
        }
        
        [ApiEndpoint("brush.moveby")]
        public static void BrushMoveBy(Vector3 offset)
        {
            ApiManager.Instance.BrushPosition += offset;
        }
        
        [ApiEndpoint("brush.move")]
        public static void BrushMove(float distance)
        {
            var currentPosition = ApiManager.Instance.BrushPosition;
            var directionVector = ApiManager.Instance.BrushBearing;
            var newPosition = currentPosition + (directionVector * distance);
            ApiManager.Instance.BrushPosition = newPosition;
        }

        [ApiEndpoint("brush.draw")]
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
        
        [ApiEndpoint("brush.turn")]
        [ApiEndpoint("brush.turn.y")]
        [ApiEndpoint("brush.yaw")]
        public static void BrushYaw(float angle)
        {
            ChangeBrushBearing(angle, Vector3.up);
        }
        
        [ApiEndpoint("brush.pitch")]
        [ApiEndpoint("brush.turn.x")]
        public static void BrushPitch(float angle)
        {
            ChangeBrushBearing(angle, Vector3.left);
        }
        
        [ApiEndpoint("brush.roll")]
        [ApiEndpoint("brush.turn.z")]
        public static void BrushRoll(float angle)
        {
            ChangeBrushBearing(angle, Vector3.forward);
        }

        [ApiEndpoint("brush.lookat")]
        public static void BrushLookAt(Vector3 direction)
        {
            ApiManager.Instance.BrushBearing = direction.normalized;
        }
    }
}
