using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The walk a Hero figure makes outside an Encounter (issue #315, ADR-0021). Speed is in units per second,
    /// the unit of whatever space the figure walks in; the model neither knows nor cares.
    /// </summary>
    [TestFixture]
    public sealed class HeroTravelTests
    {
        private static readonly Coordinate Origin = new(0f, 0f);

        private static HeroTravel StandingAt(Coordinate at) => new(at);

        [Test]
        public void A_traveller_stands_where_it_was_placed_and_walks_nowhere()
        {
            var travel = StandingAt(new Coordinate(3f, 4f));

            var arrived = travel.Advance(10f, 1f);

            Assert.That(travel.Position, Is.EqualTo(new Coordinate(3f, 4f)));
            Assert.That(arrived, Is.False);
            Assert.That(travel.IsWalking, Is.False);
        }

        [Test]
        public void Advancing_moves_toward_the_target_at_the_given_speed()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(10f, 0f));

            travel.Advance(2f, 0.5f);

            Assert.That(travel.Position, Is.EqualTo(new Coordinate(1f, 0f)));
            Assert.That(travel.IsWalking, Is.True);
        }

        [Test]
        public void Advancing_moves_along_the_straight_line_to_a_diagonal_target()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(30f, 40f));

            travel.Advance(10f, 1f);

            Assert.That(Coordinate.Approximately(travel.Position, new Coordinate(6f, 8f)), Is.True);
        }

        [Test]
        public void A_step_that_covers_the_rest_stops_exactly_on_the_target()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(3f, 0f));

            var arrived = travel.Advance(100f, 1f);

            Assert.That(travel.Position, Is.EqualTo(new Coordinate(3f, 0f)));
            Assert.That(arrived, Is.True);
            Assert.That(travel.IsWalking, Is.False);
        }

        [Test]
        public void A_step_that_lands_exactly_on_the_target_arrives()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(4f, 0f));

            Assert.That(travel.Advance(2f, 1f), Is.False);
            Assert.That(travel.Advance(2f, 1f), Is.True);
            Assert.That(travel.Position, Is.EqualTo(new Coordinate(4f, 0f)));
        }

        [Test]
        public void Arrival_is_reported_once()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(1f, 0f));

            var first = travel.Advance(5f, 1f);
            var second = travel.Advance(5f, 1f);
            var third = travel.Advance(5f, 1f);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(third, Is.False);
        }

        [Test]
        public void A_new_target_mid_walk_turns_toward_it_from_where_the_walk_stands()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(10f, 0f));
            travel.Advance(1f, 4f); // now at (4, 0)

            travel.Retarget(new Coordinate(4f, 10f));
            var arrived = travel.Advance(1f, 3f);

            Assert.That(travel.Position, Is.EqualTo(new Coordinate(4f, 3f)));
            Assert.That(arrived, Is.False);
            Assert.That(travel.IsWalking, Is.True);
        }

        [Test]
        public void A_walk_turned_mid_way_reports_no_arrival_for_the_target_it_left()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(10f, 0f));
            travel.Advance(1f, 4f);
            travel.Retarget(new Coordinate(4f, 10f));

            var arrived = travel.Advance(1f, 10f);

            Assert.That(arrived, Is.True);
            Assert.That(travel.Position, Is.EqualTo(new Coordinate(4f, 10f)));
            Assert.That(travel.Advance(1f, 10f), Is.False);
        }

        [Test]
        public void A_new_target_after_arrival_walks_and_arrives_again()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(1f, 0f));
            travel.Advance(5f, 1f);

            travel.Retarget(new Coordinate(1f, 2f));

            Assert.That(travel.Advance(1f, 1f), Is.False);
            Assert.That(travel.Advance(1f, 1f), Is.True);
            Assert.That(travel.Position, Is.EqualTo(new Coordinate(1f, 2f)));
        }

        [Test]
        public void A_target_where_the_traveller_already_stands_arrives_on_the_next_advance()
        {
            var travel = StandingAt(new Coordinate(2f, 2f));

            travel.Retarget(new Coordinate(2f, 2f));

            Assert.That(travel.Advance(1f, 0.1f), Is.True);
            Assert.That(travel.Advance(1f, 0.1f), Is.False);
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        public void A_speed_that_cannot_walk_stands_still(float speed)
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(5f, 0f));

            var arrived = travel.Advance(speed, 1f);

            Assert.That(travel.Position, Is.EqualTo(Origin));
            Assert.That(arrived, Is.False);
            Assert.That(travel.IsWalking, Is.True);
        }

        [Test]
        public void Placing_stands_the_traveller_there_without_a_walk()
        {
            var travel = StandingAt(Origin);
            travel.Retarget(new Coordinate(10f, 0f));
            travel.Advance(1f, 2f);

            travel.Place(new Coordinate(7f, 7f));

            Assert.That(travel.Position, Is.EqualTo(new Coordinate(7f, 7f)));
            Assert.That(travel.IsWalking, Is.False);
            Assert.That(travel.Advance(5f, 5f), Is.False);
            Assert.That(travel.Position, Is.EqualTo(new Coordinate(7f, 7f)));
        }
    }
}
