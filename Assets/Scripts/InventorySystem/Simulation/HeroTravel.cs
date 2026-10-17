using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The pure model behind a Hero figure's walk (ADR-0021): a position, a target and an arrival that is
    /// reported once per target. The figure owns the space it walks in and the speed it reads from the hero.
    /// </summary>
    public sealed class HeroTravel : IHeroTravel
    {
        private Coordinate _target;

        public HeroTravel(Coordinate start)
        {
            Position = start;
            _target = start;
        }

        public Coordinate Position { get; private set; }

        public bool IsWalking { get; private set; }

        public void Retarget(Coordinate target)
        {
            _target = target;
            IsWalking = true;
        }

        public void Place(Coordinate position)
        {
            Position = position;
            _target = position;
            IsWalking = false;
        }

        public bool Advance(float speed, float deltaSeconds)
        {
            if (!IsWalking) return false;

            // Written so a NaN speed or step fails the comparison and stands still instead of poisoning the position.
            var step = speed * deltaSeconds;
            var remaining = Coordinate.Distance(Position, _target);
            if (remaining <= 0f)
                return Arrive();
            if (!(step > 0f))
                return false;
            if (step >= remaining)
            {
                Position = _target;
                return Arrive();
            }

            Position = Coordinate.MoveTowards(Position, _target, step);
            return false;
        }

        private bool Arrive()
        {
            IsWalking = false;
            return true;
        }
    }
}
