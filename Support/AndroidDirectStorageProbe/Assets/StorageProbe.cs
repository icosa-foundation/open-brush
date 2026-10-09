// Copyright 2026 The Open Brush Authors. Licensed under Apache-2.0.
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.SharpZipLib.Zip;

public sealed class StorageProbe : MonoBehaviour
{
    private AndroidJavaObject activity;
    private string root;
    private string reportPath;
    private string tree;
    private string status = "Starting";
    private const int BufferSize = 64 * 1024;
    private static readonly byte[] Payload = { 0, 1, 127, 128, 254, 255 };

    private void Start()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        reportPath = Path.Combine(Application.persistentDataPath, "obds-results.txt");
        Log($"BEGIN unity={Application.unityVersion} il2cpp={Il2Cpp} package={Application.identifier}");
        Log(activity.Call<string>("environment"));
        root = Path.Combine(activity.Call<string>("documentsPath"), "Open Brush", "OBDS_UnityProbe20261009");
        tree = activity.Call<string>("persistedTree");
        if (PlayerPrefs.HasKey("OBDS_mixed_final")) Run("mixed-final-after-restart", () =>
        {
            using (var stream = File.OpenRead(PlayerPrefs.GetString("OBDS_mixed_final")))
                CheckBytes(stream, UpdatedPayload);
        });
        Run("direct-operations", DirectOperations);
        if (!string.IsNullOrEmpty(tree)) OnTree(tree);
    }

    private static bool Il2Cpp
    {
        get
        {
#if ENABLE_IL2CPP
            return true;
#else
            return false;
#endif
        }
    }

    private void Log(string message)
    {
        string line = $"OBDS_UNITY {DateTime.UtcNow:O} {message}";
        Debug.Log(line);
        File.AppendAllText(reportPath, $"{line}\n");
        status = message;
    }

    private void Run(string operation, Action action)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            Log($"PASS operation={operation} elapsedMs={timer.ElapsedMilliseconds}");
        }
        catch (Exception error)
        {
            Log($"FAIL operation={operation} type={error.GetType().Name} detail={error.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new IOException(message);
    }

    private void DirectOperations()
    {
        Directory.CreateDirectory(root);
        string previous = Path.Combine(root, "restart.bin");
        if (File.Exists(previous))
        {
            using (var stream = File.OpenRead(previous)) CheckPayload(stream);
            Log("PASS operation=previous-process-reread bytes=6");
        }
        else Log("INFO operation=previous-process-reread no-visible-marker (first run or access loss)");

        string run = Path.Combine(root, $"run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(run);
        string source = Path.Combine(run, "source.bin");
        using (var stream = new FileStream(source, FileMode.CreateNew, FileAccess.ReadWrite))
        {
            stream.Write(Payload, 0, Payload.Length);
            stream.Flush(true);
            Require(stream.Length == Payload.Length, "Incorrect direct length");
            stream.Seek(3, SeekOrigin.Begin);
            Require(stream.ReadByte() == 128, "Incorrect direct seek/read");
        }
        string moved = Path.Combine(run, "moved.bin");
        File.Move(source, moved);
        using (var stream = File.OpenRead(moved)) CheckPayload(stream);
        File.WriteAllBytes(source, Payload);
        // Same rename/replace sequence as the path writer, confined to this run's fixtures.
        string backup = Path.Combine(run, "backup.bin");
        File.Move(moved, backup);
        File.Move(source, moved);
        using (var stream = File.OpenRead(moved)) CheckPayload(stream);
        File.WriteAllBytes(moved, Payload);
        Require(Directory.GetFiles(run).Length == 2, "Incorrect direct enumeration");
        string renamed = $"{run}-renamed";
        Directory.Move(run, renamed);
        File.Delete(Path.Combine(renamed, "backup.bin"));

        string fixture = Path.Combine(renamed, "synthetic.tilt");
        CreateArchive(fixture);
        ValidateArchive(() => File.OpenRead(fixture), "direct-synthetic");
        // Never overwrite a hidden marker: CreateNew distinguishes denial/collision from absence.
        if (!File.Exists(previous))
        {
            using (var stream = new FileStream(previous, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(Payload, 0, Payload.Length);
                stream.Flush(true);
            }
        }
        Log($"INFO fixtures={renamed} synthetic-only=true");
    }

    private static void CheckPayload(Stream stream)
    {
        CheckBytes(stream, Payload);
    }

    private static readonly byte[] UpdatedPayload = { 255, 254, 128, 127, 1, 0 };
    private static void CheckBytes(Stream stream, byte[] expectedBytes)
    {
        foreach (byte expected in expectedBytes) Require(stream.ReadByte() == expected, "Payload mismatch");
        Require(stream.ReadByte() == -1, "Unexpected trailing bytes");
    }

    private static void CreateArchive(string path)
    {
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            // TiltZipHeader followed by ZIP, matching TiltFile.AtomicWriter's layout.
            byte[] header = { 0x74, 0x69, 0x6c, 0x54, 16, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            output.Write(header, 0, header.Length);
            using (var zip = new ZipOutputStream(output) { IsStreamOwner = false, UseZip64 = UseZip64.Off })
            {
                zip.SetLevel(0);
                WriteEntry(zip, "metadata.json", Encoding.UTF8.GetBytes("{\"Authors\":[\"OBDS synthetic fixture\"]}"));
                WriteEntry(zip, "thumbnail.png", Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg=="));
                WriteEntry(zip, "data.sketch", Payload); // Byte fixture, not renderable stroke data.
            }
            output.Flush(true);
        }
    }

    private static void WriteEntry(ZipOutputStream zip, string name, byte[] bytes)
    {
        zip.PutNextEntry(new ZipEntry(name));
        zip.Write(bytes, 0, bytes.Length);
        zip.CloseEntry();
    }

    private void ValidateArchive(Func<Stream> open, string route)
    {
        long length;
        using (var stream = open())
        using (var reader = new BinaryReader(stream))
        {
            length = stream.Length;
            Require(stream.CanSeek, "Archive source is not seekable");
            Require(reader.ReadUInt32() == 0x546c6974, "Incorrect Tilt sentinel");
            ushort size = reader.ReadUInt16();
            Require(size >= 16 && size <= length, "Invalid Tilt header size");
            Require(reader.ReadUInt16() == 1, "Unsupported Tilt header version");
            stream.Seek(size, SeekOrigin.Begin);
            Require(reader.ReadUInt32() == 0x04034b50, "Missing ZIP local header");
        }
        // Each reader keeps its own backing stream alive. Interleave independently opened members.
        using (var metadataArchive = new ZipFile(open()))
        using (var thumbnailArchive = new ZipFile(open()))
        {
            ZipEntry metadata = metadataArchive.GetEntry("metadata.json") ?? metadataArchive.GetEntry("main.json");
            ZipEntry thumbnail = thumbnailArchive.GetEntry("thumbnail.png");
            Require(metadata != null && thumbnail != null && metadataArchive.GetEntry("data.sketch") != null,
                "Missing required archive member");
            using (var metadataStream = metadataArchive.GetInputStream(metadata))
            using (var thumbnailStream = thumbnailArchive.GetInputStream(thumbnail))
            {
                Require(metadataStream.ReadByte() >= 0, "Empty metadata");
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                foreach (byte value in signature)
                    Require(thumbnailStream.ReadByte() == value, "Invalid PNG signature");
                byte[] buffer = new byte[BufferSize];
                while (metadataStream.Read(buffer, 0, buffer.Length) != 0) { }
                while (thumbnailStream.Read(buffer, 0, buffer.Length) != 0) { }
            }
        }
        Log($"PASS archive route={route} length={length} header=true metadata=true thumbnail=true rendering=untested");
    }

    public void OnTree(string selectedTree)
    {
        tree = selectedTree;
        if (string.IsNullOrEmpty(tree)) { Log("INFO picker cancelled"); return; }
        Log(activity.Call<string>("environment"));
        string[] documents = null;
        Run("tree-discovery", () =>
        {
            documents = activity.Call<string[]>("children", tree);
            Log($"INFO tree .tilt-count={documents.Length}");
            // Bounded probe: choose a folder with one deliberate fixture; no recursive catalog scan.
            if (documents.Length != 1) throw new IOException("Select a folder containing exactly one .tilt fixture");
        });
        if (documents == null || documents.Length != 1) return;
        Run("fixture-test-dispatch", () =>
        {
            string uri = documents[0];
            string local = activity.Call<string>("localPath", uri);
            Log($"INFO fixture-uri={uri} candidate-local-path={local}");
            Run("direct-before-translation", () => ValidateArchive(() => File.OpenRead(local), "direct-before"));
            Run("java-channel", () => ValidateArchive(() =>
                new BufferedStream(new ChannelStream(activity.Call<AndroidJavaObject>("openRead", uri)), BufferSize),
                "java-channel"));
            Run("java-channel-background", () =>
                System.Threading.Tasks.Task.Run(() =>
                {
                    Require(AndroidJNI.AttachCurrentThread() == 0, "Could not attach background JNI thread");
                    try
                    {
                        ValidateArchive(() => new BufferedStream(
                            new ChannelStream(activity.Call<AndroidJavaObject>("openRead", uri)), BufferSize),
                            "java-channel-background");
                    }
                    finally { AndroidJNI.DetachCurrentThread(); }
                }).GetAwaiter().GetResult());
            Run("descriptor-alias", () =>
            {
                using (var owner = activity.Call<AndroidJavaObject>("openRead", uri))
                {
                    try { ValidateArchive(() => File.OpenRead(owner.Call<string>("alias")), "fd-alias"); }
                    finally { owner.Call("close"); }
                }
            });
            string translated = null;
            Run("mediastore-translation", () =>
            {
                translated = activity.Call<string>("mediaUri", uri);
                Log($"INFO translated-uri={translated}");
                Require(!string.IsNullOrEmpty(translated), "No MediaStore translation");
            });
            Run("direct-after-translation-only", () =>
                ValidateArchive(() => File.OpenRead(local), "direct-after-translation-only"));
            if (!string.IsNullOrEmpty(translated)) Run("translated-uri-read", () =>
            {
                using (var channel = new ChannelStream(activity.Call<AndroidJavaObject>("openRead", translated)))
                    Require(channel.ReadByte() == 0x74, "Translated URI read failed");
            });
            Run("direct-after-translated-uri-read", () =>
                ValidateArchive(() => File.OpenRead(local), "direct-after-translated-uri-read"));
        });
        if (activity.Call<bool>("writableTestTree", tree)) Run("mixed-write-experiment", MixedWriteExperiment);
    }

    private void MixedWriteExperiment()
    {
        const string name = "OBDS_external_transaction20261009.bin";
        string uri = activity.Call<string>("testDocument", tree, name);
        // Refuse to mutate anything except the exact six-byte synthetic test payload.
        using (var stream = new ChannelStream(activity.Call<AndroidJavaObject>("openRead", uri)))
            CheckPayload(stream);
        string path = activity.Call<string>("localPath", uri);
        Log($"INFO transaction-original={uri} length=6");
        Run("transaction-direct-before-translation", () =>
        {
            using (var stream = File.OpenRead(path)) CheckPayload(stream);
        });
        string translated = activity.Call<string>("mediaUri", uri);
        Log($"INFO transaction-translated-uri={translated}");
        Run("translated-readwrite-open", () =>
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                CheckPayload(stream);
                stream.Position = 0;
                stream.Write(Payload, 0, Payload.Length); // Same synthetic bytes, original remains identical.
                stream.Flush(true);
            }
        });
        string id = Guid.NewGuid().ToString("N");
        string temporary = Path.Combine(Path.GetDirectoryName(path), $"OBDS_new_{id}.bin");
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(UpdatedPayload, 0, UpdatedPayload.Length);
            stream.Flush(true);
        }
        using (var stream = File.OpenRead(temporary)) CheckBytes(stream, UpdatedPayload);
        string backupName = $"OBDS_original_{id}.bin";
        string backup = Path.Combine(Path.GetDirectoryName(path), backupName);
        bool renamedDirectly = false;
        Run("translated-direct-rename", () =>
        {
            File.Move(path, backup);
            renamedDirectly = true;
        });
        string backupUri;
        if (renamedDirectly) backupUri = activity.Call<string>("testDocument", tree, backupName);
        else backupUri = activity.Call<string>("renameTestDocument", tree, uri, backupName);
        using (var stream = new ChannelStream(activity.Call<AndroidJavaObject>("openRead", backupUri)))
            CheckPayload(stream);
        Log($"PASS original-preserved route={(renamedDirectly ? "direct-rename" : "saf-rename")} backup={backupUri}");
        // Every artifact remains in the test folder on failure. No delete or rollback can
        // erase the sole original; this experiment establishes semantics, not crash safety.
        File.Move(temporary, path);
        using (var stream = File.OpenRead(path)) CheckBytes(stream, UpdatedPayload);
        using (var stream = new ChannelStream(activity.Call<AndroidJavaObject>("openRead", backupUri)))
            CheckPayload(stream);
        PlayerPrefs.SetString("OBDS_mixed_final", path);
        PlayerPrefs.Save();
        Log($"PASS mixed-install final={path} original-backup={backupUri} newLength=6");
    }

    public void OnTreeError(string message) { Log($"FAIL picker {message}"); }

    private void LargeOffset()
    {
        string path = Path.Combine(root, $"large-{Guid.NewGuid():N}.bin");
        const long offset = (1L << 31) + 4096;
        using (var javaFile = new AndroidJavaObject("java.io.File", root))
            Require(javaFile.Call<long>("getUsableSpace") > offset + (1L << 30), "Need more than 3 GB free");
        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                stream.SetLength(offset + Payload.Length);
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Write(Payload, 0, Payload.Length);
                stream.Flush(true);
            }
            using (var stream = File.OpenRead(path))
            {
                Require(stream.Length == offset + Payload.Length, "64-bit length mismatch");
                stream.Seek(offset, SeekOrigin.Begin);
                CheckPayload(stream);
            }
            Log($"PASS operation=large-offset offset={offset} length={offset + Payload.Length}");
            // This isolates JNI/64-bit channel behavior. It is not a SAF grant test.
            using (var stream = new ChannelStream(activity.Call<AndroidJavaObject>(
                "openRead", new Uri(path).AbsoluteUri)))
            {
                Require(stream.Length == offset + Payload.Length, "Channel 64-bit length mismatch");
                stream.Seek(-Payload.Length, SeekOrigin.End);
                CheckPayload(stream);
            }
            Log($"PASS operation=java-channel-large-offset offset={offset} source=file-uri");
        }
        finally { File.Delete(path); }
    }

    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1000f, Screen.height / 600f, 1));
        GUI.Label(new Rect(20, 15, 960, 70), $"OBDS isolated IL2CPP probe\n{status}");
        if (GUI.Button(new Rect(20, 100, 460, 70), "Select folder with ONE external .tilt (read only)"))
            activity.Call("pickTree");
        if (GUI.Button(new Rect(20, 190, 460, 70), "Repeat persisted tree reads")) OnTree(tree);
        if (GUI.Button(new Rect(510, 100, 460, 70), "Refresh production folder access"))
            StartCoroutine(TestProductionRefresh());
        if (GUI.Button(new Rect(510, 190, 460, 70), "Test production folder startup gate"))
            StartCoroutine(TestProductionStartup());
        if (GUI.Button(new Rect(20, 280, 460, 70), "Test >2 GB offset (requires >3 GB free)"))
            Run("large-offset", LargeOffset);
        if (GUI.Button(new Rect(510, 280, 460, 70), "Grant write access to probe test folder"))
            activity.Call("pickWritableTestTree");
        if (GUI.Button(new Rect(20, 370, 460, 70), "Index ONLY the probe JSON fixture"))
            activity.Call("scanStartupJson");
        GUI.Label(new Rect(20, 450, 960, 150), $"Fixtures: {root}\nResults: {reportPath}\nSynthetic archive data is not a renderable sketch.");
    }

    private void OnDestroy() { activity?.Dispose(); }

    private System.Collections.IEnumerator TestProductionStartup()
    {
        yield return TiltBrush.AndroidDirectStorage.Initialize("OBDS_StartupGate20261009");
        Require(TiltBrush.AndroidDirectStorage.StartupReady, "Production startup did not become ready");
        Log($"PASS production-startup root={TiltBrush.AndroidDirectStorage.RootPath} documents={TiltBrush.AndroidDirectStorage.DocumentCount}");
        Run("production-writer", TestProductionWriter);
        Run("production-external-read", () =>
        {
            string external = Path.Combine(TiltBrush.AndroidDirectStorage.RootPath, "externally-supplied-real.tilt");
            string[] listed = TiltBrush.AndroidDirectStorage.GetFiles(TiltBrush.AndroidDirectStorage.RootPath);
            Require(Array.Exists(listed, path => path == external), "External sketch missing from merged listing");
            var sketch = new TiltBrush.TiltFile(external);
            Require(sketch.IsLoadable(), "External sketch could not be read by production TiltFile");
            using (Stream thumbnail = sketch.GetReadStream(TiltBrush.TiltFile.FN_THUMBNAIL))
                Require(thumbnail != null && thumbnail.ReadByte() == 137, "External thumbnail is not PNG");
            Log($"PASS production-external-read bytes={new FileInfo(external).Length} no adoption copy");
        });
    }

    private void TestProductionWriter()
    {
        string test = Path.Combine(TiltBrush.AndroidDirectStorage.RootPath, $"OBDS_Writer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(test);
        foreach (TiltBrush.TiltFormat format in new[] { TiltBrush.TiltFormat.Zip, TiltBrush.TiltFormat.Directory })
        {
            TiltBrush.DevOptions.I.PreferredTiltFormat = format;
            string path = Path.Combine(test, $"{format}.tilt");
            foreach (byte[] payload in new[] { Payload, UpdatedPayload })
            {
                using (var writer = new TiltBrush.TiltFile.AtomicWriter(path))
                {
                    using (Stream data = writer.GetWriteStream(TiltBrush.TiltFile.FN_SKETCH))
                        data.Write(payload, 0, payload.Length);
                    using (Stream metadata = writer.GetWriteStream(TiltBrush.TiltFile.FN_METADATA))
                        metadata.WriteByte((byte)'{');
                    using (Stream thumbnail = writer.GetWriteStream(TiltBrush.TiltFile.FN_THUMBNAIL))
                        thumbnail.WriteByte(137);
                    writer.Commit();
                }
                var sketch = new TiltBrush.TiltFile(path);
                Require(sketch.IsLoadable(), $"{format} production save is not loadable");
                using (Stream data = sketch.GetReadStream(TiltBrush.TiltFile.FN_SKETCH)) CheckBytes(data, payload);
            }
            Log($"PASS production-writer format={format} new+overwrite path={path}");
        }
        TiltBrush.DevOptions.I.PreferredTiltFormat = TiltBrush.TiltFormat.Zip;
    }

    private System.Collections.IEnumerator TestProductionRefresh()
    {
        if (!TiltBrush.AndroidDirectStorage.StartupReady)
        {
            Log("FAIL operation=production-refresh detail=Connect the production test folder first");
            yield break;
        }
        bool done = false;
        Action completed = () => done = true;
        TiltBrush.AndroidDirectStorage.Refreshed += completed;
        try
        {
            TiltBrush.AndroidDirectStorage.RequestRefresh();
            while (!done) yield return null;
            Run("production-refresh", () =>
            {
                string path = Path.Combine(TiltBrush.AndroidDirectStorage.RootPath, "externally-added-after-startup.json");
                Require(Array.Exists(TiltBrush.AndroidDirectStorage.GetFiles(TiltBrush.AndroidDirectStorage.RootPath),
                    candidate => candidate == path), "New external file missing after refresh");
                Require(File.ReadAllText(path).Contains("obds"), "New external file could not be read");
            });
        }
        finally { TiltBrush.AndroidDirectStorage.Refreshed -= completed; }
    }

    private sealed class ChannelStream : Stream
    {
        private AndroidJavaObject channel;
        private long position;
        private readonly long length;
        public ChannelStream(AndroidJavaObject value)
        {
            channel = value;
            try { length = channel.Call<long>("length"); }
            catch { Dispose(); throw; }
        }
        public override bool CanRead => channel != null;
        public override bool CanSeek => channel != null;
        public override bool CanWrite => false;
        public override long Length { get { EnsureOpen(); return length; } }
        public override long Position { get { EnsureOpen(); return position; } set { Seek(value, SeekOrigin.Begin); } }
        private void EnsureOpen() { if (channel == null) throw new ObjectDisposedException(nameof(ChannelStream)); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureOpen();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
            if (count == 0) return 0;
            sbyte[] bytes = channel.Call<sbyte[]>("read", position, Math.Min(count, BufferSize));
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
            position = checked(position + bytes.Length);
            return bytes.Length;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureOpen();
            long target;
            switch (origin)
            {
                case SeekOrigin.Begin: target = offset; break;
                case SeekOrigin.Current: target = checked(position + offset); break;
                case SeekOrigin.End: target = checked(length + offset); break;
                default: throw new ArgumentOutOfRangeException(nameof(origin));
            }
            if (target < 0) throw new IOException("Negative seek");
            return position = target;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && channel != null)
            {
                try { channel.Call("close"); }
                finally { channel.Dispose(); channel = null; }
            }
            base.Dispose(disposing);
        }
        public override void Flush() { EnsureOpen(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
