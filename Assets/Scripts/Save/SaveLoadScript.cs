// Copyright 2020 The Tilt Brush Authors
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

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{

    public class SaveLoadScript : MonoBehaviour
    {

        //
        // Static API
        //

        public const string UNTITLED_PREFIX = "Untitled_";
        public const string TILTASAURUS_PREFIX = "Tiltasaurus_";
        public const string TILT_SUFFIX = ".tilt";

        public static SaveLoadScript m_Instance;

        public static IEnumerable<DiskSceneFileInfo> IterScenes(DirectoryInfo di, bool makeReadOnly = false)
        {
            foreach (var sub in di.GetFiles("*" + TILT_SUFFIX))
            {
                yield return new DiskSceneFileInfo(sub.FullName, readOnly: makeReadOnly);
            }
            foreach (var sub in di.GetDirectories("*" + TILT_SUFFIX))
            {
                yield return new DiskSceneFileInfo(sub.FullName, readOnly: makeReadOnly);
            }
        }

        // e.g. for "Foo (0123-abcd).tilt", capture "0123-abcd".
        static Regex Md5SuffixRegex = new Regex(@" \(([0-9a-f]{4}-[0-9a-f]{4})\)\.tilt$");
        static Regex Md5RemoveSuffixRegex = new Regex(@"(.+) \([0-9a-f]{4}-[0-9a-f]{4}\)(\.tilt)$");

        /// Returns MD5 suffix portion of given .tilt filename if it's present, else null.
        public static string Md5Suffix(string fileName)
        {
            var groups = Md5SuffixRegex.Match(fileName).Groups;
            return (groups.Count == 2) ? groups[1].Value : null;
        }

        /// Returns filename or path with MD5 suffix removed.
        public static string RemoveMd5Suffix(string fileName)
        {
            return Md5RemoveSuffixRegex.Replace(fileName, @"$1$2");
        }

        /// Returns given filename/path with MD5 suffix inserted.  Input should not have a suffix.
        public static string AddMd5Suffix(string filename, byte[] hash)
        {
            string md5 = BitConverter.ToString(hash).Replace("-", null).ToLower();
            return filename.Replace(
                TILT_SUFFIX,
                String.Format(" ({0}-{1}){2}",
                    md5.Substring(md5.Length - 8, 4),
                    md5.Substring(md5.Length - 4, 4),
                    TILT_SUFFIX));
        }

        /// Returns MD5 hash computed via blocking read of entire file.
        public static byte[] GetMd5(string path)
        {
            var hashAlg = new System.Security.Cryptography.MD5CryptoServiceProvider();
            return hashAlg.ComputeHash(new FileStream(path, FileMode.Open, FileAccess.Read));
        }

        // See https://docs.microsoft.com/en-us/dotnet/api/system.text.encoding?view=netframework-4.7.1
        const int kAsciiCodePage = 20127;
        static SaveLoadScript()
        {
#if UNITY_2018_4_OR_NEWER
            // 2018 doesn't include ANSICodePage any more -- or maybe it's only if we use .net 4.6?
            ICSharpCode.SharpZipLib.Zip.ZipStrings.CodePage = kAsciiCodePage;
#else
            // There's an ancient mono bug (that Unity inherits) that prevents builds
            // from including the proper set of code pages, causing runtime errors when
            // SharpZipLib tries to use one. We only ever write ASCII filenames, so
            // the choice here is arbitrary. It just needs to be something that is
            // guaranteed to be included in the build.
            ICSharpCode.SharpZipLibUnityPort.Zip.ZipConstants.DefaultCodePage =
                System.Globalization.CultureInfo.InvariantCulture.TextInfo.ANSICodePage;
#endif
        }

        //
        // Instance API
        //

        [SerializeField] private Texture2D m_AutosaveThumbnail;
        [SerializeField] private float m_AutosaveTriggerSeconds;
        [SerializeField] private string m_AutosaveFilenamePattern;
        [SerializeField] private int m_AutosaveFileCount;

        private string m_SaveDir;
        private SceneFileInfo m_LastSceneFile;
        private bool m_LastSceneIsLegacy;

        private int m_LastNonexistentFileIndex = 0;

        private JsonSerializer m_JsonSerializer;

        [SerializeField] private SaveIconCaptureScript m_SaveIconCapture;
        private IEnumerator<Timeslice> m_SaveCoroutine;

        [SerializeField] private bool m_CaptureHiResSaveIcon = false;
        [SerializeField] private bool m_CaptureGifSaveIcon = false;

        // Various Save Icon render textures.
        [SerializeField] private int m_SaveIconHiResWidth = 1920;
        [SerializeField] private int m_SaveIconHiResHeight = 1080;
        private RenderTexture m_SaveIconHiResRenderTexture;

        [SerializeField] private int m_SaveGifWidth = 256;
        [SerializeField] private int m_SaveGifHeight = 256;
        private RenderTexture[] m_SaveGifRenderTextures;
        public int m_SaveGifTextureCount = 5;

        private byte[] m_AutosaveThumbnailBytes;
        private float m_SecondsUntilNextAutosave;
        private DiskSceneFileInfo m_AutosaveFileInfo;
        private bool m_AutosaveFailed;
        private string m_AutosaveTargetFilename;
        private IEnumerator<Timeslice> m_AutosaveCoroutine;

        private RenderTexture m_SaveIconRenderTexture;

        private byte[] m_LastThumbnailBytes;
        private string m_LastJsonMetadatError;
        private string m_LastWriteSnapshotError;
        private bool m_SuppressNotify;

        public bool AutosaveEnabled
        {
            get
            {
                return !m_AutosaveFailed &&
                    App.PlatformConfig.EnableAutosave &&
                    !App.UserConfig.Flags.DisableAutosave;
            }
        }

        public TrTransform? LastThumbnail_SS { get; set; }

        public TrTransform ReasonableThumbnail_SS
        {
            get
            {
                if (LastThumbnail_SS.HasValue)
                {
                    return LastThumbnail_SS.Value;
                }
                else
                {
                    // If we need to create a default position, we want it to be quick, so we set the
                    // number of items to enumerate to 1.
                    return SketchControlsScript.m_Instance.GenerateBestGuessSaveIconTransform(
                        itemsToEnumerate: 1);
                }
            }
        }

        /// Check .Valid on return value if you want to know
        public SceneFileInfo SceneFile => m_LastSceneFile;

        public RenderTexture GetSaveIconRenderTexture()
        {
            return m_SaveIconRenderTexture;
        }

        public bool SuppressSaveNotifcation { set => m_SuppressNotify = value; }

        public bool CanOverwriteSource { get { return !m_LastSceneFile.ReadOnly; } }

        public string LastMetadataError
        {
            get { return m_LastJsonMetadatError; }
        }

        public string LastWriteSnapshotError { get => m_LastWriteSnapshotError; }

        public JsonSerializer JsonSerializer { get { return m_JsonSerializer; } }

        public bool IsSavingAllowed() { return m_SaveCoroutine == null; }

        public string GetLastFileHumanName()
        {
            return m_LastSceneFile.Valid ? m_LastSceneFile.HumanName : "New Sketch";
        }

        public byte[] GetLastThumbnailBytes()
        {
            return m_LastThumbnailBytes;
        }

        void Awake()
        {
            m_Instance = this;
            m_JsonSerializer = new JsonSerializer();
            m_JsonSerializer.ContractResolver = new CustomJsonContractResolver();
            if (!Application.isEditor)
            {
                m_JsonSerializer.Error += HandleDeserializationError;
            }

            ResetLastFilename();

            // Create hi-res save icon render texture.
            m_SaveIconHiResRenderTexture = new RenderTexture(m_SaveIconHiResWidth, m_SaveIconHiResHeight,
                0, RenderTextureFormat.ARGB32);

            // Guarantee we've got an odd, >0 number of gif render textures.
            Debug.Assert((m_SaveGifTextureCount % 2) == 1);

            // Create low-res save gif render textures.
            m_SaveGifRenderTextures = new RenderTexture[m_SaveGifTextureCount];
            for (int i = 0; i < m_SaveGifTextureCount; ++i)
            {
                m_SaveGifRenderTextures[i] = new RenderTexture(m_SaveGifWidth, m_SaveGifHeight, 0,
                    RenderTextureFormat.ARGB32);
            }
            m_SaveIconRenderTexture = new RenderTexture(m_SaveGifWidth, m_SaveGifHeight, 0,
                RenderTextureFormat.ARGB32);

            m_SaveDir = App.UserSketchPath();
            FileUtils.InitializeDirectoryWithUserError(m_SaveDir);

            MarkAsAutosaveDone();
            m_AutosaveThumbnailBytes = m_AutosaveThumbnail.EncodeToPNG();

            SketchMemoryScript.m_Instance.OperationStackChanged += SketchChanged;
        }

        protected void OnDestroy()
        {
            SketchMemoryScript.m_Instance.OperationStackChanged -= SketchChanged;
        }

        public void ResetLastFilename()
        {
            m_LastSceneFile = new DiskSceneFileInfo();
        }

        // Create a name that is guaranteed not to exist.
        public string GenerateNewFilename(string desiredFilename, string directory, string extension)
        {
            int iIndex = m_LastNonexistentFileIndex;
            int iSanity = 9999;
            while (iSanity > 0)
            {
                string attempt = desiredFilename;
                if (iIndex > 0)
                {
                    attempt += "_" + iIndex;
                }
                --iSanity;
                ++iIndex;

                attempt = Path.Combine(directory, attempt) + extension;
                if (!File.Exists(attempt) && !Directory.Exists(attempt))
                {
                    m_LastNonexistentFileIndex = iIndex;
                    return attempt;
                }
            }
            Debug.Assert(false, "Could not generate a name");
            return null;
        }


        // Create a name that is guaranteed not to exist.
        public string GenerateNewUntitledFilename(string directory, string extension)
        {
            string filename = UNTITLED_PREFIX;
            return GenerateNewFilename(filename, directory, extension);
        }

        // Create a Tiltasaurus based name that is guaranteed not to exist.
        public string GenerateNewTiltasaurusFilename(string directory, string extension)
        {
            string filename = TILTASAURUS_PREFIX + Tiltasaurus.m_Instance.Prompt;
            return GenerateNewFilename(filename, directory, extension);
        }

        public void SaveOverwriteOrNewIfNotAllowed()
        {
            bool saveNew = true;
            if (SceneFile.Valid)
            {
                if (!SceneFile.ReadOnly)
                {
                    SketchControlsScript.m_Instance.IssueGlobalCommand(
                        SketchControlsScript.GlobalCommands.Save);
                    saveNew = false;
                }
            }

            if (saveNew)
            {
                SketchControlsScript.m_Instance.GenerateBestGuessSaveIcon();
                SketchControlsScript.m_Instance.IssueGlobalCommand(
                    SketchControlsScript.GlobalCommands.SaveNew);
            }
        }

        /// Used only for monoscopic backwards compatibility
        public IEnumerator<Timeslice> SaveMonoscopic(int slot)
        {
            string path = Path.Combine(m_SaveDir, "Sketch" + slot) + TILT_SUFFIX;
            Debug.LogFormat("Saving to {0}", path);
            return SaveLow(new DiskSceneFileInfo(path));
        }

        /// When a new scene file info or metadata is being created from an existing scene file info,
        /// we either preserve SourceId, or if this was a cloud sketch set it from the original asset.
        public string TransferredSourceIdFrom(SceneFileInfo info)
        {
            if (info is IcosaSceneFileInfo polyInfo)
            {
                // If the original is a Poly sketch it becomes the source.
                return polyInfo.AssetId;
            }
            else
            {
                return info.SourceId;
            }
        }

        public DiskSceneFileInfo GetNewNameSceneFileInfo(bool tiltasaurusMode = false, string filename = null)
        {
            string uniquePath;
            // If no filename is passed in then generate one
            if (string.IsNullOrWhiteSpace(filename))
            {
                uniquePath = tiltasaurusMode
                    ? GenerateNewTiltasaurusFilename(m_SaveDir, TILT_SUFFIX)
                    : GenerateNewUntitledFilename(m_SaveDir, TILT_SUFFIX);
            }
            else
            {
                uniquePath = GenerateNewFilename(filename, m_SaveDir, TILT_SUFFIX);
            }
            DiskSceneFileInfo fileInfo = new DiskSceneFileInfo(uniquePath);
            if (m_LastSceneFile.Valid)
            {
                fileInfo.SourceId = TransferredSourceIdFrom(m_LastSceneFile);
            }
            return fileInfo;
        }

        public DiskSceneFileInfo GetSceneFileInfoFromName(string name)
        {
            DiskSceneFileInfo fileInfo = new DiskSceneFileInfo(name);
            if (m_LastSceneFile.Valid)
            {
                fileInfo.SourceId = TransferredSourceIdFrom(m_LastSceneFile);
            }
            return fileInfo;
        }

        /// Save a snapshot directly to a location.
        /// The snapshot's AssetId is the source of truth
        public IEnumerator<Timeslice> SaveSnapshot(SceneFileInfo fileInfo, SketchSnapshot snapshot)
        {
            return SaveLow(fileInfo, false, snapshot);
        }

        /// Save, overwriting current file name
        /// Used mostly for doing silent upgrades-in-place and so on.
        /// Also used for the emergency-save command
        public IEnumerator<object> SaveOverwrite(bool tiltasaurusMode = false)
        {
            if (!m_LastSceneFile.Valid || tiltasaurusMode)
            {
                yield return SaveNewName(tiltasaurusMode);
            }
            else
            {
                yield return SaveLow(m_LastSceneFile);
            }
        }

        /// Save to a completely new name
        public IEnumerator<Timeslice> SaveNewName(bool tiltasaurusMode = false)
        {
            return SaveLow(GetNewNameSceneFileInfo(tiltasaurusMode));
        }

        public IEnumerator<Timeslice> SaveAs(string filename)
        {
            return SaveLow(GetNewNameSceneFileInfo(false, filename));
        }

        /// In order to for this to work properly:
        /// - m_SaveIconRenderTexture must contain data
        /// - SaveIconTool.LastSaveCameraRigState must be good
        /// SaveIconTool.ProgrammaticCaptureSaveIcon() does both of these things
        private IEnumerator<Timeslice> SaveLow(
            SceneFileInfo info, bool bNotify = true, SketchSnapshot snapshot = null)
        {
            Debug.Assert(!SelectionManager.m_Instance.HasSelection);
            if (snapshot != null && info.AssetId != snapshot.AssetId)
            {
                Debug.LogError($"AssetId in FileInfo '{info.AssetId}' != shapshot '{snapshot.AssetId}'");
            }
            if (!info.Valid)
            {
                throw new ArgumentException("null filename");
            }

            if (!FileUtils.CheckDiskSpaceWithError(m_SaveDir))
            {
                return new List<Timeslice>().GetEnumerator();
            }

            m_LastSceneFile = info;
            AbortAutosave();

            m_SaveCoroutine = ThreadedSave(info, bNotify, snapshot);
            return m_SaveCoroutine;
        }

        private IEnumerator<Timeslice> ThreadedSave(SceneFileInfo fileInfo,
                                                    bool bNotify = true, SketchSnapshot snapshot = null)
        {
            // Cancel any pending transfers of this file.
            var cancelTask = App.DriveSync.CancelTransferAsync(fileInfo.FullPath);

            bool newFile = !fileInfo.Exists;

            if (snapshot == null)
            {
                IEnumerator<Timeslice> timeslicedConstructor;
                snapshot = CreateSnapshotWithIcons(out timeslicedConstructor);
                if (App.CurrentState != App.AppState.Reset)
                {
                    App.Instance.SetDesiredState(App.AppState.Saving);
                }
                while (timeslicedConstructor.MoveNext())
                {
                    yield return timeslicedConstructor.Current;
                }
            }
            LastThumbnail_SS = snapshot.LastThumbnail_SS;
            App.Instance.SetDesiredState(App.AppState.Standard);
            m_LastWriteSnapshotError = null;

            // Make sure the cancel task is done before we start writing the snapshot.
            while (!cancelTask.IsCompleted)
            {
                yield return null;
            }

            string error = null;
            var writeFuture = new Future<string>(
                () => snapshot.WriteSnapshotToFile(fileInfo.FullPath),
                null, true);
            while (!writeFuture.TryGetResult(out error))
            {
                yield return null;
            }

            m_LastWriteSnapshotError = error;
            m_LastThumbnailBytes = snapshot.Thumbnail;
            SketchMemoryScript.m_Instance.SetLastOperationStackCount();
            SketchMemoryScript.m_Instance.InitialSketchTransform = App.Scene.Pose;
            m_SaveCoroutine = null;
            if (error == null)
            {
                if (newFile)
                {
                    SketchCatalog.m_Instance.NotifyUserFileCreated(m_LastSceneFile.FullPath);
                }
                else
                {
                    SketchCatalog.m_Instance.NotifyUserFileChanged(m_LastSceneFile.FullPath);
                }
            }
            if (bNotify && !m_SuppressNotify)
            {
                NotifySaveFinished(m_LastSceneFile, error, newFile);
            }
            App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
            m_SuppressNotify = false;
        }

        /// If success, error should be null
        private void NotifySaveFinished(SceneFileInfo info, string error, bool newFile)
        {
            if (error == null)
            {
                // TODO: More long term something should be done in OutputWindowScript itself to handle such
                // cases, such as a check for Floating Panels Mode or a check for if the controller requested
                // is tracked.
                if (newFile)
                {
                    OutputWindowScript.ReportFileSaved("Added to Sketchbook!", info.FullPath,
                        OutputWindowScript.InfoCardSpawnPos.Brush);
                }
                else
                {
                    OutputWindowScript.ReportFileSaved("Saved!", info.FullPath,
                        OutputWindowScript.InfoCardSpawnPos.UIReticle);
                    AudioManager.m_Instance.PlaySaveSound(
                        InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush));
                }
                App.Instance.AutosaveRestoreFileExists = false;
            }
            else
            {
                OutputWindowScript.Error(
                    InputManager.ControllerName.Wand,
                    "Failed to save sketch", error);
            }
        }

        static public Stream GetMetadataReadStream(SceneFileInfo fileInfo)
        {
            var stream = fileInfo.GetReadStream(TiltFile.FN_METADATA);
            if (stream != null)
            {
                return stream;
            }
            else
            {
                return fileInfo.GetReadStream(TiltFile.FN_METADATA_LEGACY);
            }
        }

        // Loads the head and scene trandsforms into the secondary ODS
        public bool LoadTransformsForOds(SceneFileInfo fileInfo,
                                         ref TrTransform head,
                                         ref TrTransform scene)
        {
            if (!fileInfo.IsHeaderValid())
            {
                OutputWindowScript.m_Instance.AddNewLine(
                    "Could not load transform: {0}", fileInfo.HumanName);
                return false;
            }

            m_LastSceneIsLegacy = false;
            Stream metadata = GetMetadataReadStream(fileInfo);
            if (metadata == null)
            {
                OutputWindowScript.m_Instance.AddNewLine("Could not load: {0}", fileInfo.HumanName);
                return false;
            }
            using (var jsonReader = new JsonTextReader(new StreamReader(metadata)))
            {
                var jsonData = DeserializeMetadata(jsonReader);

                if (jsonData.RequiredCapabilities != null)
                {
                    var missingCapabilities = jsonData.RequiredCapabilities.Except(
                        Enum.GetNames(typeof(PlaybackCapabilities))).ToArray();
                    if (missingCapabilities.Length > 0)
                    {
                        Debug.LogFormat("Lacking playback capabilities: {0}",
                            String.Join(", ", missingCapabilities));
                        OutputWindowScript.m_Instance.AddNewLine(
                            $"Lacking a capability to load {fileInfo.HumanName}. " +
                            $"Upgrade {App.kAppDisplayName}?");
                        return false;
                    }
                }

                scene = jsonData.SceneTransformInRoomSpace;
                head = jsonData.ThumbnailCameraTransformInRoomSpace;
            }

            return true;
        }

        /// Follows the "force-superseded by" chain until the end is reached, then returns that brush
        /// If the passed Guid is invalid, returns it verbatim.
        static Guid GetForceSupersededBy(Guid original)
        {
            var brush = BrushCatalog.m_Instance.GetBrush(original);
            // The failure will be reported downstream
            if (brush == null) { return original; }
            while (brush.m_SupersededBy != null && brush.m_SupersededBy.m_LooksIdentical)
            {
                brush = brush.m_SupersededBy;
            }
            return brush.m_Guid;
        }

        /// bAdditive is an experimental feature.
        /// XXX: bAdditive is buggy; it re-draws any pre-existing strokes.
        /// We never noticed before because the duplicate geometry draws on top of itself.
        /// It begins to be noticeable now that loading goes into the active canvas,
        /// which may not be the canvas of the original strokes.
        public bool Load(SceneFileInfo fileInfo, bool bAdditive = false)
        {
            m_LastThumbnailBytes = null;
            if (!fileInfo.IsHeaderValid())
            {
                OutputWindowScript.m_Instance.AddNewLine(
                    "Could not load: {0}", fileInfo.HumanName);
                return false;
            }

            m_LastSceneIsLegacy = false;
            Stream metadata = GetMetadataReadStream(fileInfo);
            if (metadata == null)
            {
                OutputWindowScript.m_Instance.AddNewLine("Could not load: {0}", fileInfo.HumanName);
                return false;
            }
            using (var jsonReader = new JsonTextReader(new StreamReader(metadata)))
            {
                SketchMetadata jsonData = DeserializeMetadata(jsonReader);
                if (LastMetadataError != null)
                {
                    ControllerConsoleScript.m_Instance.AddNewLine(
                        string.Format("Error detected in sketch '{0}'.\nSuggest re-saving.",
                            fileInfo.HumanName));
                    Debug.LogWarning(string.Format("Error reading meteadata for {0}.\n{1}",
                        fileInfo.FullPath,
                        SaveLoadScript.m_Instance.LastMetadataError));
                }
                if (jsonData.RequiredCapabilities != null)
                {
                    var missingCapabilities = jsonData.RequiredCapabilities.Except(
                        Enum.GetNames(typeof(PlaybackCapabilities))).ToArray();
                    if (missingCapabilities.Length > 0)
                    {
                        Debug.LogFormat("Lacking playback capabilities: {0}",
                            String.Join(", ", missingCapabilities));
                        OutputWindowScript.m_Instance.AddNewLine(
                            "Lacking a capability to load {0}.  Upgrade Tilt Brush?",
                            fileInfo.HumanName);
                        return false;
                    }
                }

                if (!bAdditive)
                {
                    var environment = EnvironmentCatalog.m_Instance
                        .GetEnvironment(new Guid(jsonData.EnvironmentPreset));
                    if (environment != null)
                    {
                        SceneSettings.m_Instance.RecordSkyColorsForFading();
                        if (jsonData.Environment != null)
                        {
                            SceneSettings.m_Instance.SetCustomEnvironment(jsonData.Environment, environment);
                        }
                        SceneSettings.m_Instance.SetDesiredPreset(
                            environment, forceTransition: true,
                            keepSceneTransform: true, hasCustomLights: jsonData.Lights != null
                        );
                        // This will have been overwritten by Set
                        if (jsonData.Environment != null && jsonData.Environment.Skybox != null)
                        {
                            SceneSettings.m_Instance.LoadCustomSkybox(jsonData.Environment.Skybox);
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Unknown environment preset {0}",
                            jsonData.EnvironmentPreset);
                    }
                    App.Instance.SetOdsCameraTransforms(jsonData.ThumbnailCameraTransformInRoomSpace,
                        jsonData.SceneTransformInRoomSpace);
                    App.Scene.Pose = jsonData.SceneTransformInRoomSpace;
                    App.Scene.ResetLayers(true);
                    LastThumbnail_SS = App.Scene.Pose.inverse *
                        jsonData.ThumbnailCameraTransformInRoomSpace;

                }

                SketchControlsScript.m_Instance.SketchPlaybackMode =
                    SketchControlsScript.m_Instance.m_DefaultSketchPlaybackMode;

                if (!bAdditive)
                {
                    // Create Layers
                    if (jsonData.Layers != null)
                    {
                        for (var i = 0; i < jsonData.Layers.Length; i++)
                        {
                            var layer = jsonData.Layers[i];
                            CanvasScript canvas = i == 0 ? App.Scene.MainCanvas : App.Scene.AddLayerNow();
                            canvas.gameObject.name = layer.Name;
                            canvas.gameObject.SetActive(layer.Visible);

                            // Assume that layers with a scale of 0 are from legacy sketches with no layer transform stored
                            // and that they should be set to 1
                            // nb. The correct place to do this would be somewhere in the deserialization code
                            // But after failing with DefaultValueHandling.Populate and custom JsonConverters
                            // I'm just going to do it here
                            if (layer.Transform.scale == 0)
                            {
                                TrTransform tr = layer.Transform;
                                tr.scale = 1;
                                layer.Transform = tr;
                            }
                            canvas.LocalPose = layer.Transform;
                        }
                    }
                }

                var oldGroupToNewGroup = new Dictionary<int, int>();

                // Load sketch
                using (var stream = fileInfo.GetReadStream(TiltFile.FN_SKETCH))
                {
                    Guid[] brushGuids = jsonData.BrushIndex.Select(GetForceSupersededBy).ToArray();
                    bool legacySketch;
                    bool success = SketchWriter.ReadMemory(stream, brushGuids, bAdditive, out legacySketch, out oldGroupToNewGroup);
                    m_LastSceneIsLegacy |= legacySketch;
                    if (!success)
                    {
                        OutputWindowScript.m_Instance.AddNewLine(
                            "Could not load: {0}", fileInfo.HumanName);
                        // Prevent it from being overwritten
                        m_LastSceneIsLegacy = false;
                        return false;
                    }
                }


                // It's proving to be rather complex to merge widgets/models etc.
                // For now skip all that when loading additively with the if (!bAdditive) below
                // This should cover the majority of use cases.

                // (For when we do support merging widgets:)
                // It's much simpler to change the group ids in the JSON
                // before we pass it to WidgetManager
                //GroupManager.UpdateWidgetJsonToNewGroups(jsonData, oldGroupToNewGroup);

                if (!bAdditive)
                {
                    ModelCatalog.m_Instance.ClearMissingModels();
                    SketchMemoryScript.m_Instance.InitialSketchTransform = jsonData.SceneTransformInRoomSpace;

                    if (jsonData.ModelIndex != null)
                    {
                        WidgetManager.m_Instance.SetModelDataFromTilt(jsonData.ModelIndex);
                    }

                    if (jsonData.LightIndex != null)
                    {
                        WidgetManager.m_Instance.SetLightDataFromTilt(jsonData.LightIndex);
                    }

                    if (jsonData.GuideIndex != null)
                    {
                        foreach (Guides guides in jsonData.GuideIndex)
                        {
                            StencilWidget.FromGuideIndex(guides);
                        }
                    }
                    if (jsonData.Lights != null)
                    {
                        LightsControlScript.m_Instance.CustomLights = jsonData.Lights;
                    }
                    // Pass even if null; null is treated as empty
                    CustomColorPaletteStorage.m_Instance.SetColorsFromPalette(jsonData.Palette);
                    // Images are not stored on Poly either.
                    // TODO - will this assumption still hold with Icosa?
                    if (!(fileInfo is IcosaSceneFileInfo))
                    {
                        if (ReferenceImageCatalog.m_Instance != null && jsonData.ImageIndex != null)
                        {
                            WidgetManager.m_Instance.SetImageDataFromTilt(jsonData.ImageIndex);
                        }
                        if (VideoCatalog.Instance != null && jsonData.Videos != null)
                        {
                            WidgetManager.m_Instance.SetVideoDataFromTilt(jsonData.Videos);
                        }
                        if (jsonData.TextWidgets != null)
                        {
                            WidgetManager.m_Instance.SetTextDataFromTilt(jsonData.TextWidgets);
                        }
                    }
                    if (jsonData.Mirror != null)
                    {
                        PointerManager.m_Instance.SymmetryWidgetFromMirror(jsonData.Mirror);
                    }
                    if (jsonData.CameraPaths != null)
                    {
                        WidgetManager.m_Instance.SetCameraPathDataFromTilt(jsonData.CameraPaths);
                    }
                    if (fileInfo is GoogleDriveSketchSet.GoogleDriveFileInfo gdInfo)
                    {
                        gdInfo.SourceId = jsonData.SourceId;
                    }
                    if (WidgetManager.m_Instance.CreatingMediaWidgets)
                    {
                        StartCoroutine(
                            OverlayManager.m_Instance.RunInCompositor(
                                OverlayType.LoadMedia,
                                WidgetManager.m_Instance.CreateMediaWidgetsFromLoadDataCoroutine(),
                                0.5f));
                    }
                    m_LastSceneFile = fileInfo;
                }
            }

            return true;
        }

        public SketchMetadata DeserializeMetadata(JsonTextReader jsonReader)
        {
            m_LastJsonMetadatError = null;
            var metadata = m_JsonSerializer.Deserialize<SketchMetadata>(jsonReader);
            if (metadata != null)
            {
                MetadataUtils.VerifyMetadataVersion(metadata);
            }
            return metadata;
        }

        private void HandleDeserializationError(object sender,
                                                Newtonsoft.Json.Serialization.ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            Debug.LogWarning(currentError);
            m_LastJsonMetadatError = currentError;
            errorArgs.ErrorContext.Handled = true;
        }

        public void SignalPlaybackCompletion()
        {
            if (DevOptions.I.ResaveLegacyScenes && m_LastSceneIsLegacy)
            {
                Debug.Log("Rewriting legacy file: " + m_LastSceneFile.HumanName + TILT_SUFFIX);
                SketchControlsScript.m_Instance.GenerateBoundingBoxSaveIcon();
                StartCoroutine(SaveOverwrite());
            }
        }

        public void MarkAsAutosaveDone()
        {
            m_SecondsUntilNextAutosave = -1f;
        }

        public void SketchChanged()
        {
            if (m_AutosaveCoroutine != null)
            {
                AbortAutosave();
            }
            if (App.CurrentState != App.AppState.Standard)
            {
                return;
            }
            m_SecondsUntilNextAutosave = m_AutosaveTriggerSeconds;
        }

        private void Update()
        {
            if (!AutosaveEnabled)
            {
                return;
            }

            if (PointerManager.m_Instance.MainPointer.IsCreatingStroke())
            {
                SketchChanged();
            }

            if (m_SecondsUntilNextAutosave >= 0f)
            {
                m_SecondsUntilNextAutosave -= Time.unscaledDeltaTime;
                if (m_SecondsUntilNextAutosave < 0f && m_AutosaveCoroutine == null)
                {
                    m_AutosaveCoroutine = AutosaveCoroutine();
                    StartCoroutine(m_AutosaveCoroutine);
                }
            }
        }

        private IEnumerator<Timeslice> AutosaveCoroutine()
        {
            if (!AutosaveEnabled)
            {
                yield break;
            }

            // Despite not actually creating a thumbnail for autosaves, we need to create a valid thumbnail
            // camera position so that if the autosave gets loaded the user can do a quicksave and the
            // created thumbnail will be in the right place.
            var iconCameraRig = new SaveIconTool.CameraRigState();
            iconCameraRig.SetLossyTransform(ReasonableThumbnail_SS);

            IEnumerator<Timeslice> timeslicedConstructor;
            SketchSnapshot snapshot = new SketchSnapshot(
                m_JsonSerializer, m_SaveIconCapture, out timeslicedConstructor);
            while (timeslicedConstructor.MoveNext())
            {
                yield return timeslicedConstructor.Current;
            }
            snapshot.Thumbnail = m_AutosaveThumbnailBytes;

            // We can clear the reference to the coroutine here because  at this point we already have a
            // full snapshot of the save and can just go ahead and write it.
            m_AutosaveCoroutine = null;

            string error = null;
            if (m_LastSceneFile.Valid)
            {
                snapshot.SourceId = TransferredSourceIdFrom(m_LastSceneFile);
            }
            var writeFuture = new Future<string>(
                () => snapshot.WriteSnapshotToFile(m_AutosaveFileInfo.FullPath),
                null, true);
            while (!writeFuture.TryGetResult(out error))
            {
                yield return null;
            }

            if (error != null)
            {
                m_AutosaveFailed = true;
                OutputWindowScript.Error("Error with autosave! Autosave disabled.");
                Debug.LogWarning(error);
                ControllerConsoleScript.m_Instance.AddNewLine(error);
            }
            MarkAsAutosaveDone();

            App.Instance.AutosaveRestoreFileExists = true;
        }

        private void AbortAutosave()
        {
            if (m_AutosaveCoroutine != null)
            {
                StopCoroutine(m_AutosaveCoroutine);
                m_AutosaveCoroutine = null;
            }
        }

        /// Creates a new filename for the autosave file and deletes old autosaves.
        /// It uses the date and time that the sketch was started or loaded.
        public void NewAutosaveFile()
        {
            if (!AutosaveEnabled)
            {
                return;
            }

            string autosaveStart = m_AutosaveFilenamePattern.Substring(
                0, m_AutosaveFilenamePattern.IndexOf("{"));
            try
            {
                if (!Directory.Exists(App.AutosavePath()))
                {
                    Directory.CreateDirectory(App.AutosavePath());
                }
                var files = new DirectoryInfo(App.AutosavePath()).GetFiles()
                    .Where(x => x.Name.StartsWith(autosaveStart))
                    .OrderBy(x => x.LastWriteTimeUtc).ToArray();
                if (files.Length >= m_AutosaveFileCount)
                {
                    for (int i = files.Length - m_AutosaveFileCount; i >= 0; i--)
                    {
                        File.Delete(files[i].FullName);
                    }
                }

                m_AutosaveTargetFilename = Path.Combine(
                    App.AutosavePath(), string.Format(m_AutosaveFilenamePattern, DateTime.Now));

                m_AutosaveFileInfo = new DiskSceneFileInfo(m_AutosaveTargetFilename);
            }
            catch (Exception exception)
            {
                m_AutosaveFailed = true;
                // wait a couple of seconds before showing the error, so it doesn't get lost if at startup.
                StartCoroutine(ShowErrorAfterDelay("Error with autosave! Autosave disabled.", 2f));
                ControllerConsoleScript.m_Instance.AddNewLine(exception.Message);
                Debug.LogWarningFormat("{0}\n{1}", exception.Message, exception.StackTrace);
                if (!(exception is IOException || exception is AccessViolationException ||
                    exception is UnauthorizedAccessException))
                {
                    throw;
                }
            }
        }

        public string MostRecentAutosaveFile()
        {
            string autosaveDir = App.AutosavePath();
            if (!Directory.Exists(autosaveDir))
            {
                return null;
            }
            var lastFile = Directory.GetFiles(autosaveDir, "*.tilt").Select(x => new FileInfo(x)).OrderByDescending(x => x.CreationTimeUtc).FirstOrDefault();

            return lastFile.FullName;
        }

        private IEnumerator ShowErrorAfterDelay(string error, float delay)
        {
            yield return new WaitForSeconds(delay);
            OutputWindowScript.Error(error);
        }

        /// Like the SketchSnapshot constructor, but also populates the snapshot with icons.
        public async Task<SketchSnapshot> CreateSnapshotWithIconsAsync()
        {
            var snapshot = CreateSnapshotWithIcons(out var coroutine);
            await coroutine; // finishes off the snapshot
            return snapshot;
        }

        /// Like the SketchSnapshot constructor, but also populates the snapshot with icons.
        /// As with the constructor, you must run the coroutine to completion before the snapshot
        /// is usable.
        public SketchSnapshot CreateSnapshotWithIcons(out IEnumerator<Timeslice> coroutine)
        {
            IEnumerator<Timeslice> timeslicedConstructor;
            SketchSnapshot snapshot = new SketchSnapshot(
                m_JsonSerializer, m_SaveIconCapture, out timeslicedConstructor);
            coroutine = CoroutineUtil.Sequence(
                timeslicedConstructor,
                snapshot.CreateSnapshotIcons(m_SaveIconRenderTexture,
                    m_CaptureHiResSaveIcon ? m_SaveIconHiResRenderTexture : null,
                    m_CaptureGifSaveIcon ? m_SaveGifRenderTextures : null));
            return snapshot;
        }

        public IEnumerator GetLastAutosaveBytes(Action<byte[]> onComplete)
        {

            while (m_AutosaveCoroutine != null) yield return null;

            // Retrieve the autosaved file
            string autosaveFile = MostRecentAutosaveFile();
            if (!string.IsNullOrEmpty(autosaveFile) && File.Exists(autosaveFile))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(autosaveFile);
                    Debug.Log($"Autosave complete. Loaded {fileBytes.Length} bytes from {autosaveFile}");
                    onComplete?.Invoke(fileBytes);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"Failed to read autosave file: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogWarning("Autosave file not found or doesn't exist.");
                onComplete?.Invoke(null);
            }
        }

        public void LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogError("LoadFromBytes: Data is null or empty.");
                return;
            }

            try
            {
                // Write the byte array to a temporary file
                string tempFilePath = Path.Combine(Application.temporaryCachePath, "temp_autosave.tilt");
                File.WriteAllBytes(tempFilePath, data);

                // Load the temporary file into the scene
                var fileInfo = new DiskSceneFileInfo(tempFilePath);
                if (Load(fileInfo))
                {
                    Debug.Log("LoadFromBytes: Scene successfully loaded from bytes.");
                }
                else
                {
                    Debug.LogError("LoadFromBytes: Failed to load scene.");
                }

                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadFromBytes: Error while loading scene from bytes. Exception: {ex.Message}");
            }
        }
    }

} // namespace TiltBrush
