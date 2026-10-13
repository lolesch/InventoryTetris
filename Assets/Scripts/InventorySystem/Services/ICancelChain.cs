using Submodules.Utility.Services;
using System;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the one back-out key does: a ranked chain of layers (<see cref="CancelRank"/>), each press given
    /// to the first layer that consumes it. Installed with the services, so the Main Menu scene has it as
    /// well as the game.
    /// </summary>
    public interface ICancelChain : IService
    {
        /// <summary>
        /// Adds a layer under <paramref name="rank"/>. <paramref name="handler"/> returns whether it consumed
        /// the press; a layer with nothing to do returns <c>false</c> and the press moves down. Within a rank
        /// the most recently registered layer is asked first. Disposing the result removes the layer.
        /// </summary>
        IDisposable Register(CancelRank rank, Func<bool> handler);

        /// <summary>
        /// One Cancel press: asks the layers top rank first and stops at the first that consumes it.
        /// The Cancel key calls this; a test or a button may too.
        /// </summary>
        /// <returns>Whether a layer consumed the press.</returns>
        bool Cancel();
    }
}
