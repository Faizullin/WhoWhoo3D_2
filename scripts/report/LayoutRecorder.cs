using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Networking;
using WhoWhoo3D.UI.HUD;

// Only compiled into the disposable report player. Render at real pixel dimensions regardless of monitor size.
internal sealed class LayoutRecorder
{
    private static readonly Vector2Int[] Sizes = { new(800, 480), new(1280, 720), new(1920, 1080), new(3440, 1440), new(360, 640) };
    private readonly WaitForEndOfFrame _end = new();
    private readonly string _directory;
    private readonly GameController _game;
    private readonly ScoreboardView _hud;
    private readonly Camera _camera;
    private readonly Canvas[] _canvases;

    public LayoutRecorder(GameController game, string directory)
    {
        _game = game;
        _hud = game.GetComponent<ScoreboardView>();
        _camera = Camera.main;
        _directory = Path.Combine(directory, "layouts");
        Directory.CreateDirectory(_directory);
        _canvases = new[] { "Match HUD", "Match screens" }.Select(name => game.transform.Find(name).GetComponent<Canvas>()).ToArray();
    }

    public IEnumerator Run()
    {
        bool previousLarge = PresentationSettings.LargeText;
        try { yield return CaptureScreens(); }
        finally { PresentationSettings.LargeText = previousLarge; }
    }

    private IEnumerator CaptureScreens()
    {
        _hud.enabled = false;
        foreach (var canvas in _canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = _camera.nearClipPlane + .1f;
        }
        yield return Capture("setup");
        var room = _game.GetComponent<LanMatchSession>();
        room.Host("terrace6x6", 4, 0, "Terrace evening", true);
        yield return Capture("room");
        room.Leave();
        _game.StartMatch(new MatchSetup("terrace6x6", true, new SeatControl[4]));
        _game.SelectAbility(MatchAction.Freeze);
        var target = _game.Board.Cells.Values.SelectMany(c => c.ExposedSides).SelectMany(f => f.Slots)
            .First(s => _game.Abilities.CanTarget(MatchAction.Freeze, s.Id));
        _game.OnSlotClicked(target.Id);
        yield return Capture("target");
        _game.SelectAbility(MatchAction.Claim);
        _hud.Layout(Screen.safeArea, new Vector2(Screen.width, Screen.height), 1);
        _game.GetComponentsInChildren<Button>().Single(b => b.name == "Ability info").onClick.Invoke();
        yield return Capture("guide");
        _game.DeclareGameOver(new PlayerId(1));
        yield return Capture("result");
        File.WriteAllText(Path.Combine(_directory, "complete"), "50 layouts verified");
    }

    private IEnumerator Capture(string stage)
    {
        foreach (var size in Sizes)
        foreach (int scale in new[] { 1, 2 })
        {
            PresentationSettings.LargeText = scale == 2;
            var rt = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = rt;
            _camera.aspect = (float)size.x / size.y;
            var bounds = size.x < size.y ? new Rect(12, 24, size.x - 24, size.y - 54) : new Rect(0, 0, size.x, size.y);
            _hud.Layout(bounds, size, scale);
            Canvas.ForceUpdateCanvases();
            yield return null;
            _hud.Layout(bounds, size, scale);
            Canvas.ForceUpdateCanvases();
            yield return _end;
            string name = $"{size.x}x{size.y}-{scale}x-{stage}";
            var canvas = _canvases.Single(c => c.gameObject.activeSelf);
            CheckControls(canvas, bounds, name);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(_directory, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private void CheckControls(Canvas canvas, Rect bounds, string name)
    {
        var corners = new Vector3[4];
        var controls = new List<Rect>();
        var safe = (RectTransform)canvas.transform.Find("Safe area");
        Vector2 Pixel(Vector3 world) => _camera.WorldToScreenPoint(world);
        safe.GetWorldCorners(corners);
        if (Vector2.Distance(Pixel(corners[0]), bounds.min) > 2 || Vector2.Distance(Pixel(corners[2]), bounds.max) > 2)
            throw new InvalidOperationException($"Safe area mismatch in {name}: {Pixel(corners[0])}..{Pixel(corners[2])}, expected {bounds}");
        var guide = safe.Find("Ability guide");
        Transform scope = guide && guide.gameObject.activeInHierarchy ? guide : safe;
        foreach (var control in scope.GetComponentsInChildren<Selectable>())
        {
            ((RectTransform)control.transform).GetWorldCorners(corners);
            var r = Rect.MinMaxRect(Pixel(corners[0]).x, Pixel(corners[0]).y, Pixel(corners[2]).x, Pixel(corners[2]).y);
            if (r.xMin < bounds.xMin - 1 || r.xMax > bounds.xMax + 1 || r.yMin < bounds.yMin - 1 || r.yMax > bounds.yMax + 1)
                throw new InvalidOperationException($"{control.name} outside safe area in {name}: {r}");
            var inset = Rect.MinMaxRect(r.xMin + 1, r.yMin + 1, r.xMax - 1, r.yMax - 1);
            if (controls.Any(other => other.Overlaps(inset))) throw new InvalidOperationException($"Overlapping control {control.name} in {name}");
            controls.Add(inset);
        }
        foreach (var label in scope.GetComponentsInChildren<TMP_Text>())
        {
            if (label.GetComponentInParent<TMP_InputField>() || string.IsNullOrWhiteSpace(label.text)) continue;
            label.ForceMeshUpdate();
            if (label.isTextOverflowing || label.preferredHeight > label.rectTransform.rect.height + 2)
                throw new InvalidOperationException($"Clipped {label.name} in {name}: {label.text}");
        }
    }
}
