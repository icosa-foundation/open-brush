using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SVGMeshUnity;
using UnityEngine;

namespace TiltBrush
{
    // ReSharper disable once UnusedType.Global
    public static class ApiMethods
    {

        public static Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint)
        {
            Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);
            float dot = Vector3.Dot(Vector3.forward, forwardVector);
            if (Math.Abs(dot - (-1.0f)) < 0.000001f)
            {
                return new Quaternion(Vector3.up.x, Vector3.up.y, Vector3.up.z, 3.1415926535897932f);
            }
            if (Math.Abs(dot - (1.0f)) < 0.000001f)
            {
                return Quaternion.identity;
            }

            float rotAngle = (float)Math.Acos(dot);
            Vector3 rotAxis = Vector3.Cross(Vector3.forward, forwardVector);
            rotAxis = Vector3.Normalize(rotAxis);
            return CreateFromAxisAngle(rotAxis, rotAngle);
        }

        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float halfAngle = angle * .5f;
            float s = (float)System.Math.Sin(halfAngle);
            Quaternion q;
            q.x = axis.x * s;
            q.y = axis.y * s;
            q.z = axis.z * s;
            q.w = (float)System.Math.Cos(halfAngle);
            return q;
        }

        private static void ChangeBrushBearing(float angle, Vector3 axis)
        {
            ApiManager.Instance.BrushRotation *= Quaternion.AngleAxis(angle, axis);
        }

        private static void ChangeSpectatorBearing(float angle, Vector3 axis)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.rotation *= Quaternion.AngleAxis(angle, axis);
        }

        // private static void ChangeUserBearing(float angle, Vector3 axis)
        // {
        //     Transform camTr = Camera.main.transform;
        //     var rot = camTr.rotation;
        //     rot *= Quaternion.AngleAxis(angle, axis);
        //     camTr.rotation = rot;
        // }

        [ApiEndpoint("draw.paths", "Draws a series of paths at the current brush position [[[x1,y1,z1],[x2,y2,z2], etc...]]. Does not move the brush position")]
        public static void DrawPaths(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var paths = JsonConvert.DeserializeObject<List<List<List<float>>>>($"[{jsonString}]");
            DrawStrokes.MultiPathsToStrokes(paths, origin);
        }

        [ApiEndpoint("draw.path", "Draws a path at the current brush position [x1,y1,z1],[x2,y2,z2], etc.... Does not move the brush position")]
        public static void DrawPath(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var path = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            DrawStrokes.SinglePathToStroke(path, origin);
        }

        [ApiEndpoint("draw.stroke", "Draws an exact brush stroke as recorded in another app")]
        public static void DrawStroke(string jsonString)
        {
            var strokeData = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            DrawStrokes.SinglePathToStroke(strokeData, Vector3.zero, rawStroke: true);
        }

        [ApiEndpoint("listenfor.strokes", "Adds the url of an app that wants to receive the data for a stroke as each one is finished")]
        public static void AddListener(string url)
        {
            ApiManager.Instance.AddOutgoingCommandListener(new Uri(url));
        }

        private static Vector3 rotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rot)
        {
            Vector3 dir = point - pivot;
            dir = rot * dir;
            point = dir + pivot;
            return point;
        }

        [ApiEndpoint("draw.polygon", "Draws a polygon at the current brush position. Does not move the brush position")]
        public static void DrawPolygon(int sides, float radius, float angle)
        {
            var path = new List<Vector3>();
            for (float i = 0; i <= sides; i++)
            {
                var theta = Mathf.PI * (i / sides) * 2f;
                theta += angle * Mathf.Deg2Rad;
                var point = new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0
                ) * radius;
                point = ApiManager.Instance.BrushRotation * point;
                path.Add(point);
            }
            DrawStrokes.PositionPathsToStroke(path, ApiManager.Instance.BrushPosition);
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
            DrawStrokes.MultiPositionPathsToStrokes(polyline2d, null, null, origin);
        }

        [ApiEndpoint("draw.svg", "Draws the path supplied as an SVG Path string at the current brush position")]
        public static void SvgPath(string svgPathString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            DrawStrokes.MultiPath2dToStrokes(svgPolyline.Polyline, origin, 0.01f, true);
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
                    brushDescriptor = BrushCatalog.m_Instance.AllBrushes
                        .First(x => x.m_Description
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("(", "")
                            .Replace(")", "")
                            .ToLower() == brushType);
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
            color = color.ToLower();
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

        [ApiEndpoint("spectator.move.to", "Moves the spectator camera to the given position")]
        public static void MoveSpectatorTo(Vector3 position)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.position = position;
        }

        [ApiEndpoint("user.move.to", "Moves the user to the given position")]
        public static void MoveUserTo(Vector3 position)
        {
            TrTransform pose = App.Scene.Pose;
            pose.translation = position;
            float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
            App.Scene.Pose = pose;
        }

        [ApiEndpoint("spectator.move.by", "Moves the spectator camera by the given amount")]
        public static void MoveSpectatorBy(Vector3 amount)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.position += amount;
        }

        [ApiEndpoint("user.move.by", "Moves the user by the given amount")]
        public static void MoveUserBy(Vector3 amount)
        {
            TrTransform pose = App.Scene.Pose;
            pose.translation -= amount;
            float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
            App.Scene.Pose = pose;
        }

        [ApiEndpoint("spectator.turn.y", "Rotates the spectator camera left or right.")]
        public static void SpectatorYaw(float angle)
        {
            ChangeSpectatorBearing(angle, Vector3.up);
        }

        [ApiEndpoint("spectator.turn.x", "Rotates the spectator camera up or down.")]
        public static void SpectatorPitch(float angle)
        {
            ChangeSpectatorBearing(angle, Vector3.left);
        }

        [ApiEndpoint("spectator.turn.z", "Tilts the angle of the spectator camera clockwise or anticlockwise.")]
        public static void SpectatorRoll(float angle)
        {
            ChangeSpectatorBearing(angle, Vector3.forward);
        }

        // [ApiEndpoint("user.turn.y", "Rotates the user camera left or right.")]
        // public static void UserYaw(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.up);
        // }
        //
        // [ApiEndpoint("user.turn.x", "Rotates the user camera up or down. (monoscopic mode only)")]
        // public static void UserPitch(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.left);
        // }
        //
        // [ApiEndpoint("user.turn.z", "Tilts the angle of the user camera clockwise or anticlockwise. (monoscopic mode only)")]
        // public static void UserRoll(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.forward);
        // }

        [ApiEndpoint("spectator.direction", "Points the spectator camera to look in the specified direction. Angles are given in x,y,z degrees")]
        public static void SpectatorDirection(Vector3 direction)
        {
            TrTransform lookPose = App.Scene.Pose;
            Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
            lookPose.rotation = qNewRotation;
            App.Scene.Pose = lookPose;
        }

        // [ApiEndpoint("user.direction", "Points the user camera to look in the specified direction. Angles are given in x,y,z degrees. (Monoscopic mode only)")]
        // public static void UserDirection(Vector3 direction)
        // {
        //     TrTransform lookPose = App.Scene.Pose;
        //     Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
        //     lookPose.rotation = qNewRotation;
        //     App.Scene.Pose = lookPose;
        // }

        [ApiEndpoint("spectator.look.at", "Points the spectator camera towards a specific point")]
        public static void SpectatorLookAt(Vector3 position)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            Quaternion qNewRotation = LookAt(cam.transform.position, position);
            cam.transform.rotation = qNewRotation;
        }

        // [ApiEndpoint("user.look.at", "Points the user camera towards a specific point (In VR this only changes the y axis. In monoscopic mode it changes all 3 axes)")]
        // public static void UserLookAt(Vector3 position)
        // {
        //     TrTransform lookPose = App.Scene.Pose;
        //     Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
        //     lookPose.rotation = qNewRotation;
        //     App.Scene.Pose = lookPose;
        // }

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
            Vector3 directionVector = ApiManager.Instance.BrushRotation * Vector3.forward;
            var newPosition = currentPosition + (directionVector * distance);
            ApiManager.Instance.BrushPosition = newPosition;
        }

        [ApiEndpoint("brush.draw", "Moves the brush forward by 'distance' and draws a line")]
        public static void BrushDraw(float distance)
        {
            Vector3 directionVector = ApiManager.Instance.BrushRotation * Vector3.forward;
            var end = directionVector * distance;
            var path = new List<List<Vector3>>
            {
                new List<Vector3>{Vector3.zero, end}
            };
            var origin = ApiManager.Instance.BrushPosition;
            DrawStrokes.MultiPositionPathsToStrokes(path, null, null, origin);
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

        [ApiEndpoint("brush.look.at", "Changes the brush direction to look at the specified point")]
        public static void BrushLookAt(Vector3 direction)
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(direction, Vector3.up);
        }

        [ApiEndpoint("brush.look.forwards", "Changes the brush direction to look forwards")]
        public static void BrushLookForwards()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.forward, Vector3.up);
        }

        [ApiEndpoint("brush.look.up", "Changes the brush direction to look upwards")]
        public static void BrushLookUp()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.up, Vector3.up);
        }

        [ApiEndpoint("brush.look.down", "Changes the brush direction to look downwards")]
        public static void BrushLookDown()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.down, Vector3.up);
        }

        [ApiEndpoint("brush.look.left", "Changes the brush direction to look to the left")]
        public static void BrushLookLeft()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.left, Vector3.up);
        }

        [ApiEndpoint("brush.look.right", "Changes the brush direction to look to the right")]
        public static void BrushLookRight()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.right, Vector3.up);
        }

        [ApiEndpoint("brush.look.backwards", "Changes the brush direction to look backwards")]
        public static void BrushLookBackwards()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.back, Vector3.up);
        }

        [ApiEndpoint("brush.home", "Resets the brush position and direction")]
        public static void BrushHome()
        {
            BrushMoveTo(ApiManager.Instance.BrushOrigin);
            ApiManager.Instance.BrushRotation = ApiManager.Instance.BrushInitialRotation;
        }

        [ApiEndpoint("brush.home.set", "Sets the current brush position and direction as the new home")]
        public static void BrushSetHome()
        {
            ApiManager.Instance.BrushOrigin = ApiManager.Instance.BrushPosition;
            ApiManager.Instance.BrushInitialRotation = ApiManager.Instance.BrushRotation;
        }

        [ApiEndpoint("brush.transform.push", "Stores the current brush position and direction on to a stack")]
        public static void BrushTransformPush()
        {
            ApiManager.Instance.BrushTransformStack.Push((ApiManager.Instance.BrushPosition, ApiManager.Instance.BrushRotation));
        }

        [ApiEndpoint("brush.transform.pop", "Pops the most recent current brush position and direction from the stack")]
        public static void BrushTransformPop()
        {
            var (pos, rot) = ApiManager.Instance.BrushTransformStack.Pop();
            BrushMoveTo(pos);
            ApiManager.Instance.BrushRotation = rot;
        }

        [ApiEndpoint("debug.brush", "Logs some info about the brush")]
        public static void DebugBrush()
        {
            Debug.Log($"Brush position: {ApiManager.Instance.BrushPosition}");
            Debug.Log($"Brush rotation: {ApiManager.Instance.BrushRotation.eulerAngles}");
        }

        [ApiEndpoint("stroke.delete", "Delete strokes by their index. If index is 0 the most recent stroke is deleted. -1 etc steps back in time")]
        public static void DeleteStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
            stroke.Uncreate();
        }

        [ApiEndpoint("stroke.select", "Selects a stroke by it's index. 0 is the most recent stroke, -1 is second to last, 1 is the first.")]
        public static void SelectStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { stroke });
        }

        [ApiEndpoint("strokes.select", "Select multiple strokes by their index. 0 is the most recent stroke, -1 is second to last, 1 is the first.")]
        public static void SelectStrokes(int start, int end)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            SelectionManager.m_Instance.SelectStrokes(strokes);
        }

        [ApiEndpoint("selection.recolor", "Recolors the currently selected strokes")]
        public static void RecolorSelection()
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, true, false, false);
            }
        }

        [ApiEndpoint("selection.rebrush", "Rebrushes the currently selected strokes")]
        public static void RebrushSelection()
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, false, true, false);
            }
        }

        [ApiEndpoint("selection.resize", "Changes the brush size the currently selected strokes")]
        public static void ResizeSelection()
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, false, false, true);
            }
        }

        [ApiEndpoint("selection.trim", "Removes a number of points from the currently selected strokes")]
        public static void TrimSelection(int count)
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                int newCount = Mathf.Max(0, stroke.m_ControlPoints.Length - count);
                if (newCount > 0)
                {
                    Array.Resize(ref stroke.m_ControlPoints, newCount);
                    stroke.Recreate(null, stroke.Canvas);
                    SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, false, false, true);
                }
                else
                {
                    SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                    stroke.Uncreate();
                }
            }
        }

        private static Vector3 QuantizePosition(Vector3 pos, Vector3 grid)
        {
            float round(float val, float res) { return Mathf.Round(val / res) * res; }
            return new Vector3(round(pos.x, grid.x), round(pos.y, grid.y), round(pos.z, grid.z));
        }

        private static void ModifyControlPoints(Func<Vector3, Vector3> func)
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                var newCPs = new List<PointerManager.ControlPoint>();
                for (var i = 0; i < stroke.m_ControlPoints.Length; i++)
                {
                    var cp = stroke.m_ControlPoints[i];
                    cp.m_Pos = func(cp.m_Pos);
                    // Only add a control point if it's pos is different to it's predecessor
                    if (i == 0 || (i > 0 && cp.m_Pos != stroke.m_ControlPoints[i - 1].m_Pos))
                    {
                        newCPs.Add(cp);
                    }
                }
                stroke.m_ControlPoints = newCPs.ToArray();
                stroke.Uncreate();
                stroke.Recreate(null, stroke.Canvas);
            }
        }

        [ApiEndpoint("selection.points.addnoise", "Moves the position of all control points in the selection using a noise function")]
        public static void PerlinNoiseSelection(string axis, Vector3 scale)
        {
            Enum.TryParse(axis.ToUpper(), out Axis _axis);
            Func<Vector3, Vector3> quantize = pos => PerlinNoisePosition(pos, scale, _axis);
            ModifyControlPoints(quantize);
        }

        private enum Axis { X, Y, Z }

        private static Vector3 PerlinNoisePosition(Vector3 pos, Vector3 scale, Axis axis)
        {
            pos = new Vector3(pos.x / scale.x, pos.y / scale.y, pos.z / scale.z);
            switch (axis)
            {
                case Axis.X:
                    pos.x += Mathf.PerlinNoise(pos.y, pos.z) * scale.x;
                    break;
                case Axis.Y:
                    pos.y += Mathf.PerlinNoise(pos.x, pos.z) * scale.y;
                    break;
                case Axis.Z:
                    pos.z += Mathf.PerlinNoise(pos.x, pos.y) * scale.z;
                    break;
            }
            return new Vector3(pos.x * scale.x, pos.y * scale.y, pos.z * scale.z);
        }

        [ApiEndpoint("selection.points.quantize", "Snaps all the points in selected strokes to a grid (buggy)")]
        public static void QuantizeSelection(Vector3 grid)
        {
            Func<Vector3, Vector3> quantize = pos => QuantizePosition(pos, grid);
            ModifyControlPoints(quantize);
        }

        [ApiEndpoint("stroke.join", "Joins a stroke with the previous one")]
        public static void JoinStroke()
        {
            var stroke1 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(0);
            var stroke2 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(-1);
            stroke2.m_ControlPoints = stroke2.m_ControlPoints.Concat(stroke1.m_ControlPoints).ToArray();
            stroke2.Uncreate();
            stroke2.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke2.m_ControlPoints.Length).ToArray();
            stroke2.Recreate(null, stroke2.Canvas);
            DeleteStroke(0);
        }

        [ApiEndpoint("strokes.join", "Joins all strokes between the two indices (inclusive)")]
        public static void JoinStrokes(int start, int end)
        {
            var strokesToJoin = SketchMemoryScript.GetStrokesBetween(start, end);
            var firstStroke = strokesToJoin[0];
            firstStroke.m_ControlPoints = strokesToJoin.SelectMany(x => x.m_ControlPoints).ToArray();
            for (int i = 1; i < strokesToJoin.Count; i++)
            {
                var stroke = strokesToJoin[i];
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
            }
            firstStroke.Uncreate();
            firstStroke.m_ControlPointsToDrop = Enumerable.Repeat(false, firstStroke.m_ControlPoints.Length).ToArray();
            firstStroke.Recreate(null, firstStroke.Canvas);
        }

        [ApiEndpoint("stroke.add", "Adds a point at the current brush position to the specified stroke")]
        public static void AddPointToStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            var prevCP = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1];
            Array.Resize(ref stroke.m_ControlPoints, stroke.m_ControlPoints.Length + 1);

            PointerManager.ControlPoint cp = new PointerManager.ControlPoint
            {
                m_Pos = ApiManager.Instance.BrushPosition,
                m_Orient = ApiManager.Instance.BrushRotation,
                m_Pressure = prevCP.m_Pressure,
                m_TimestampMs = prevCP.m_TimestampMs
            };

            stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1] = cp;
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }



        [ApiEndpoint("import.model", "Imports a model from your media libraries Models folder")]
        public static void ImportModel(string path)
        {
            path = Path.Combine(App.MediaLibraryPath(), "Models", path);
            var model = new Model(Model.Location.File(path));
            model.LoadModel();

            var tr = new TrTransform();
            tr.translation = ApiManager.Instance.BrushPosition;
            tr.rotation = ApiManager.Instance.BrushRotation;
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.ModelWidgetPrefab, tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            ModelWidget modelWidget = createCommand.Widget as ModelWidget;
            modelWidget.Model = model;
            modelWidget.Show(true);
            createCommand.SetWidgetCost(modelWidget.GetTiltMeterCost());

            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);

        }

        // Tools.

        [ApiEndpoint("tool.sketchsurface", "Activates the SketchSurface")]
        public static void ActivateSketchSurface()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SketchSurface);
        }

        [ApiEndpoint("tool.selection", "Activates the Selection Tool")]
        public static void ActivateSelection()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.Selection);
        }

        [ApiEndpoint("tool.colorpicker", "Activates the Color Picker")]
        public static void ActivateColorPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ColorPicker);
        }

        [ApiEndpoint("tool.brushpicker", "Activates the Brush Picker")]
        public static void ActivateBrushPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.BrushPicker);
        }

        [ApiEndpoint("tool.brushandcolorpicker", "Activates the Brush And Color Picker")]
        public static void ActivateBrushAndColorPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.BrushAndColorPicker);
        }

        [ApiEndpoint("tool.sketchorigin", "Activates the SketchOrigin Tool")]
        public static void ActivateSketchOrigin()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SketchOrigin);
        }

        [ApiEndpoint("tool.autogif", "Activates the AutoGif Tool")]
        public static void ActivateAutoGif()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.AutoGif);
        }

        [ApiEndpoint("tool.canvas", "Activates the Canvas Tool")]
        public static void ActivateCanvasTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CanvasTool);
        }

        [ApiEndpoint("tool.transform", "Activates the Transform Tool")]
        public static void ActivateTransformTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.TransformTool);
        }

        [ApiEndpoint("tool.stamp", "Activates the Stamp Tool")]
        public static void ActivateStampTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.StampTool);
        }

        [ApiEndpoint("tool.freepaint", "Activates the FreePaint Tool")]
        public static void ActivateFreePaintTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FreePaintTool);
        }

        [ApiEndpoint("tool.eraser", "Activates the Eraser Tool")]
        public static void ActivateEraserTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.EraserTool);
        }

        [ApiEndpoint("tool.screenshot", "Activates the Screenshot Tool")]
        public static void ActivateScreenshotTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ScreenshotTool);
        }

        [ApiEndpoint("tool.dropper", "Activates the Dropper Tool")]
        public static void ActivateDropperTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.DropperTool);
        }

        [ApiEndpoint("tool.saveicon", "Activates the SaveIcon Tool")]
        public static void ActivateSaveIconTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SaveIconTool);
        }

        [ApiEndpoint("tool.threedofviewing", "Activates the ThreeDofViewing Tool")]
        public static void ActivateThreeDofViewingTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ThreeDofViewingTool);
        }

        [ApiEndpoint("tool.multicam", "Activates the MultiCam Tool")]
        public static void ActivateMultiCamTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.MultiCamTool);
        }

        [ApiEndpoint("tool.teleport", "Activates the Teleport Tool")]
        public static void ActivateTeleportTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.TeleportTool);
        }

        [ApiEndpoint("tool.repaint", "Activates the Repaint Tool")]
        public static void ActivateRepaintTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RepaintTool);
        }

        [ApiEndpoint("tool.recolor", "Activates the Recolor Tool")]
        public static void ActivateRecolorTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RecolorTool);
        }

        [ApiEndpoint("tool.rebrush", "Activates the Rebrush Tool")]
        public static void ActivateRebrushTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RebrushTool);
        }

        [ApiEndpoint("tool.selection", "Activates the Selection Tool")]
        public static void ActivateSelectionTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
        }

        [ApiEndpoint("tool.pin", "Activates the Pin Tool")]
        public static void ActivatePinTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.PinTool);
        }

        // [ApiEndpoint("tool.empty", "Activates the Empty Tool")]
        // public static void ActivateEmptyTool()
        // {
        //     SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.EmptyTool);
        // }

        [ApiEndpoint("tool.camerapath", "Activates the CameraPath Tool")]
        public static void ActivateCameraPathTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
        }

        [ApiEndpoint("tool.fly", "Activates the Fly Tool")]
        public static void ActivateFlyTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
        }

        [ApiEndpoint("environment.type", "Sets the current environment")]
        public static void SetEnvironment(string name)
        {
            Environment env = EnvironmentCatalog.m_Instance.AllEnvironments.First(x => x.name == name);
            SceneSettings.m_Instance.SetDesiredPreset(env, false, true);
        }

    }
}
