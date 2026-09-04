using NUnit.Framework;
using Submodules.Utility.Tools.Tweening;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Utility
{
    /// <summary>
    /// Drives <see cref="Tween"/> by hand — <c>tween.Tick(dt)</c>, no scene, no PlayerLoop —
    /// exactly the manual-tick style AutoBattler's <c>TimerTests</c> use for <c>Timer</c>.
    /// Pins external behaviour only: the value handed to the applier, whether OnComplete
    /// fired and how often, and that a cancel stays silent.
    /// </summary>
    [TestFixture]
    public sealed class TweenTests
    {
        [TearDown]
        public void KillEverythingLeftRunning() => Tween.KillAll();

        [Test]
        public void PartialTick_HandsTheEasedFractionToTheApplier()
        {
            var received = float.NaN;
            var tween = Tween.Play( 1f, Ease.Linear, value => received = value );

            tween.Tick( 0.25f );

            // Linear: the eased fraction is the raw progress.
            Assert.That( received, Is.EqualTo( 0.25f ).Within( 1e-5f ) );
        }

        [Test]
        public void PartialTick_RoutesTheProgressThroughTheEaseCurve()
        {
            var received = float.NaN;
            var tween = Tween.Play( 1f, Ease.InQuad, value => received = value );

            tween.Tick( 0.5f );

            // InQuad(0.5) = 0.25 — a known literal, not the implementation's formula.
            Assert.That( received, Is.EqualTo( 0.25f ).Within( 1e-5f ) );
        }

        [Test]
        public void Start_DoesNotFireOnComplete()
        {
            var completions = 0;
            Tween.Play( 1f, Ease.Linear, _ => { } ).OnComplete( () => completions++ );

            Assert.That( completions, Is.Zero );
        }

        [Test]
        public void OnComplete_FiresExactlyOnce_WhenTheDurationElapses()
        {
            var completions = 0;
            var tween = Tween.Play( 1f, Ease.Linear, _ => { } ).OnComplete( () => completions++ );

            tween.Tick( 0.6f );
            Assert.That( completions, Is.Zero, "not before the duration is up" );

            tween.Tick( 0.6f );
            Assert.That( completions, Is.EqualTo( 1 ), "once, as the duration elapses" );

            tween.Tick( 1f );
            tween.Tick( 1f );
            Assert.That( completions, Is.EqualTo( 1 ), "and never again" );
        }

        [Test]
        public void OnComplete_HandsTheApplierTheFinalValue()
        {
            var received = float.NaN;
            var tween = Tween.Play( 1f, Ease.InQuad, value => received = value );

            tween.Tick( 2f );

            Assert.That( received, Is.EqualTo( 1f ).Within( 1e-5f ) );
        }

        [Test]
        public void Kill_CancelsSilently()
        {
            var completions = 0;
            var tween = Tween.Play( 1f, Ease.Linear, _ => { } ).OnComplete( () => completions++ );

            tween.Tick( 0.4f );
            tween.Kill();
            tween.Tick( 1f );

            Assert.That( completions, Is.Zero );
            Assert.That( tween.IsRunning, Is.False );
        }

        [Test]
        public void KillByTarget_CancelsEveryTweenOnThatTarget_Silently()
        {
            var target = new object();
            var completions = 0;

            Tween.Play( 1f, Ease.Linear, _ => { } ).SetTarget( target ).OnComplete( () => completions++ );
            Tween.Play( 1f, Ease.Linear, _ => { } ).SetTarget( target ).OnComplete( () => completions++ );
            var other = Tween.Play( 1f, Ease.Linear, _ => { } ).SetTarget( new object() );

            Assert.That( Tween.IsTweening( target ), Is.True );

            Tween.Kill( target );

            Assert.That( Tween.IsTweening( target ), Is.False );
            Assert.That( completions, Is.Zero );
            Assert.That( other.IsRunning, Is.True, "a tween on a different target is untouched" );
        }

        [Test]
        public void IsTweening_IsFalse_OnceTheTweenCompletes()
        {
            var target = new object();
            var tween = Tween.Play( 1f, Ease.Linear, _ => { } ).SetTarget( target );

            tween.Tick( 2f );

            Assert.That( Tween.IsTweening( target ), Is.False );
        }

        [Test]
        public void ATweenLinkedToADestroyedObject_StopsWithoutThrowingOrCompleting()
        {
            var go = new GameObject( "linked" );
            var canvasGroup = go.AddComponent<CanvasGroup>();
            var completions = 0;

            var tween = Tween.Play( 1f, Ease.Linear, value => canvasGroup.alpha = value )
                .LinkTo( canvasGroup )
                .OnComplete( () => completions++ );

            Object.DestroyImmediate( go );

            Assert.DoesNotThrow( () => tween.Tick( 0.5f ) );
            Assert.That( tween.IsRunning, Is.False );
            Assert.That( completions, Is.Zero );
        }

        [Test]
        public void RepeatedStartToCompleteCycles_ReuseTheSameHandle()
        {
            var first = Tween.Play( 1f, Ease.Linear, _ => { } );
            first.Tick( 1f ); // elapses -> handle returns to the pool

            var second = Tween.Play( 1f, Ease.Linear, _ => { } );

            Assert.That( second, Is.SameAs( first ), "the completed handle was pooled and handed back" );
        }

        [Test]
        public void AKilledHandle_ReturnsToThePool()
        {
            var first = Tween.Play( 1f, Ease.Linear, _ => { } );
            first.Kill();

            var second = Tween.Play( 1f, Ease.Linear, _ => { } );

            Assert.That( second, Is.SameAs( first ) );
        }

        [Test]
        public void ActiveCount_TracksRunningTweens()
        {
            Assert.That( Tween.ActiveCount, Is.Zero );

            var a = Tween.Play( 1f, Ease.Linear, _ => { } );
            var b = Tween.Play( 1f, Ease.Linear, _ => { } );
            Assert.That( Tween.ActiveCount, Is.EqualTo( 2 ) );

            a.Tick( 2f );
            b.Kill();
            Assert.That( Tween.ActiveCount, Is.Zero );
        }
    }
}
