using NUnit.Framework;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Utility
{
    /// <summary>
    /// The five property tweens the ported components need. Each must return a handle,
    /// lerp its property from the value it had at the call, and wire the component in as
    /// both the kill-target and the destroy-link.
    /// </summary>
    [TestFixture]
    public sealed class TweenExtensionsTests
    {
        private GameObject host;

        [SetUp]
        public void MakeHost() => host = new GameObject( "tween-host", typeof( RectTransform ) );

        [TearDown]
        public void Cleanup()
        {
            Tween.KillAll();

            if ( host != null )
                Object.DestroyImmediate( host );
        }

        [Test]
        public void TweenAlpha_ReturnsAHandle_AndLerpsTheCanvasGroupAlpha()
        {
            var canvasGroup = host.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var tween = canvasGroup.TweenAlpha( 1f, 1f, Ease.Linear );
            Assert.That( tween, Is.Not.Null );

            tween.Tick( 0.5f );
            Assert.That( canvasGroup.alpha, Is.EqualTo( 0.5f ).Within( 1e-4f ) );

            tween.Tick( 1f );
            Assert.That( canvasGroup.alpha, Is.EqualTo( 1f ).Within( 1e-4f ) );
        }

        [Test]
        public void TweenAnchoredPosition_ReturnsAHandle_AndLerpsTheRectTransform()
        {
            var rect = host.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;

            var tween = rect.TweenAnchoredPosition( new Vector2( 100f, -40f ), 1f, Ease.Linear );
            Assert.That( tween, Is.Not.Null );

            tween.Tick( 0.5f );
            Assert.That( rect.anchoredPosition.x, Is.EqualTo( 50f ).Within( 1e-3f ) );
            Assert.That( rect.anchoredPosition.y, Is.EqualTo( -20f ).Within( 1e-3f ) );
        }

        [Test]
        public void TweenScale_ReturnsAHandle_AndLerpsTheLocalScaleFromAUniformTarget()
        {
            var tween = host.transform.TweenScale( 2f, 1f, Ease.Linear );
            Assert.That( tween, Is.Not.Null );

            tween.Tick( 0.5f );
            Assert.That( host.transform.localScale.x, Is.EqualTo( 1.5f ).Within( 1e-3f ) );
        }

        [Test]
        public void TweenColor_ReturnsAHandle_AndLerpsTheGraphicColour()
        {
            var image = host.AddComponent<Image>();
            image.color = Color.black;

            var tween = image.TweenColor( Color.white, 1f, Ease.Linear );
            Assert.That( tween, Is.Not.Null );

            tween.Tick( 0.5f );
            Assert.That( image.color.r, Is.EqualTo( 0.5f ).Within( 1e-3f ) );
        }

        [Test]
        public void TweenFillAmount_ReturnsAHandle_AndLerpsTheImageFill()
        {
            var image = host.AddComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillAmount = 0f;

            var tween = image.TweenFillAmount( 1f, 1f, Ease.Linear );
            Assert.That( tween, Is.Not.Null );

            tween.Tick( 0.25f );
            Assert.That( image.fillAmount, Is.EqualTo( 0.25f ).Within( 1e-3f ) );
        }

        [Test]
        public void AnExtensionTween_LinksToItsComponent_SoADestroyedHostCancelsItSilently()
        {
            var canvasGroup = host.AddComponent<CanvasGroup>();
            var tween = canvasGroup.TweenAlpha( 1f, 1f );

            Object.DestroyImmediate( host );

            Assert.DoesNotThrow( () => tween.Tick( 0.5f ) );
            Assert.That( tween.IsRunning, Is.False );
        }

        [Test]
        public void AnExtensionTween_TargetsItsComponent_SoKillByTargetStopsIt()
        {
            var canvasGroup = host.AddComponent<CanvasGroup>();
            var tween = canvasGroup.TweenAlpha( 1f, 1f );

            Assert.That( Tween.IsTweening( canvasGroup ), Is.True );

            Tween.Kill( canvasGroup );

            Assert.That( tween.IsRunning, Is.False );
            Assert.That( Tween.IsTweening( canvasGroup ), Is.False );
        }
    }
}
