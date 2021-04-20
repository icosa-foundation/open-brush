#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using SVGMeshUnity;
using UnityEngine;


namespace TiltBrush
{
  public partial class App {

    private Queue m_RequestedCommandQueue = Queue.Synchronized(new Queue());

    Vector3 stringToVec(string s) {
    string[] temp = s.Split(',');
      return new Vector3 (float.Parse(temp[0]), float.Parse(temp[1]), float.Parse(temp[2]));
    }

    public string[] GetApiUrlPaths() {
      return new[]
      {
         "/api/v1/draw",
         "/api/v1/svgpath",
         "/api/v1/brush",
         "/api/v1/color",
         "/api/v1/text",
         "/api/v1/teleport",
         "/api/v1/roll",
         "/api/v1/pitch",
         "/api/v1/yaw",
         "/api/v1/turn",
      };
    }

    string ApiCommandCallback(HttpListenerRequest request) {

      string[] urlParts = request.Url.Segments;
      if (urlParts.Length!=4 || urlParts[1] != "api/" || urlParts[2] != "v1/") {
        return null; // TODO Status codes
      }

      KeyValuePair<string, string> command;
      string paramString = null;

      if (request.HasEntityBody) {
        using (Stream body = request.InputStream) {
          using (var reader = new StreamReader(body, request.ContentEncoding))
          {
            var parts = Uri.UnescapeDataString(reader.ReadToEnd()).Split(new[] {'='}, 2);
            paramString = parts[1].Replace("+", " ");
          }
        }
      }

      if (string.IsNullOrEmpty(paramString)) {
        if (request.Url.Query.Length > 1) {
          paramString = Uri.UnescapeDataString(request.Url.Query.Substring(1));
        }
      }

      if (!string.IsNullOrEmpty(paramString)) {
        command = new KeyValuePair<string, string>(urlParts[3].TrimEnd('/'), paramString);
        m_RequestedCommandQueue.Enqueue(command);
        return "OK";
      }
      return null;  // TODO Status codes
    }
    
    private void HandleApiCommand() {
      
      KeyValuePair<string, string> command;
      try
      {
        command = (KeyValuePair<string, string>) m_RequestedCommandQueue.Dequeue();
      }
      catch (InvalidOperationException)
      {
        return;
      }

      if (string.IsNullOrEmpty(command.Value)) return;

      switch (command.Key)
      {
        case "draw":
          var jsonData = JsonConvert.DeserializeObject<List<List<List<float>>>>(command.Value);
          PathsToStrokes(jsonData);
          break;
        case "text":
          var font = Resources.Load<CHRFont>("arcade");
          var textToStroke = new TextToStrokes(font);
          var polyline2d = textToStroke.Build(command.Value);
          PathsToStrokes(polyline2d);
          break;
        case "svgpath":
          SVGData svgData = new SVGData();
          svgData.Path(command.Value);
          SVGPolyline svgPolyline = new SVGPolyline();
          svgPolyline.Fill(svgData);
          PathsToStrokes(svgPolyline.Polyline, 0.01f, true);
          break;
        case "brush":
          var brushId = command.Value;
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

          PointerManager.m_Instance.SetBrushForAllPointers(brushDescriptor);
          break;
        case "color":
          Color color;
          if (ColorUtility.TryParseHtmlString(command.Value, out color) ||
              ColorUtility.TryParseHtmlString($"#{command.Value}", out color))
          {
            BrushColor.CurrentColor = color;
          }

          break;

        case "teleport":

          TrTransform pose = Scene.Pose;
          pose.translation -= stringToVec(command.Value);
          float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
          pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
          App.Scene.Pose = pose;
          break;

        case "yaw":
        case "turn":
          TurnBy(float.Parse(command.Value), Vector3.up);
          break;

        case "pitch":
          TurnBy(float.Parse(command.Value), Vector3.left);
          break;

        case "roll":
          TurnBy(float.Parse(command.Value), Vector3.forward);
          break;

        case "lookdirection":
          TrTransform lookPose = Scene.Pose;
          var euler = stringToVec(command.Value);
          Quaternion qNewRotation = Quaternion.Euler(euler.x, euler.y, euler.z);
          lookPose.rotation = qNewRotation;
          Scene.Pose = lookPose;
          break;
      }
    }

