using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  public static class QuillSequenceReader
  {
    /// <summary>
    /// Reads a Quill project folder and imports it in a Sequence object.
    /// A project consists in a json-based scene hierarchy, a json-based state file, and binary data.
    /// </summary>
    public static Sequence Read(string path)
    {
      if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        return null;

      string sequenceFilename = Path.Combine(path, "Quill.json");
      string paintDataFilename = Path.Combine(path, "Quill.qbin");
      if (!File.Exists(sequenceFilename) || !File.Exists(paintDataFilename))
        return null;

      Sequence seq = null;
      try
      {
        string json = File.ReadAllText(sequenceFilename);
        JToken document = JsonConvert.DeserializeObject<JToken>(json);
        seq = Parse(document);
      }
      catch(JsonReaderException e)
      {
        // The JSON is invalid.
        // This happened sometimes in the early days of Quill when the transforms exploded and "inf" or "nan" was written in the matrix array.
        Console.WriteLine("JSON parsing error: {0}", e.Message);
      }
      catch(Exception e)
      {
        Console.WriteLine("Error during the parsing of the Quill.json document: {0}", e.Message);
      }

      if (seq == null)
        return null;

      // Now that we have loaded the scene hierarchy, read the actual attached data from the qbin.
      using (Stream stream = File.OpenRead(paintDataFilename))
      {
        QBinReader qbinReader = new QBinReader(stream);
        seq.LastStrokeId = qbinReader.ReadUInt32();
        ReadLayerData(seq.RootLayer, qbinReader);
      }

      return seq;
    }

    private static Sequence Parse(JToken s)
    {
      Sequence seq = new Sequence();
      if (s == null || s["Sequence"] == null)
        return seq;

      seq.Metadata = ParseMetadata(s["Sequence"]["Metadata"]);
      seq.Gallery = ParseGallery(s["Sequence"]["Gallery"]);
      seq.BackgroundColor = ParseColor(s["Sequence"]["BackgroundColor"] as JArray);
      if (s["Sequence"]["DefaultViewpoint"] != null)
        seq.DefaultViewpoint = (string)s["Sequence"]["DefaultViewpoint"].ToObject(typeof(string));
      else
        seq.DefaultViewpoint = ""; // "Root/InitialSpawnArea"

      seq.Framerate = ParseInt(s["Sequence"]["Framerate"], seq.Framerate);
      seq.ExportStart = ParseInt(s["Sequence"]["ExportStart"], seq.ExportStart);
      seq.ExportEnd = ParseInt(s["Sequence"]["ExportEnd"], seq.ExportEnd);
      seq.CameraResolution = ParseSize(s["Sequence"]["CameraResolution"] as JArray, seq.CameraResolution);
      seq.RootLayer = ParseLayer(s["Sequence"]["RootLayer"]) as LayerGroup;

      seq.RootLayer.Animation.Timeline = true;
      return seq;
    }

    private static Metadata ParseMetadata(JToken m)
    {
      Metadata metadata = new Metadata();
      if (m == null)
        return metadata;

      metadata.Title = (string)m["Title"].ToObject(typeof(string));
      metadata.Description = (string)m["Description"].ToObject(typeof(string));
      if (m["ThumbnailCropPosition"] != null)
        metadata.ThumbnailCropPosition = (float)m["ThumbnailCropPosition"].ToObject(typeof(float));

      return metadata;
    }

    private static Gallery ParseGallery(JToken g)
    {
      Gallery gallery = new Gallery();
      if (g == null)
        return gallery;

      // TODO.
      // ParseThumbnails: object.
      // ParsePictures: list.
      // Picture: Type, DataFileOffset, Metadata: VerticalFOV, HorizontalFOV.

      return gallery;
    }

    private static int ParseInt(JToken v, int def = 0)
    {
      return (v != null && v.Type != JTokenType.Null) ? v.ToObject<int>() : def;
    }

    private static float ParseFloat(JToken v, float def = 0)
    {
      return (v != null && v.Type != JTokenType.Null) ? v.ToObject<float>() : def;
    }

    private static bool ParseBool(JToken v, bool def = false)
    {
      return (v != null && v.Type != JTokenType.Null) ? v.ToObject<bool>() : def;
    }

    private static long ParseHexLong(JToken v, long def = 0)
    {
      if (v == null || v.Type == JTokenType.Null)
        return def;

      string value = v.Type == JTokenType.String ? v.ToObject<string>() : v.ToString();
      if (string.IsNullOrEmpty(value))
        return def;

      long parsed;
      return long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed) ? parsed : def;
    }

    private static Color ParseColor(JArray jValue, Color def = new Color())
    {
      if (jValue == null)
        return def;

      List<float> value = jValue.ToObject<List<float>>();
      if (value.Count != 3)
        return def;

      return new Color(value);
    }

    private static Size ParseSize(JArray jValue, Size def = new Size())
    {
      if (jValue == null)
        return def;

      List<int> value = jValue.ToObject<List<int>>();
      if (value.Count != 2)
        return def;

      return new Size(value);
    }

    private static Vector3 ParseVector3(JArray jValue)
    {
      if (jValue == null)
        return new Vector3();

      List<float> value = jValue.ToObject<List<float>>();
      if (value.Count != 3)
        return new Vector3();

      return new Vector3(value);
    }

    private static Vector4 ParseVector4(JArray jValue)
    {
      if (jValue == null)
        return new Vector4();

      List<float> value = jValue.ToObject<List<float>>();
      if (value.Count != 4)
        return new Vector4();

      return new Vector4(value);
    }

    private static Quaternion ParseQuaternion(JArray jValue)
    {
      if (jValue == null)
        return Quaternion.Identity;

      List<float> value = jValue.ToObject<List<float>>();
      if (value.Count != 4)
        return Quaternion.Identity;

      return new Quaternion(value);
    }

    private static Transform ParseTransform(JToken t)
    {
      if (t == null)
        return Transform.Identity;

      if (t is JObject)
      {
        Transform transform = Transform.Identity;
        transform.Rotation = ParseQuaternion(t["Rotation"] as JArray);
        transform.Scale = ParseFloat(t["Scale"], 1.0f);
        transform.Flip = t["Flip"]?.ToObject<string>();
        transform.Translation = ParseVector3(t["Translation"] as JArray);
        return transform;
      }
      else if (t is JArray)
      {
        // Old transform format from Quill 1.3 (raw 4x4 matrix).
        // We do not support this format.
        // To migrate the project to the new format open it in Quill and save it back.
        return Transform.Identity;
      }
      else
      {
        return Transform.Identity;
      }
    }

    private static BoundingBox ParseBoundingBox(JArray jValue)
    {
      if (jValue == null)
        return new BoundingBox();

      List<float> value = jValue.ToObject<List<float>>();
      if (value.Count != 6)
        return new BoundingBox();

      return new BoundingBox(value);
    }

    /// <summary>
    /// Parse the drawing metadata.
    /// The actual strokes will be read from the qbin later.
    /// </summary>
    private static Drawing ParseDrawing(JToken d)
    {
      Drawing drawing = new Drawing();

      if (d == null)
        return drawing;

      drawing.BoundingBox = ParseBoundingBox(d["BoundingBox"] as JArray);

      long offset;
      bool parsed = long.TryParse((string)d["DataFileOffset"].ToObject(typeof(string)), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
      drawing.DataFileOffset = parsed ? offset : -1;

      return drawing;
    }
    
    private static KeepAlive ParseKeepAlive(JToken ka)
    {
      KeepAlive keepAlive = new KeepAlive();
      if (ka == null)
        return keepAlive;

      keepAlive.Type = ParseEnum<KeepAliveType>(ka["Type"]);
      return keepAlive;
    }

    private static Animation ParseAnimation(JToken a)
    {
      Animation animation = new Animation();
      if (a == null)
        return animation;

      animation.Duration = ParseFloat(a["Duration"]);
      animation.Timeline = ParseBool(a["Timeline"]);
      animation.StartOffset = ParseFloat(a["StartOffset"]);
      animation.MaxRepeatCount = ParseFloat(a["MaxRepeatCount"]);
      animation.Keys = ParseKeyframes(a["Keys"]);

      return animation;
    }

    /// <summary>
    /// Parse one enum. If not found this will return the default value.
    /// </summary>
    private static T ParseEnum<T>(JToken v) where T : struct
    {
      Enum.TryParse((string)v.ToObject(typeof(string)), out T result);
      return result;
    }

    private static PictureType ParsePictureType(JToken v)
    {
      if (v == null || v.Type == JTokenType.Null)
        return PictureType.Unknown;

      string value = v.ToObject<string>();
      if (string.IsNullOrEmpty(value))
        return PictureType.Unknown;

      if (Enum.TryParse(value, ignoreCase: true, out PictureType result))
        return result;

      switch (value.Trim())
      {
        case "2D":
          return PictureType.TwoD;
        case "2DDepth":
          return PictureType.TwoDDepth;
        case "360 Equirectangular Mono":
          return PictureType.ThreeSixty_Equirect_Mono;
        case "360 Equirectangular Stereo":
          return PictureType.ThreeSixty_Equirect_Stereo;
        default:
          return PictureType.Unknown;
      }
    }
    
    /// <summary>
    /// Parse all the keyframe channels data.
    /// </summary>
    private static Keyframes ParseKeyframes(JToken kkff)
    {
      Keyframes keyframes = new Keyframes();
      if (kkff == null)
        return keyframes;

      if (kkff["Visibility"] != null)
      {
        keyframes.Visibility.Clear();
        foreach (var kf in kkff["Visibility"])
        {
          Keyframe<bool> keyframe = new Keyframe<bool>();
          keyframe.Time = (int)kf["Time"].ToObject<float>();
          keyframe.Value = kf["Value"].ToObject<bool>();
          keyframe.Interpolation = ParseEnum<Interpolation>(kf["Interpolation"]);
          keyframes.Visibility.Add(keyframe);
        }
      }

      if (kkff["Offset"] != null)
      {
        keyframes.Offset.Clear();
        foreach (var kf in kkff["Offset"])
        {
          Keyframe<int> keyframe = new Keyframe<int>();
          keyframe.Time = (int)kf["Time"].ToObject<float>();
          keyframe.Value = kf["Value"].ToObject<int>();
          keyframe.Interpolation = ParseEnum<Interpolation>(kf["Interpolation"]);
          keyframes.Offset.Add(keyframe);
        }
      }

      if (kkff["Opacity"] != null)
      {
        keyframes.Opacity.Clear();
        foreach (var kf in kkff["Opacity"])
        {
          Keyframe<float> keyframe = new Keyframe<float>();
          keyframe.Time = (int)kf["Time"].ToObject<float>();
          keyframe.Value = kf["Value"].ToObject<float>();
          keyframe.Interpolation = ParseEnum<Interpolation>(kf["Interpolation"]);
          keyframes.Opacity.Add(keyframe);
        }
      }

      if (kkff["Transform"] != null)
      {
        keyframes.Transform.Clear();
        foreach (var kf in kkff["Transform"])
        {
          Keyframe<Transform> keyframe = new Keyframe<Transform>();
          keyframe.Time = (int)kf["Time"].ToObject<float>();
          keyframe.Value = ParseTransform(kf["Value"]);
          keyframe.Interpolation = ParseEnum<Interpolation>(kf["Interpolation"]);
          keyframes.Transform.Add(keyframe);
        }
      }

      return keyframes;
    }

    /// <summary>
    /// Parse one layer. Drill down recursively for groups.
    /// </summary>
    private static Layer ParseLayer(JToken l)
    {
      if (l == null)
        return null;

      Layer layer;
      LayerType type = ParseEnum<LayerType>(l["Type"]);
      switch (type)
      {
        case LayerType.Group:
          {
            layer = new LayerGroup();
            LayerGroup lg = layer as LayerGroup;
            foreach (var c in l["Implementation"]["Children"])
            {
              Layer child = ParseLayer(c);
              if (child != null)
                lg.Children.Add(child);
            }

            break;
          }
        case LayerType.Paint:
          {
            layer = new LayerPaint();
            LayerPaint lp = layer as LayerPaint;

            lp.Framerate = ParseInt(l["Implementation"]["Framerate"], lp.Framerate);
            lp.MaxRepeatCount = ParseInt(l["Implementation"]["MaxRepeatCount"], lp.MaxRepeatCount);

            if (l["Implementation"]["Drawings"] != null && l["Implementation"]["Frames"] != null)
            {
              foreach (var d in l["Implementation"]["Drawings"])
              {
                Drawing drawing = ParseDrawing(d);
                if (drawing != null)
                  lp.Drawings.Add(drawing);
              }
              lp.Frames = l["Implementation"]["Frames"].ToObject<List<int>>();
            }
            else if (l["Implementation"]["BoundingBox"] != null && l["Implementation"]["DataFileOffset"] != null)
            {
              // Old format from Quill 1.3, circa 2017, before animations.
              Drawing drawing = new Drawing();
              drawing.BoundingBox = ParseBoundingBox(l["Implementation"]["BoundingBox"] as JArray);
              long offset;
              bool parsed = long.TryParse((string)l["Implementation"]["DataFileOffset"].ToObject(typeof(string)), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
              drawing.DataFileOffset = parsed ? offset : -1;
              lp.Drawings.Add(drawing);
              lp.Frames.Add(0);
            }
            break;
          }
        case LayerType.Viewpoint:
          {
            layer = new LayerViewpoint();
            LayerViewpoint lv = layer as LayerViewpoint;
            lv.Version = l["Implementation"]["Version"].ToObject<int>();
            lv.Color = ParseColor(l["Implementation"]["Color"] as JArray);
            lv.Sphere = ParseVector4(l["Implementation"]["Sphere"] as JArray);
            lv.AllowTranslationX = l["Implementation"]["AllowTranslationX"].ToObject<bool>();
            lv.AllowTranslationY = l["Implementation"]["AllowTranslationY"].ToObject<bool>();
            lv.AllowTranslationZ = l["Implementation"]["AllowTranslationZ"].ToObject<bool>();
            lv.Exporting = l["Implementation"]["Exporting"].ToObject<bool>();
            lv.ShowingVolume = l["Implementation"]["ShowingVolume"].ToObject<bool>();
            lv.TypeStr = l["Implementation"]["TypeStr"].ToObject<string>();
            break;
          }
        case LayerType.Camera:
          {
            layer = new LayerCamera();
            LayerCamera lc = layer as LayerCamera;
            lc.FOV = l["Implementation"]["FOV"].ToObject<float>();
            break;
          }
        case LayerType.Picture:
          {
            layer = new LayerPicture();
            LayerPicture lp = layer as LayerPicture;
            var impl = l["Implementation"];
            if (impl != null)
            {
              lp.PictureType = ParsePictureType(impl["Type"]);
              lp.ViewerLocked = ParseBool(impl["ViewerLocked"], lp.ViewerLocked);
              lp.ImportFilePath = impl["ImportFilePath"]?.ToObject<string>();
              lp.DataFileOffset = ParseHexLong(impl["DataFileOffset"], lp.DataFileOffset);
            }
            break;
          }
        case LayerType.Model:
        case LayerType.Sound:
        case LayerType.Unknown:
        default:
          layer = null;
          break;
      }

      if (layer != null)
        ParseLayerCommon(layer, l);

      return layer;
    }

    /// <summary>
    /// Parse the common part of the layer info.
    /// </summary>
    private static void ParseLayerCommon(Layer layer, JToken l)
    {
      layer.Name = l["Name"].ToObject<string>();
      layer.Visible = ParseBool(l["Visible"], layer.Visible);
      layer.Locked = ParseBool(l["Locked"], layer.Locked);
      layer.Collapsed = ParseBool(l["Collapsed"], layer.Collapsed);
      layer.BBoxVisible = ParseBool(l["BBoxVisible"], layer.BBoxVisible);
      layer.Opacity = ParseFloat(l["Opacity"], layer.Opacity);
      layer.IsModelTopLayer = ParseBool(l["IsModelTopLayer"], layer.IsModelTopLayer);
      layer.KeepAlive = ParseKeepAlive(l["KeepAlive"]);
      layer.Transform = ParseTransform(l["Transform"]);
      layer.Pivot = ParseTransform(l["Pivot"]);
      layer.Animation = ParseAnimation(l["Animation"]);
    }

    /// <summary>
    /// Recursive function reading binary data for the entire hierarchy.
    /// </summary>
    private static void ReadLayerData(Layer layer, QBinReader qbinReader)
    {
      if (layer.Type == LayerType.Group)
      {
        foreach (Layer l in ((LayerGroup)layer).Children)
          ReadLayerData(l, qbinReader);
      }
      else if (layer.Type == LayerType.Paint)
      {
        foreach (Drawing drawing in ((LayerPaint)layer).Drawings)
        {
          qbinReader.BaseStream.Seek(drawing.DataFileOffset, SeekOrigin.Begin);
          drawing.Data = qbinReader.ReadDrawingData();
        }
      }
      else if (layer.Type == LayerType.Picture)
      {
        LayerPicture picture = layer as LayerPicture;
        if (picture != null && picture.DataFileOffset > 0)
        {
          qbinReader.BaseStream.Seek(picture.DataFileOffset, SeekOrigin.Begin);
          picture.Data = qbinReader.ReadPictureData();
        }
      }
    }
  }
}
