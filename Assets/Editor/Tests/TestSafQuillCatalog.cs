using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestSafQuillCatalog
    {
        private sealed class Backend : IUserStorageBackend, IDisposable
        {
            private sealed class Entry
            {
                public StorageDocument Document;
                public byte[] Data;
            }

            private readonly Dictionary<string, List<Entry>> m_Children =
                new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<StorageDocumentId, Entry> m_Entries =
                new Dictionary<StorageDocumentId, Entry>();
            private readonly Dictionary<string, StorageDocumentId> m_DirectoryIds =
                new Dictionary<string, StorageDocumentId>(StringComparer.OrdinalIgnoreCase);
            private readonly string m_MaterializedRoot = Path.Combine(
                Path.GetTempPath(), $"quill-saf-test-{Guid.NewGuid():N}");
            public string RootIdentity { get; set; } = "root";
            public bool ChangeRootOnMaterialize { get; set; }
            public string ChangeRootOnList { get; set; }
            public bool WrongChildParent { get; set; }
            public int MaterializeCount { get; private set; }

            public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
            public bool IsReady => true;

            public void AddProject(string directory, bool complete)
            {
                AddDirectory(directory);
                AddFile($"{directory}/Quill.json");
                if (complete) { AddFile($"{directory}/Quill.qbin"); }
            }

            public void AddDirectory(string path)
            {
                string parent = Parent(path);
                string name = Path.GetFileName(path);
                var entry = AddEntry(parent, name, true, null);
                m_DirectoryIds[path] = entry.Document.DocumentId;
                if (!m_Children.ContainsKey(path)) { m_Children[path] = new List<Entry>(); }
            }

            public void AddFile(string path)
            {
                AddEntry(Parent(path), Path.GetFileName(path), false, Array.Empty<byte>());
            }

            private Entry AddEntry(string parent, string name, bool directory, byte[] data)
            {
                var id = new StorageDocumentId(Guid.NewGuid().ToString("N"));
                StorageDocumentId parentId;
                m_DirectoryIds.TryGetValue(parent, out parentId);
                if (!directory && WrongChildParent) { parentId = new StorageDocumentId("wrong-parent"); }
                var entry = new Entry
                {
                    Data = data,
                    Document = new StorageDocument(id, parentId, name,
                        directory ? "vnd.android.document/directory" : "application/octet-stream",
                        directory, directory ? null : data.Length, DateTime.UtcNow, 0,
                        string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}")
                };
                if (!m_Children.TryGetValue(parent, out List<Entry> children))
                {
                    children = new List<Entry>();
                    m_Children[parent] = children;
                }
                children.Add(entry);
                m_Entries[id] = entry;
                if (directory)
                {
                    string path = Combine(parent, name);
                    m_DirectoryIds[path] = id;
                    if (!m_Children.ContainsKey(path)) { m_Children[path] = new List<Entry>(); }
                }
                return entry;
            }

            public StorageDirectoryResult List(StorageArea area, string relativeDirectory,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = (relativeDirectory ?? "").Trim('/');
                Assert.AreEqual(StorageArea.MediaLibraryQuill, area);
                if (path == ChangeRootOnList) { RootIdentity = "changed"; }
                return m_Children.TryGetValue(path, out List<Entry> entries)
                    ? StorageDirectoryResult.Succeeded(entries.Select(entry => entry.Document).ToList())
                    : StorageDirectoryResult.Failed(StorageResultCode.NotFound, "missing");
            }

            public string Materialize(StorageDocumentId documentId, MaterializationScope scope,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.AreEqual(MaterializationScope.File, scope);
                ++MaterializeCount;
                Entry entry = m_Entries[documentId];
                string path = Path.Combine(m_MaterializedRoot,
                    entry.Document.RelativeDisplayPath.Replace('/', Path.DirectorySeparatorChar));
                if (entry.Document.IsDirectory)
                {
                    Directory.CreateDirectory(path);
                    foreach (Entry child in m_Children[entry.Document.RelativeDisplayPath])
                    {
                        File.WriteAllBytes(Path.Combine(path, child.Document.DisplayName), child.Data);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, entry.Data);
                }
                if (ChangeRootOnMaterialize) { RootIdentity = "changed"; }
                return path;
            }

            public Stream OpenRead(StorageDocumentId documentId, bool requireSeekable,
                CancellationToken cancellationToken) => new MemoryStream(m_Entries[documentId].Data);
            public StorageTreeResult EnumerateTree(StorageArea area, string relativeDirectory,
                StorageTreeQuery query, CancellationToken cancellationToken) =>
                StorageTreeResult.Succeeded(Array.Empty<StorageDocument>());
            public IStorageWriteTransaction BeginWrite(StorageArea area, string relativePath,
                string mimeType, CancellationToken cancellationToken, StorageDocumentId targetDocumentId = default) =>
                throw new NotSupportedException();
            public StorageMutationResult Rename(StorageDocumentId documentId, string newDisplayName,
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public StorageMutationResult Delete(StorageDocumentId documentId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public string GetMaterializationPath(StorageDocumentId documentId) => Materialize(
                documentId, MaterializationScope.File, CancellationToken.None);

            public void Dispose()
            {
                if (Directory.Exists(m_MaterializedRoot)) { Directory.Delete(m_MaterializedRoot, true); }
            }

            private static string Parent(string path)
            {
                int slash = path.LastIndexOf('/');
                return slash < 0 ? "" : path.Substring(0, slash);
            }

            private static string Combine(string parent, string name) =>
                string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
        }

        [Test]
        public void QuerySafFiles_RecognizesCompleteDirectChildProjectAndImm()
        {
            using var backend = new Backend();
            backend.AddProject("Selected/Complete", true);
            backend.AddProject("Selected/Incomplete", false);
            backend.AddFile("Selected/standalone.imm");
            List<QuillFileInfo> files = QuillFileCatalog.QuerySafFiles(backend, "Selected");
            Assert.AreEqual(2, files.Count);
            Assert.AreEqual(2, backend.MaterializeCount);
            Assert.IsTrue(files.Any(file => file.SourceType == QuillSourceType.Quill));
            Assert.IsTrue(files.Any(file => file.SourceType == QuillSourceType.Imm));
        }

        [Test]
        public void QuerySafFiles_RejectsRootChangeAfterMaterialization()
        {
            using var backend = new Backend { ChangeRootOnMaterialize = true };
            backend.AddProject("Complete", true);
            Assert.Throws<IOException>(() => QuillFileCatalog.QuerySafFiles(backend, ""));
        }

        [TestCase("")]
        [TestCase("Complete")]
        public void QuerySafFiles_RejectsRootChangeDuringListing(string changedDirectory)
        {
            using var backend = new Backend { ChangeRootOnList = changedDirectory };
            backend.AddProject("Complete", true);
            Assert.Throws<IOException>(() => QuillFileCatalog.QuerySafFiles(backend, ""));
            Assert.AreEqual(0, backend.MaterializeCount);
        }

        [Test]
        public void QuerySafFiles_RejectsUnrelatedParentAndIncorrectCase()
        {
            using var backend = new Backend { WrongChildParent = true };
            backend.AddProject("Unrelated", true);
            backend.WrongChildParent = false;
            backend.AddDirectory("CaseMismatch");
            backend.AddFile("CaseMismatch/quill.json");
            backend.AddFile("CaseMismatch/Quill.qbin");
            Assert.IsEmpty(QuillFileCatalog.QuerySafFiles(backend, ""));
            Assert.AreEqual(0, backend.MaterializeCount);
        }

        [Test]
        public void QuerySafFiles_DoesNotFlattenNestedProjects()
        {
            using var backend = new Backend();
            backend.AddDirectory("Outer");
            backend.AddDirectory("Outer/Inner");
            backend.AddProject("Outer/Inner/Project", true);
            backend.AddFile("Outer/Inner/clip.IMM");
            Assert.IsEmpty(QuillFileCatalog.QuerySafFiles(backend, ""));
            Assert.IsEmpty(QuillFileCatalog.QuerySafFiles(backend, "Outer"));
            Assert.AreEqual(2, QuillFileCatalog.QuerySafFiles(backend, "Outer\\Inner").Count);
        }
    }
}
