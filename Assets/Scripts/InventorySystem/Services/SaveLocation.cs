using Submodules.Utility.Persistence;
using System;
using System.IO;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Where the hero files live, and the one-click clearing of them. The folder is chosen here, once,
    /// and everything else takes a store, so a test never touches it.
    /// </summary>
    public static class SaveLocation
    {
        private const string FolderArgument = "-savesFolder";

        /// <summary>
        /// A folder under the platform's persistent data path, or the folder named by
        /// <c>-savesFolder &lt;path&gt;</c> on the command line, so a headless run (the Play-exit check, a
        /// PlayMode run) points at a scratch folder and never writes the player's saves.
        /// </summary>
        public static string Folder()
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == FolderArgument && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1];
            }

            return Path.Combine(Application.persistentDataPath, "saves");
        }

        /// <summary>
        /// Deletes every hero save and the Account file in <paramref name="store"/>. A damaged save that was set
        /// aside, and a quarantine sidecar, are evidence and stay.
        /// </summary>
        /// <returns>How many saves were deleted.</returns>
        public static int WipeAll(ISaveStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var deleted = 0;

            foreach (var key in store.Keys())
            {
                if (store.Delete(key))
                    deleted++;
            }

            return deleted;
        }
    }
}
