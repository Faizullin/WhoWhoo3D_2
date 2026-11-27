using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;

// Report-player only. Select the longest chain in one complete seeded match,
// then measure that same action through the shipping presentation in each motion mode.
internal static class WaveProfile
{
    [Serializable]
    private sealed class Sample
    {
        public int frames;
        public float medianMs, p95Ms, maxMs;
    }

    [Serializable]
    private sealed class Result
    {
        public int seed = 42, turns, waveTurn, steps, changes;
        public string device;
        public Sample normal, reduced;
    }

    public static IEnumerator Run(GameController game, string directory)
    {
        Time.captureFramerate = 0;
        var setup = new MatchSetup("terrace6x6", new SeatControl[2]);
        var match = new GameObject("Profile reference").AddComponent<GameController>();
        match.enabled = false;
        var result = new Result { device = SystemInfo.graphicsDeviceName };
        var random = new System.Random(result.seed);
        MatchSnapshot before = null;
        string expected = null;
        SlotId selected = default;
        try
        {
            match.StartMatchWithoutPresentation(setup);
            while (!match.IsGameOver)
            {
                if (++result.turns > 2048) throw new InvalidOperationException("Terrace reference match stalled");
                var legal = BoardActions.LegalMoves(match.Board, match.ActivePlayer.Id);
                var slot = legal[random.Next(legal.Count)];
                var opening = MatchSnapshot.Capture(match);
                int first = match.History.Entries.Count;
                if (!match.TryPlayerAction(slot)) throw new InvalidOperationException("Reference claim failed");
                var steps = match.History.Entries.Skip(first).Where(e => e.Kind == HistoryKind.WaveStep).ToArray();
                int changes = steps.Sum(e => e.Changes.Count);
                if (steps.Length > result.steps || (steps.Length == result.steps && changes > result.changes))
                {
                    before = opening;
                    selected = slot;
                    expected = JsonUtility.ToJson(MatchSnapshot.Capture(match));
                    result.waveTurn = opening.turn;
                    result.steps = steps.Length;
                    result.changes = changes;
                }
            }
        }
        finally { UnityEngine.Object.Destroy(match.gameObject); }

        if (before == null) throw new InvalidOperationException("No wave to measure");
        bool preference = PresentationSettings.ReducedMotion;
        try
        {
            foreach (bool reduced in new[] { false, true })
            {
                PresentationSettings.ReducedMotion = reduced;
                game.RestoreMatch(setup, before);
                for (int i = 0; i < 12; i++) yield return null;
                var samples = new List<float>();
                double previous = Time.realtimeSinceStartupAsDouble;
                if (!game.TryPlayerAction(selected) || !game.WaveAnimator.IsPlaying)
                    throw new InvalidOperationException("Expected a visible terrace wave");
                do
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    samples.Add((float)((now - previous) * 1000));
                    previous = now;
                }
                while (game.WaveAnimator.IsPlaying || !(game.IsGameOver || game.AcceptsBoardInput));
                if (JsonUtility.ToJson(MatchSnapshot.Capture(game)) != expected)
                    throw new InvalidOperationException("Profile playback diverged from its reference");
                samples.Sort();
                var sample = new Sample { frames = samples.Count,
                    medianMs = samples[(samples.Count - 1) / 2],
                    p95Ms = samples[Mathf.CeilToInt(samples.Count * .95f) - 1],
                    maxMs = samples[samples.Count - 1] };
                if (reduced) result.reduced = sample;
                else result.normal = sample;
            }
        }
        finally { PresentationSettings.ReducedMotion = preference; }
        File.WriteAllText(Path.Combine(directory, "wave-profile.json"), JsonUtility.ToJson(result));
    }
}
