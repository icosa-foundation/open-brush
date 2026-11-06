// Copyright 2026 The Open Brush Authors
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

using System.IO;
using Newtonsoft.Json;

namespace TiltBrush
{
    // Saves ONLY the post-production preview's metadata (offsets, crop, camera
    // setup) into an existing .tilt file, leaving every other byte - most
    // importantly data.sketch, the actual stroke data - completely untouched,
    // copied verbatim from the existing file rather than re-derived from live scene
    // state. Added 2026-09-08 after a real data-loss incident: the normal save path
    // (SketchSnapshot.WriteSnapshotToFile) re-derives strokes from
    // SketchMemoryScript.AllStrokes() at save time, which was found to return a
    // truncated set while sketch.scrub's playback timeline was mid-position -
    // silently dropping everything after the current scrub point (confirmed via
    // parse_tilt_sketch.py: num_strokes dropped 50 -> 14 after a save.overwrite
    // during scrubbing). This path can never do that, because it never touches
    // stroke data in any form - only a byte-for-byte copy.
    public static class PostProductionMetadataSaver
    {
        // Safe to read from the HTTP listener thread (see
        // query.postprod.metadata.save.status in ApiManager.cs) - HTTP 200 alone
        // never proves success in this codebase's API, so every outcome (success or
        // error) is recorded here explicitly rather than only thrown/logged.
        public static string LastStatus { get; private set; } = "not saved yet";

        // Editor Play sessions keep static state (no domain reload).
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => LastStatus = "not saved yet";

        public static string Save(string path)
        {
            string result;
            try
            {
                result = SaveInternal(path);
            }
            catch (System.Exception e)
            {
                // Without this, any exception (locked file, JSON issue, whatever)
                // vanished silently - LastStatus stayed at its initial default
                // forever, with no clue anything had even been attempted. Confirmed
                // happening for real 2026-09-08.
                result = $"error: exception during save: {e.GetType().Name}: {e.Message}";
            }
            LastStatus = result;
            return result;
        }

        private static string SaveInternal(string path)
        {
            var tiltFile = new TiltFile(path);
            if (!tiltFile.IsHeaderValid())
            {
                return $"error: not a valid .tilt file: {path}";
            }

            SketchMetadata metadata;
            using (var metaStream = tiltFile.GetReadStream(TiltFile.FN_METADATA))
            {
                if (metaStream == null)
                {
                    return $"error: no {TiltFile.FN_METADATA} found in {path}";
                }
                using var reader = new JsonTextReader(new StreamReader(metaStream));
                metadata = MakeJsonSerializer().Deserialize<SketchMetadata>(reader);
            }
            if (metadata == null)
            {
                return $"error: could not parse {TiltFile.FN_METADATA} in {path}";
            }

            // Read every other subfile verbatim - never re-derived from live scene
            // state, so stroke data cannot be truncated or altered by this path no
            // matter what the current scrub/playback state is.
            byte[] sketchBytes = ReadAllBytes(tiltFile, TiltFile.FN_SKETCH);
            if (sketchBytes == null)
            {
                return $"error: no {TiltFile.FN_SKETCH} found in {path} - refusing to save without it";
            }
            byte[] thumbnailBytes = ReadAllBytes(tiltFile, TiltFile.FN_THUMBNAIL);
            byte[] hiResBytes = ReadAllBytes(tiltFile, TiltFile.FN_HI_RES);

            // Update ONLY the post-production fields. Everything else on the
            // deserialized metadata - Layers[].Visible/Transform/Name, ModelIndex,
            // StrokeTimeSessions, etc. - stays exactly as read from disk, not from
            // live state (e.g. a layer temporarily hidden for testing must not get
            // baked in as permanently hidden by this save).
            metadata.PostProduction = SketchSnapshot.GetPostProductionMetadata();
            if (metadata.Layers != null)
            {
                for (int i = 0; i < metadata.Layers.Length; i++)
                {
                    metadata.Layers[i].PostProdOffset = PostProductionSettings.GetLayerSpatialOffset(i);
                    metadata.Layers[i].TimeOffsetMs = PostProductionSettings.GetLayerTimeOffsetMs(i);
                }
            }

            using (var tiltWriter = new TiltFile.AtomicWriter(path))
            {
                if (thumbnailBytes != null)
                {
                    using var s = tiltWriter.GetWriteStream(TiltFile.FN_THUMBNAIL);
                    s.Write(thumbnailBytes, 0, thumbnailBytes.Length);
                }
                if (hiResBytes != null)
                {
                    using var s = tiltWriter.GetWriteStream(TiltFile.FN_HI_RES);
                    s.Write(hiResBytes, 0, hiResBytes.Length);
                }
                using (var s = tiltWriter.GetWriteStream(TiltFile.FN_SKETCH))
                {
                    s.Write(sketchBytes, 0, sketchBytes.Length);
                }
                using (var jsonWriter = new CustomJsonWriter(new StreamWriter(
                    tiltWriter.GetWriteStream(TiltFile.FN_METADATA))))
                {
                    MakeJsonSerializer().Serialize(jsonWriter, metadata);
                }
                tiltWriter.Commit();
            }

            PostProductionSettings.SavedGlobalSpatialOffset = PostProductionSettings.GlobalSpatialOffset;
            if (metadata.Layers != null)
            {
                for (int i = 0; i < metadata.Layers.Length; i++)
                {
                    PostProductionSettings.SetSavedLayerSpatialOffset(i, PostProductionSettings.GetLayerSpatialOffset(i));
                }
            }
            return $"saved post-production metadata only to {path} " +
                $"({sketchBytes.Length} bytes of sketch data copied verbatim, untouched)";
        }

        // metadata.json's Vector3/Quaternion/TrTransform fields serialize as JSON
        // arrays (e.g. [x,y,z]), not objects - a plain `new JsonSerializer()` doesn't
        // know that and throws trying to parse them (confirmed 2026-09-08:
        // JsonSerializationException on ThumbnailCameraTransformInRoomSpace). Must
        // use the same ContractResolver SaveLoadScript/SketchSnapshot use for every
        // other read/write of this file.
        private static JsonSerializer MakeJsonSerializer()
        {
            return new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
        }

        private static byte[] ReadAllBytes(TiltFile tiltFile, string subfileName)
        {
            using var stream = tiltFile.GetReadStream(subfileName);
            if (stream == null) { return null; }
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
