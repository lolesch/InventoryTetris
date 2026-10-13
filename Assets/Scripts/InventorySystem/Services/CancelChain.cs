using Submodules.Utility.Bindings;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The <see cref="ICancelChain"/> of one boot. It subscribes to the input service in the constructor and
    /// lives as long as it, like <see cref="PauseHotkey"/>, so it needs no scene object and no tick.
    /// </summary>
    public sealed class CancelChain : ICancelChain
    {
        private readonly List<Layer> layers = new();
        private long registrations;

        public CancelChain(IInputService<GameAction> input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // Held for the life of the boot: the input service is replaced with it, never outlived.
            input.Performed += action =>
            {
                if (action == GameAction.Cancel)
                    _ = Cancel();
            };
        }

        /// <summary>
        /// Registers a layer from a view that may be enabled before a boot has armed the services (an
        /// enable in Edit Mode): does nothing and returns <c>null</c> rather than throwing from the locator.
        /// </summary>
        public static IDisposable TryRegister(CancelRank rank, Func<bool> handler) =>
            ServiceLocator.IsArmed && ServiceLocator.Current.TryGet<ICancelChain>(out var chain)
                ? chain.Register(rank, handler)
                : null;

        public IDisposable Register(CancelRank rank, Func<bool> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var layer = new Layer(rank, registrations++, handler, this);
            layers.Add(layer);

            return layer;
        }

        public bool Cancel()
        {
            // A snapshot, so a layer that closes itself, or opens another, while handling does not disturb the walk.
            var ordered = layers.OrderBy(layer => layer.Rank).ThenByDescending(layer => layer.Order).ToArray();

            foreach (var layer in ordered)
            {
                if (layer.Handler())
                    return true;
            }

            return false;
        }

        private sealed class Layer : IDisposable
        {
            private readonly CancelChain owner;

            public Layer(CancelRank rank, long order, Func<bool> handler, CancelChain owner)
            {
                Rank = rank;
                Order = order;
                Handler = handler;
                this.owner = owner;
            }

            public CancelRank Rank { get; }
            public long Order { get; }
            public Func<bool> Handler { get; }

            public void Dispose() => owner.layers.Remove(this);
        }
    }
}
