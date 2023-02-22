using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Application = UnityEngine.Device.Application;
namespace TiltBrush.TestFiles
{
    [TestFixture]
    public class TiltFileTest
    {
        [Test]
        public async Task CanExtractMetaFromTiltFile()
        {
            var fileSketch = new FilesystemSketch($"{Application.dataPath}/Scripts/Resources/Editor/TestFiles/SketchSet/Sketch 1.tilt");
            var tiltFile = new DotTiltFile(fileSketch);
            var metaStream = await tiltFile.GetSubFileAsync("metadata.json");
            Assert.IsNotNull(metaStream);
            var text = new StreamReader(metaStream);
            Debug.Log(await text.ReadToEndAsync());
        }
    }
}
