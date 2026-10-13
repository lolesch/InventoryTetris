using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI
{
    /// <summary>
    /// The game's scene change through the <see cref="SceneProvider"/>, which says nothing when it declines
    /// one: it refuses a load while its previous one is still fading out, with a log line and no result. This
    /// answers whether the load was started, from the loads that go through it, so a button locks itself
    /// only once leaving really began instead of staying dead after a refusal.
    /// </summary>
    public static class SceneRequest
    {
        private static bool underway;

        /// <summary>Asks for <paramref name="scene"/>. <c>false</c> when a load requested here has not finished, the name is blank or no loader exists.</summary>
        public static bool TryLoad(string scene)
        {
            if (underway || string.IsNullOrWhiteSpace(scene))
                return false;

            var provider = SceneProvider.Instance;

            if (!provider)
                return false;

            underway = true;
            provider.OnSceneLoaded += Finished;
            provider.LoadScene(scene);
            return true;

            void Finished(string _)
            {
                provider.OnSceneLoaded -= Finished;
                underway = false;
            }
        }

        // A Play entry keeps statics with domain reload off; a load that never finished must not outlive its session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => underway = false;
    }
}
