using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// A save store that runs <c>flush</c> after every change. On WebGL the files live in a memory filesystem
    /// that reaches the browser's IndexedDB only when synced, so without it a refresh loses the saves.
    /// </summary>
    public sealed class WebGlSyncingSaveStore : ISaveStore
    {
        private readonly ISaveStore inner;
        private readonly Action flush;

        public WebGlSyncingSaveStore(ISaveStore inner, Action flush)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.flush = flush ?? throw new ArgumentNullException(nameof(flush));
        }

        /// <summary>Copies the in-memory filesystem to IndexedDB, through <c>IdbfsSync.jslib</c>.</summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "IdbfsSync_Flush")]
        public static extern void FlushToBrowser();
#else
        public static void FlushToBrowser() { }
#endif

        public bool TryRead(string key, out string content) => inner.TryRead(key, out content);

        public bool TryReadBackup(string key, out string content) => inner.TryReadBackup(key, out content);

        public IReadOnlyCollection<string> Keys() => inner.Keys();

        public void Write(string key, string content)
        {
            inner.Write(key, content);
            flush();
        }

        public bool Delete(string key)
        {
            var deleted = inner.Delete(key);
            flush();
            return deleted;
        }

        public bool SetAside(string key)
        {
            var movedAside = inner.SetAside(key);
            flush();
            return movedAside;
        }

        public void AppendSideFile(string fileName, string text)
        {
            inner.AppendSideFile(fileName, text);
            flush();
        }
    }
}
