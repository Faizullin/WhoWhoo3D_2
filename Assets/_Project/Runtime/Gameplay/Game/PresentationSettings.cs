using UnityEngine;

namespace WhoWhoo3D.Gameplay.Game;

/// <summary>Local display preferences. Multiplayer snapshots contain only match state.</summary>
public static class PresentationSettings
{
    private const string MotionKey = "WhoWhoo3D.ReduceMotion";
    private const string TextKey = "WhoWhoo3D.LargeText";
    private static bool? _reducedMotion, _largeText;

    public static bool LargeText
    {
        get => _largeText ??= PlayerPrefs.GetInt(TextKey, 0) != 0;
        set
        {
            _largeText = value;
            PlayerPrefs.SetInt(TextKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ReducedMotion
    {
        get => _reducedMotion ??= PlayerPrefs.GetInt(MotionKey, 0) != 0;
        set
        {
            _reducedMotion = value;
            PlayerPrefs.SetInt(MotionKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
