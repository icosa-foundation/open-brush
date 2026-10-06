// Copyright 2026 The Open Brush Authors

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestCatalogFileWatcher
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task NativeRenameForwardsOldAndNewPaths(bool directory)
        {
            string root = Path.Combine(Path.GetTempPath(), $"catalog-rename-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string oldPath = Path.Combine(root, "old");
            string newPath = Path.Combine(root, "new");
            try
            {
                if (directory) Directory.CreateDirectory(oldPath);
                else File.WriteAllText(oldPath, "fixture");
                using (var native = new FileSystemWatcher(root))
                using (var watcher = (FileWatcher)FormatterServices.GetUninitializedObject(typeof(FileWatcher)))
                {
                    typeof(FileWatcher).GetField("m_InternalFileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(watcher, native);
                    typeof(FileWatcher).GetMethod("AddEventsToInternalFileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(watcher, null);
                    var deleted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var created = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    watcher.FileDeleted += (sender, e) => deleted.TrySetResult(e.FullPath);
                    watcher.FileCreated += (sender, e) => created.TrySetResult(e.FullPath);
                    watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
                    watcher.EnableRaisingEvents = true;
                    if (directory) Directory.Move(oldPath, newPath);
                    else File.Move(oldPath, newPath);
                    Task both = Task.WhenAll(deleted.Task, created.Task);
                    Assert.AreSame(both, await Task.WhenAny(both, Task.Delay(30000)), "Rename notifications timed out.");
                    Assert.AreEqual(oldPath, await deleted.Task);
                    Assert.AreEqual(newPath, await created.Task);
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
