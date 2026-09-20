// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TiltBrush
{
    /// A record of the symmetry settings that were in place at the moment a stroke was created.
    ///
    /// Snapshots are immutable once created, and shared by every symmetry group that was drawn
    /// with the same settings, so a sketch holds one per distinct set of settings rather than one
    /// per stroke. SketchWriter dedupes them the same way when saving.
    [Serializable]
    public class SymmetrySettingsSnapshot
    {
        /// Bumped if the serialized layout changes. Readers refuse blobs they don't understand
        /// rather than mis-parsing them; the rest of the stroke is unaffected because the blob
        /// is length-prefixed.
        private const int kSerializedVersion = 1;

        public PointerManager.SymmetryMode Mode;
        public PointerManager.CustomSymmetryType CustomType;
        public PointSymmetry.Family PointFamily;
        public int PointOrder;
        public SymmetryGroup.R WallpaperGroup;
        public int WallpaperRepeatX;
        public int WallpaperRepeatY;
        public float WallpaperScale;
        public float WallpaperScaleX;
        public float WallpaperScaleY;
        public float WallpaperSkewX;
        public float WallpaperSkewY;
        /// Pose of the symmetry widget, in scene space.
        public TrTransform WidgetTransform;
        /// Angular velocity of the symmetry widget, in scene space.
        public Vector3 Spin;
        /// Name of the active symmetry script. Only meaningful for ScriptedSymmetryMode.
        public string ScriptName;

        /// Takes a snapshot of the symmetry settings currently in effect.
        public static SymmetrySettingsSnapshot FromCurrentSettings()
        {
            var pm = PointerManager.m_Instance;
            if (pm == null) { return null; }

            var snapshot = new SymmetrySettingsSnapshot
            {
                Mode = pm.CurrentSymmetryMode,
                CustomType = pm.m_CustomSymmetryType,
                PointFamily = pm.m_PointSymmetryFamily,
                PointOrder = pm.m_PointSymmetryOrder,
                WallpaperGroup = pm.m_WallpaperSymmetryGroup,
                WallpaperRepeatX = pm.m_WallpaperSymmetryX,
                WallpaperRepeatY = pm.m_WallpaperSymmetryY,
                WallpaperScale = pm.m_WallpaperSymmetryScale,
                WallpaperScaleX = pm.m_WallpaperSymmetryScaleX,
                WallpaperScaleY = pm.m_WallpaperSymmetryScaleY,
                WallpaperSkewX = pm.m_WallpaperSymmetrySkewX,
                WallpaperSkewY = pm.m_WallpaperSymmetrySkewY,
                ScriptName = "",
            };

            var widget = pm.SymmetryWidget;
            if (widget != null)
            {
                snapshot.WidgetTransform = App.Scene.AsScene[widget.transform];
                snapshot.Spin = widget.GetSpin();
            }
            else
            {
                snapshot.WidgetTransform = TrTransform.identity;
            }

            if (snapshot.Mode == PointerManager.SymmetryMode.ScriptedSymmetryMode &&
                LuaManager.Instance != null)
            {
                try
                {
                    snapshot.ScriptName =
                        LuaManager.Instance.GetActiveScriptName(LuaApiCategory.SymmetryScript) ?? "";
                }
                catch (Exception)
                {
                    // No active symmetry script; leave the name empty.
                }
            }

            return snapshot;
        }

        /// Restores these settings, so that new strokes are created the same way as the
        /// stroke this snapshot came from. Does not restore the active symmetry script.
        public void ApplyToCurrentSettings()
        {
            var pm = PointerManager.m_Instance;
            if (pm == null) { return; }

            pm.m_CustomSymmetryType = CustomType;
            pm.m_PointSymmetryFamily = PointFamily;
            pm.m_PointSymmetryOrder = PointOrder;
            pm.m_WallpaperSymmetryGroup = WallpaperGroup;
            pm.m_WallpaperSymmetryX = WallpaperRepeatX;
            pm.m_WallpaperSymmetryY = WallpaperRepeatY;
            pm.m_WallpaperSymmetryScale = WallpaperScale;
            pm.m_WallpaperSymmetryScaleX = WallpaperScaleX;
            pm.m_WallpaperSymmetryScaleY = WallpaperScaleY;
            pm.m_WallpaperSymmetrySkewX = WallpaperSkewX;
            pm.m_WallpaperSymmetrySkewY = WallpaperSkewY;

            var widget = pm.SymmetryWidget;
            if (widget != null)
            {
                App.Scene.AsScene[widget.transform] = WidgetTransform;
            }

            pm.SetSymmetryMode(Mode);
        }

        // -------------------------------------------------------------------------------------- //
        // Serialization
        // -------------------------------------------------------------------------------------- //

        /// Serializes to a self-contained blob. Callers are responsible for the length prefix.
        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new SketchBinaryWriter(stream))
                {
                    writer.Int32(kSerializedVersion);
                    writer.Int32((int)Mode);
                    writer.Int32((int)CustomType);
                    writer.Int32((int)PointFamily);
                    writer.Int32(PointOrder);
                    writer.Int32((int)WallpaperGroup);
                    writer.Int32(WallpaperRepeatX);
                    writer.Int32(WallpaperRepeatY);
                    writer.Float(WallpaperScale);
                    writer.Float(WallpaperScaleX);
                    writer.Float(WallpaperScaleY);
                    writer.Float(WallpaperSkewX);
                    writer.Float(WallpaperSkewY);
                    writer.Vec3(WidgetTransform.translation);
                    writer.Quaternion(WidgetTransform.rotation);
                    writer.Float(WidgetTransform.scale);
                    writer.Vec3(Spin);
                    byte[] name = Encoding.UTF8.GetBytes(ScriptName ?? "");
                    writer.Int32(name.Length);
                    if (name.Length > 0)
                    {
                        writer.BaseStream.Write(name, 0, name.Length);
                    }
                }
                return stream.ToArray();
            }
        }

        /// Inverse of ToBytes(). Returns null if the blob can't be understood.
        public static SymmetrySettingsSnapshot FromBytes(byte[] data)
        {
            if (data == null || data.Length == 0) { return null; }
            try
            {
                using (var stream = new MemoryStream(data, writable: false))
                using (var reader = new SketchBinaryReader(stream))
                {
                    int version = reader.Int32();
                    if (version != kSerializedVersion) { return null; }
                    var snapshot = new SymmetrySettingsSnapshot
                    {
                        Mode = (PointerManager.SymmetryMode)reader.Int32(),
                        CustomType = (PointerManager.CustomSymmetryType)reader.Int32(),
                        PointFamily = (PointSymmetry.Family)reader.Int32(),
                        PointOrder = reader.Int32(),
                        WallpaperGroup = (SymmetryGroup.R)reader.Int32(),
                        WallpaperRepeatX = reader.Int32(),
                        WallpaperRepeatY = reader.Int32(),
                        WallpaperScale = reader.Float(),
                        WallpaperScaleX = reader.Float(),
                        WallpaperScaleY = reader.Float(),
                        WallpaperSkewX = reader.Float(),
                        WallpaperSkewY = reader.Float(),
                    };
                    Vector3 translation = reader.Vec3();
                    Quaternion rotation = reader.Quaternion();
                    float scale = reader.Float();
                    snapshot.WidgetTransform = TrTransform.TRS(translation, rotation, scale);
                    snapshot.Spin = reader.Vec3();
                    int nameLength = reader.Int32();
                    if (nameLength < 0 || nameLength > data.Length) { return null; }
                    if (nameLength > 0)
                    {
                        var name = new byte[nameLength];
                        int read = stream.Read(name, 0, nameLength);
                        if (read != nameLength) { return null; }
                        snapshot.ScriptName = Encoding.UTF8.GetString(name);
                    }
                    else
                    {
                        snapshot.ScriptName = "";
                    }
                    return snapshot;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Ignoring unreadable symmetry settings: {e.Message}");
                return null;
            }
        }

        // -------------------------------------------------------------------------------------- //
        // Value equality; lets identical settings be shared by every group that was drawn with
        // them, both in memory and in the saved file.
        // -------------------------------------------------------------------------------------- //

        public override bool Equals(object obj)
        {
            if (!(obj is SymmetrySettingsSnapshot other)) { return false; }
            return Mode == other.Mode &&
                CustomType == other.CustomType &&
                PointFamily == other.PointFamily &&
                PointOrder == other.PointOrder &&
                WallpaperGroup == other.WallpaperGroup &&
                WallpaperRepeatX == other.WallpaperRepeatX &&
                WallpaperRepeatY == other.WallpaperRepeatY &&
                WallpaperScale == other.WallpaperScale &&
                WallpaperScaleX == other.WallpaperScaleX &&
                WallpaperScaleY == other.WallpaperScaleY &&
                WallpaperSkewX == other.WallpaperSkewX &&
                WallpaperSkewY == other.WallpaperSkewY &&
                WidgetTransform == other.WidgetTransform &&
                Spin == other.Spin &&
                ScriptName == other.ScriptName;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Mode;
                hash = (hash * 397) ^ (int)CustomType;
                hash = (hash * 397) ^ (int)PointFamily;
                hash = (hash * 397) ^ PointOrder;
                hash = (hash * 397) ^ (int)WallpaperGroup;
                hash = (hash * 397) ^ WallpaperRepeatX;
                hash = (hash * 397) ^ WallpaperRepeatY;
                hash = (hash * 397) ^ WallpaperScale.GetHashCode();
                hash = (hash * 397) ^ WallpaperSkewX.GetHashCode();
                hash = (hash * 397) ^ WidgetTransform.GetHashCode();
                hash = (hash * 397) ^ (ScriptName?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Mode)
            {
                case PointerManager.SymmetryMode.MultiMirror
                    when CustomType == PointerManager.CustomSymmetryType.Point:
                    return $"Point {PointFamily}{PointOrder}";
                case PointerManager.SymmetryMode.MultiMirror
                    when CustomType == PointerManager.CustomSymmetryType.Wallpaper:
                    return $"Wallpaper {WallpaperGroup} {WallpaperRepeatX}x{WallpaperRepeatY}";
                case PointerManager.SymmetryMode.ScriptedSymmetryMode:
                    return $"Scripted ({ScriptName})";
                default:
                    return Mode.ToString();
            }
        }
    }
} // namespace TiltBrush
