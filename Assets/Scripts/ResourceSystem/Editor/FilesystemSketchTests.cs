using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.VersionControl;
using UnityEngine;
using Task = System.Threading.Tasks.Task;
namespace TiltBrush
{
    [TestFixture]
    public class FilesystemSketchTests
    {

        [Test]
        public void CreateFilesystemSketchFromTilt()
        {
            var fileSketch = new FilesystemSketch($"{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet/Sketch 1.tilt");
        }

        [Test]
        public void FilesystemSketchUriIsCorrect()
        {
            var fileSketch = new FilesystemSketch($"{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet/Sketch 1.tilt");
            Assert.AreEqual(fileSketch.Uri, new Uri($"file://{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet/Sketch 1.tilt"));
        }

        [Test]
        public async Task FilesystemSketchCollectionEnumAsync()
        {
            var collection = new FilesystemSketchCollection($"{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet", "Test");
            await collection.InitAsync();
            Assert.AreEqual(collection.Uri, new Uri($"file://{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet"));
            Assert.AreEqual(collection.Name, "Test");
            var contents = new List<IResource>();
            await foreach (var resource in collection.Contents())
            {
                contents.Add(resource);
            }
            Assert.AreEqual(contents.Count, 6);
            Assert.AreEqual(contents.Count(x => x is IResourceCollection), 1);

            var subdir = contents.FirstOrDefault(x => x is IResourceCollection) as FilesystemSketchCollection;
            await subdir.InitAsync();

            contents = new List<IResource>();
            await foreach (var resource in subdir.Contents())
            {
                contents.Add(resource);
            }
            Assert.AreEqual(contents.Count, 5);
        }
    }
}