  private void TurnBy(float angle, Vector3 axis) {
    TrTransform lookPose = Scene.Pose;
    Quaternion qOffsetRotation = Quaternion.AngleAxis(angle, axis);
    Quaternion qNewRotation = qOffsetRotation * lookPose.rotation;
    lookPose.rotation = qNewRotation;
    Scene.Pose = lookPose;
  }

  private static void PathsToStrokes(List<List<List<float>>> floatPaths, float scale = 1f)
  {
    var paths = new List<List<Vector3>>();
    foreach (List<List<float>> positionList in floatPaths)
    {
      var path = new List<Vector3>();
      foreach (List<float> position in positionList)
      {
        path.Add(new Vector3(position[0], position[1], position[2]));
      }
      paths.Add(path);
    }
    PathsToStrokes(paths, scale);
  }

  private static void PathsToStrokes(List<List<Vector2>> polyline2d, float scale = 1f, bool breakOnOrigin=false)
  {
    var paths = new List<List<Vector3>>();
    foreach (List<Vector2> positionList in polyline2d)
    {
      var path = new List<Vector3>();
      foreach (Vector2 position in positionList)
      {
        path.Add(new Vector3(position.x, position.y, 0));
      }
      paths.Add(path);
    }
    PathsToStrokes(paths, scale, breakOnOrigin);
  }

  private static void PathsToStrokes(List<List<Vector3>> paths, float scale = 1f, bool breakOnOrigin=false)
  {
    Vector3 pos = Vector3.zero;
    var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
    uint time = 0;
    float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
    float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);

    var strokes = new List<Stroke>();
    foreach (var path in paths)
    {
      if (path.Count < 2) continue;
      float lineLength = 0;
      var controlPoints = new List<PointerManager.ControlPoint>();
      for (var vertexIndex = 0; vertexIndex < path.Count - 1; vertexIndex++)
      {
        var coordList0 = path[vertexIndex];
        var vert = new Vector3(coordList0[0], coordList0[1], coordList0[2]) * scale;
        var coordList1 = path[(vertexIndex + 1) % path.Count];
        // Fix for trailing zeros from SVG.
        // TODO Find out why and fix it properly
        if (breakOnOrigin && coordList1 == Vector3.zero)
        {
          break;
        }
        var nextVert = new Vector3(coordList1[0], coordList1[1], coordList1[2]) * scale;
        for (float step = 0; step <= 1f; step += .25f)
        {
          controlPoints.Add(new PointerManager.ControlPoint
          {
            m_Pos = pos + vert + ((nextVert - vert) * step),
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
        m_IntendedCanvas = Scene.ActiveCanvas,
        m_BrushGuid = brush.m_Guid,
        m_BrushScale = 1f,
        m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
        m_Color = BrushColor.CurrentColor,
        m_Seed = 0,
        m_ControlPoints = controlPoints.ToArray(),
      };
      stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
      stroke.Uncreate();
      stroke.Recreate(null, Scene.ActiveCanvas);

      SketchMemoryScript.m_Instance.MemorizeBatchedBrushStroke(
        stroke.m_BatchSubset,
        stroke.m_Color,
        stroke.m_BrushGuid,
        stroke.m_BrushSize,
        stroke.m_BrushScale,
        stroke.m_ControlPoints.ToList(),
        stroke.m_Flags,
        WidgetManager.m_Instance.ActiveStencil,
        lineLength,
        123
      );

      strokes.Add(stroke);
    }
  }

  }
}
#endif