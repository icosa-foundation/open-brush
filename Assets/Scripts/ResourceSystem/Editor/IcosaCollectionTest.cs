using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
namespace TiltBrush
{
    [TestFixture]
    public class IcosaCollectionTest
    {
        private HttpClient m_HttpClient;
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_HttpClient = new HttpClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // m_HttpClient.Dispose();
        }

        [Test]
        public async Task TestIcosaCollection()
        {
            var collection = new IcosaSketchCollection(m_HttpClient);
            await collection.InitAsync();
            var enumerator = collection.ContentsAsync().GetAsyncEnumerator();
            for (int i = 0; i < 10; ++i)
            {
                Assert.IsTrue(await enumerator.MoveNextAsync());
                Assert.IsNotNull(enumerator.Current.Name);
            }
        }
    }
}
