using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Tests.PlayMode;

public class MatchRecorder : MonoBehaviour
{
    private string _directory;
    private string _checkpoint;
    private int _frame;
    private bool _finished;
    private readonly WaitForEndOfFrame _endOfFrame = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var recorder = new GameObject("Session recording");
        if (Environment.GetEnvironmentVariable("WHOWHOO_LAN_ROLE") == null) recorder.AddComponent<MatchRecorder>();
        else recorder.AddComponent<LanMatchRecorder>();
    }

    private IEnumerator Start()
    {
        _directory = Environment.GetEnvironmentVariable("WHOWHOO_FRAMES");
        Application.runInBackground = true;
        Time.captureFramerate = 12;
        yield return null;
        var game = FindAnyObjectByType<GameController>();
        var board = FindAnyObjectByType<BoardView>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        // Keep OS cursor motion from taking over Pointer.current in this standalone recording.
        foreach (var device in InputSystem.devices)
            if (device is Pointer && device.native) InputSystem.DisableDevice(device);
        var mouse = InputSystem.AddDevice<Mouse>();
        mouse.MakeCurrent();
        bool compact = Environment.GetEnvironmentVariable("WHOWHOO_COMPACT_HUD") == "1";
        bool layouts = Environment.GetEnvironmentVariable("WHOWHOO_LAYOUTS") == "1";
        if (compact) yield return CaptureCompact("compact-setup");
        else if (!layouts) StartCoroutine(RecordFrames());
        var sequence = layouts ? new LayoutRecorder(game, _directory).Run() : compact ? CompactHudSequence.Run(game, board, Camera.main, mouse,
            () => StartCoroutine(CaptureCompact("compact-freeze"))) :
            DemoMatchSequence.Run(game, board, Camera.main, mouse, name => _checkpoint = name, true);
        // Unroll nested iterators so assertion failures also reach the calling script.
        var stack = new Stack<IEnumerator>();
        stack.Push(sequence);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            object value = null;
            try
            {
                if (!current.MoveNext()) { stack.Pop(); continue; }
                value = current.Current;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Application.Quit(1);
                yield break;
            }
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        _finished = true;
        yield return _endOfFrame;
        Application.Quit(0);
    }

    private IEnumerator CaptureCompact(string name)
    {
        yield return _endOfFrame;
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        if (image.width != 800 || image.height != 480)
        {
            Debug.LogError($"Expected 800×480 capture, got {image.width}×{image.height}");
            Application.Quit(1);
        }
        else File.WriteAllBytes(Path.Combine(_directory, name + ".png"), image.EncodeToPNG());
        Destroy(image);
    }

    private IEnumerator RecordFrames()
    {
        while (!_finished)
        {
            yield return _endOfFrame;
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            var png = image.EncodeToPNG();
            Destroy(image);
            File.WriteAllBytes(Path.Combine(_directory, $"frame-{_frame:D5}.png"), png);
            if (_checkpoint != null)
            {
                File.WriteAllBytes(Path.Combine(_directory, _checkpoint + ".png"), png);
                File.AppendAllText(Path.Combine(_directory, "timeline.tsv"), $"{_frame}\t{_checkpoint}\n");
                _checkpoint = null;
            }
            _frame++;
        }
    }
}
