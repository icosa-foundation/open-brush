using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents the scene hierarchy and its metadata.
  /// </summary>
  public class Sequence
  {

    public Metadata Metadata { get; set; } = new Metadata();

    public Gallery Gallery { get; set; } = new Gallery();

    public Color BackgroundColor { get; set; } = new Color(0.8f, 0.8f, 0.8f);

    public string DefaultViewpoint { get; set; }

    public int Framerate { get; set; } = 24;

    public int ExportStart { get; set; } = 0;

    public int ExportEnd { get; set; } = 126000;

    public Size CameraResolution { get; set; } = new Size(1920, 1080);

    /// <summary>
    /// Root of the hierarchy of layers.
    /// </summary>
    public LayerGroup RootLayer { get; set; }

    public UInt32 LastStrokeId { get; set; }

    /// <summary>
    /// Creates a default sequence with a spawn area.
    /// This does not add any paint layer.
    /// </summary>
    public static Sequence CreateDefault()
    {
      Sequence seq = new Sequence();
      seq.RootLayer = LayerGroup.CreateDefault();
      seq.RootLayer.Children.Add(LayerViewpoint.CreateDefault());
      return seq;
    }

    /// <summary>
    /// Adds a new group layer at the specified path, creating all the groups along the way if necessary.
    /// The path should not contain the "Root" group, use a single "/" instead or nothing.
    /// For example, to create a group layer named "Group" under Root/MyGroup, call this with path="/MyGroup/Group".
    /// </summary>
    public LayerGroup CreateGroupLayerAt(string path, bool isSequence = false)
    {
      LayerGroup group = RootLayer;
      if (group == null)
        return null;

      string[] nodes = path.Split(new string[] { "/", "\\" }, StringSplitOptions.RemoveEmptyEntries);

      for (int i = 0; i < nodes.Length; i++)
      {
        Layer child = group.FindChild(nodes[i]);

        if (child == null || child.Type != LayerType.Group)
        {
          child = new LayerGroup(nodes[i], isSequence);
          group.Children.Add(child);
        }

        group = child as LayerGroup;
      }

      return group;
    }

    /// <summary>
    /// Adds a new paint layer at the specified path, creating all the groups along the way if necessary.
    /// The last element of the path is the name of the new layer.
    /// If the layer already exists it will be returned instead of a new one.
    /// The path should not contain the "Root" group, use a single "/" instead or nothing.
    /// For example, to create a paint layer named "Paint" under Root/MyGroup, call this with path="/MyGroup/Paint".
    /// </summary>
    public LayerPaint CreatePaintLayerAt(string path)
    {
      string groupPath = Path.GetDirectoryName(path);
      LayerGroup group = CreateGroupLayerAt(groupPath, false);
      if (group == null)
        return null;

      // At this point we have found or created the insertion point.
      // Double check in case the layer itself already exist.
      string name = Path.GetFileName(path);
      LayerPaint paint = null;
      foreach (Layer child in group.Children)
      {
        if (child.Type != LayerType.Paint || child.Name != name)
          continue;

        paint = child as LayerPaint;
        break;
      }

      if (paint == null)
        group.Children.Add(new LayerPaint(name));

      return paint;
    }

    /// <summary>
    /// Inserts an existing layer to a group, creating all the groups along the way if necessary.
    /// The parent group is identified by its path.
    /// The path must not contain the name of the leaf layer.
    /// For example, to insert a paint layer under Root/MyGroup, call this with path="/MyGroup".
    /// </summary>
    /// <param name="child">The layer to add to the group layer.</param>
    /// <param name="path">The path to the group layer.</param>
    public void InsertLayerAt(Layer child, string path)
    {
      LayerGroup group = CreateGroupLayerAt(path, false);
      if (group != null)
        group.Children.Add(child);
    }
  }
}
