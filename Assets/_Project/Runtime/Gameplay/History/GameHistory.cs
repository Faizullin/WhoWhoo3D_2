using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;

namespace WhoWhoo3D.Gameplay.History
{
    /// <summary>
    /// The record of everything that has happened, with a cursor for stepping through it.
    ///
    /// Pure C#, no UnityEngine — the whole timeline is unit-testable without a scene.
    ///
    /// The cursor sits *between* entries: -1 means "before anything happened", and
    /// <see cref="Entries"/>.Count - 1 means "the present". Stepping does not touch the
    /// board; it hands back the entry to display, and the caller renders it. Only
    /// <see cref="TruncateAfterCursor"/> discards anything.
    /// </summary>
    public sealed class GameHistory
    {
        private readonly List<HistoryEntry> _entries = new();

        public IReadOnlyList<HistoryEntry> Entries => _entries;

        /// <summary>Index of the last applied entry. -1 is the state before the first move.</summary>
        public int Cursor { get; private set; } = -1;

        public bool IsAtPresent => Cursor == _entries.Count - 1;
        public bool CanStepBack => Cursor >= 0;
        public bool CanStepForward => Cursor < _entries.Count - 1;

        public void Clear()
        {
            _entries.Clear();
            Cursor = -1;
        }

        /// <summary>
        /// Appends an entry. Recording while scrubbed into the past drops the abandoned
        /// future first — the same branch-on-edit rule any undo stack uses.
        /// </summary>
        public void Record(HistoryEntry entry)
        {
            if (!IsAtPresent)
                TruncateAfterCursor();

            _entries.Add(entry);
            Cursor = _entries.Count - 1;
        }

        /// <summary>Discards every entry after the cursor. Used when play resumes from the past.</summary>
        public void TruncateAfterCursor()
        {
            int keep = Cursor + 1;
            if (keep < _entries.Count)
                _entries.RemoveRange(keep, _entries.Count - keep);
        }

        /// <summary>
        /// Moves back one entry and returns it. Apply each change's <c>Before</c> to display
        /// the earlier state. Null when already at the beginning.
        /// </summary>
        public HistoryEntry StepBack()
        {
            if (!CanStepBack) return null;

            var entry = _entries[Cursor];
            Cursor--;
            return entry;
        }

        /// <summary>
        /// Moves forward one entry and returns it. Apply each change's <c>After</c>.
        /// Null when already at the present.
        /// </summary>
        public HistoryEntry StepForward()
        {
            if (!CanStepForward) return null;

            Cursor++;
            return _entries[Cursor];
        }

        /// <summary>
        /// The owner every touched slot had at the cursor's position, reconstructed from the
        /// live board by reversing everything after the cursor. Used to paint the view when
        /// jumping rather than stepping.
        /// </summary>
        public Dictionary<SlotId, PlayerId> DisplayStateAtCursor(BoardModel liveBoard)
        {
            var display = new Dictionary<SlotId, PlayerId>();

            // Walk backwards from the present, so the earliest Before wins for each slot.
            for (int i = _entries.Count - 1; i > Cursor; i--)
                foreach (var change in _entries[i].Changes)
                    display[change.Slot] = change.Before;

            return display;
        }

        /// <summary>Every slot the timeline has ever touched — what the view must reset.</summary>
        public IEnumerable<SlotId> TouchedSlots()
        {
            var seen = new HashSet<SlotId>();

            foreach (var entry in _entries)
                foreach (var change in entry.Changes)
                    if (seen.Add(change.Slot))
                        yield return change.Slot;
        }
    }
}
