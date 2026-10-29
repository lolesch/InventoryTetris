using NUnit.Framework;
using Submodules.Utility.Persistence;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>The WebGL save store flushes after each change and passes every read through.</summary>
    [TestFixture]
    public sealed class WebGlSyncingSaveStoreTests
    {
        private InMemorySaveStore inner;
        private WebGlSyncingSaveStore store;
        private int flushes;

        [SetUp]
        public void SetUp()
        {
            inner = new InMemorySaveStore();
            flushes = 0;
            store = new WebGlSyncingSaveStore(inner, () => flushes++);
        }

        [Test]
        public void Write_StoresTheValueAndFlushesOnce()
        {
            store.Write("hero", "{}");

            Assert.That(inner.TryRead("hero", out var content), Is.True);
            Assert.That(content, Is.EqualTo("{}"));
            Assert.That(flushes, Is.EqualTo(1));
        }

        [Test]
        public void Delete_RemovesTheValueAndFlushes()
        {
            store.Write("hero", "{}");
            flushes = 0;

            Assert.That(store.Delete("hero"), Is.True);
            Assert.That(inner.TryRead("hero", out _), Is.False);
            Assert.That(flushes, Is.EqualTo(1));
        }

        [Test]
        public void SetAside_AndAppendSideFile_EachFlush()
        {
            store.Write("hero", "{}");
            flushes = 0;

            Assert.That(store.SetAside("hero"), Is.True);
            store.AppendSideFile("quarantine.log", "line");

            Assert.That(flushes, Is.EqualTo(2));
        }

        [Test]
        public void Reads_PassThroughWithoutFlushing()
        {
            inner.Write("hero", "{}");

            Assert.That(store.TryRead("hero", out _), Is.True);
            Assert.That(store.Keys(), Does.Contain("hero"));
            Assert.That(flushes, Is.EqualTo(0));
        }
    }
}
