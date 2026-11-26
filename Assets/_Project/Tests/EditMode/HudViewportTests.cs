using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.UI;

namespace WhoWhoo3D.Tests.EditMode;

public class HudViewportTests
{
    [TestCase(360, 640, 1f)]
    [TestCase(800, 480, 2f)]
    [TestCase(1280, 720, 1f)]
    [TestCase(3440, 1440, 1f)]
    [TestCase(1920, 1080, 4f)]
    public void PanelsFitInsetViewportAndInputUsesTheSameTransform(int width, int height, float scale)
    {
        var safe = new Rect(12, 24, width - 30, height - 38);
        var view = new HudViewport(safe, height, scale);
        Assert.That(view.Size.x, Is.GreaterThanOrEqualTo(359.99f));
        Assert.That(view.Size.y, Is.GreaterThanOrEqualTo(409.99f));
        var pixels = view.Pixels(new Rect(Vector2.zero, view.Size));
        Assert.That(pixels.xMin, Is.EqualTo(safe.xMin).Within(.01));
        Assert.That(pixels.yMin, Is.EqualTo(height - safe.yMax).Within(.01));
        Assert.That(pixels.width, Is.EqualTo(safe.width).Within(.01));
        Assert.That(pixels.height, Is.EqualTo(safe.height).Within(.01));
        var point = new Vector2(37, 42);
        Vector2 drawn = view.Matrix.MultiplyPoint3x4(point);
        Assert.That(Vector2.Distance(drawn, view.Pixels(new Rect(point, Vector2.one)).position), Is.LessThan(.01f));
    }
}
