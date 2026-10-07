using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    [Serializable]
    public sealed class WidgetLink
    {
        public uint GroupId;
        public int PointerIndex;
        [JsonIgnore] internal SymmetryWidgetGroup RuntimeGroup;
        [JsonIgnore] internal WidgetLinks Owner;

        internal void Attach(GrabWidget widget)
        {
            var group = RuntimeGroup;
            var transforms = group?.Mirror.Settings?.PointerTransforms;
            if (group == null || !SymmetryWidgetGroup.CanLink(widget) || PointerIndex < 0 ||
                transforms == null || PointerIndex >= transforms.Count ||
                (group.Mirror.Canvas != null && group.Mirror.Canvas != widget.Canvas) ||
                group.Members.Any(w => w != null && w.SymmetryPointerIndex == PointerIndex))
            {
                Owner?.WarnInvalidMembership();
                return;
            }
            SymmetryMirrors.Register(group.Mirror);
            widget.SetSymmetryGroup(group, PointerIndex);
        }

        internal static void Attach(GrabWidget widget, WidgetLink[] links, int index)
        {
            if (links != null && index < links.Length) { links[index]?.Attach(widget); }
        }
    }

    [Serializable]
    public sealed class WidgetLinks
    {
        public int Version = 1;
        public Group[] Groups;
        [JsonIgnore] private bool m_WarnedInvalidMembership;

        internal void WarnInvalidMembership()
        {
            if (m_WarnedInvalidMembership) { return; }
            m_WarnedInvalidMembership = true;
            Debug.LogWarning("[OB_WIDGET_LINK] Ignoring invalid widget memberships in this sketch.");
        }
        [Serializable]
        public sealed class Group
        {
            public uint Id;
            public Guid MirrorId;
        }

        internal void Resolve(SketchMetadata metadata, IReadOnlyDictionary<Guid, SymmetryMirror> mirrors)
        {
            var groups = new Dictionary<uint, SymmetryWidgetGroup>();
            foreach (var link in Memberships(metadata))
            {
                if (link != null) { link.Owner = this; }
            }
            if (Version != 1) { WarnInvalidMembership(); return; }
            var records = Groups ?? Array.Empty<Group>();
            var duplicateIds = records.Where(g => g != null).GroupBy(g => g.Id)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            foreach (var record in records)
            {
                if (record == null || record.Id == 0 || duplicateIds.Contains(record.Id) ||
                    !mirrors.TryGetValue(record.MirrorId, out var mirror)) { continue; }
                groups.Add(record.Id, new SymmetryWidgetGroup(mirror));
            }
            foreach (var link in Memberships(metadata))
            {
                if (link != null && groups.TryGetValue(link.GroupId, out var group))
                { link.RuntimeGroup = group; }
            }
        }

        private static IEnumerable<WidgetLink> Memberships(SketchMetadata metadata)
        {
            foreach (var model in metadata.ModelIndex ?? Array.Empty<TiltModels75>())
            { foreach (var link in model.WidgetLinks ?? Array.Empty<WidgetLink>()) { yield return link; } }
            foreach (var image in metadata.ImageIndex ?? Array.Empty<TiltImages75>())
            { foreach (var link in image.WidgetLinks ?? Array.Empty<WidgetLink>()) { yield return link; } }
            foreach (var w in metadata.LightIndex ?? Array.Empty<TiltLights>()) { yield return w.WidgetLink; }
            foreach (var w in metadata.TextWidgets ?? Array.Empty<TiltText>()) { yield return w.WidgetLink; }
            foreach (var w in metadata.Videos ?? Array.Empty<TiltVideo>()) { yield return w.WidgetLink; }
            foreach (var w in metadata.SoundClips ?? Array.Empty<TiltSoundClip>()) { yield return w.WidgetLink; }
            foreach (var w in metadata.Portals ?? Array.Empty<TiltPortal>()) { yield return w.WidgetLink; }
            foreach (var w in metadata.GaussianCaptures ?? Array.Empty<TiltGaussianCapture>()) { yield return w.WidgetLink; }
        }
    }

    public sealed class WidgetLinkSaveContext
    {
        private readonly HashSet<GrabWidget> m_Widgets;
        private readonly Dictionary<SymmetryWidgetGroup, uint> m_Groups = new Dictionary<SymmetryWidgetGroup, uint>();
        private readonly List<WidgetLinks.Group> m_Records = new List<WidgetLinks.Group>();
        internal readonly HashSet<SymmetryMirror> Mirrors = new HashSet<SymmetryMirror>();
        internal bool SelectedOnly { get; }
        internal WidgetLinks Table => m_Records.Count == 0 ? null : new WidgetLinks { Groups = m_Records.ToArray() };

        internal WidgetLinkSaveContext(IEnumerable<GrabWidget> widgets, bool selectedOnly)
        {
            SelectedOnly = selectedOnly;
            m_Widgets = new HashSet<GrabWidget>(widgets);
            foreach (var widget in m_Widgets.ToList())
            {
                if (widget.SymmetryPeerGroup != null) { m_Widgets.UnionWith(widget.SymmetryPeerGroup.ActiveMembers); }
            }
        }

        internal bool Includes(GrabWidget widget) => m_Widgets.Contains(widget);
        internal WidgetLink Get(GrabWidget widget) => Get(widget.SymmetryPeerGroup, widget.SymmetryPointerIndex);

        private WidgetLink Get(SymmetryWidgetGroup group, int index)
        {
            if (group == null) { return null; }
            if (!m_Groups.TryGetValue(group, out var id))
            {
                id = (uint)m_Groups.Count + 1;
                m_Groups.Add(group, id);
                m_Records.Add(new WidgetLinks.Group { Id = id, MirrorId = group.Mirror.Id });
                Mirrors.Add(group.Mirror);
            }
            return new WidgetLink { GroupId = id, PointerIndex = index };
        }

        internal TiltModels75 CaptureMissing(TiltModels75 model) => model.CopyForSave(
            model.WidgetLinks?.Select(link => link == null ? null : Get(link.RuntimeGroup, link.PointerIndex)).ToArray());
    }
}
