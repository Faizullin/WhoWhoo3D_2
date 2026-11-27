using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private GameObject cubePrefab;

        private readonly Dictionary<CellId, CellView> _cellViews = new();

        /// <summary>
        /// Draws a board. Purely a renderer — it never loads a map and has no opinion about
        /// which one to show. GameController owns that decision; having both own it meant two
        /// Start() methods racing to render, and a duplicate map name to keep in sync.
        /// </summary>
        public void Render(BoardModel board)
        {
            ClearChildren();
            _cellViews.Clear();

            foreach (var cell in board.Cells.Values)
            {
                var go = Instantiate(cubePrefab, transform);
                var cellView = go.AddComponent<CellView>();
                cellView.Init(cell, new Vector3(cell.Position.X, cell.Position.Y, cell.Position.Z));
                _cellViews[cell.Id] = cellView;
            }
            Physics.SyncTransforms();
        }

        public bool HasHighlightedTargets { get; private set; }

        public void ShowTurnHints(BoardModel board, PlayerId player, bool relay = false)
        {
            HasHighlightedTargets = false;
            foreach (var cell in board.Cells.Values)
                foreach (var side in cell.ExposedSides)
                {
                    bool locked = !player.IsNone && !BoardActions.CanSelectSide(side, player);
                    foreach (var slot in side.Slots)
                    {
                        bool legal = !player.IsNone && (relay ? BoardActions.TryRelay(board, slot.Id, player, out _, out _)
                            : BoardActions.CanClaim(board, slot.Id, player));
                        HasHighlightedTargets |= legal;
                        GetSlotView(slot.Id)?.SetTurnHint(legal, locked, relay && legal);
                    }
                }
        }

        public CellView GetCellView(CellId id) =>
            _cellViews.TryGetValue(id, out var view) ? view : null;

        /// <summary>Resolves a tile's view, or null if the cell or slot is unknown.</summary>
        public SlotView GetSlotView(SlotId id)
        {
            var cellView = GetCellView(id.Side.Cell);
            return cellView != null ? cellView.GetSlotView(id) : null;
        }

        /// <summary>
        /// Overrides what a single tile draws, without touching the model. This is how both
        /// wave playback and history scrubbing show a state the board is not actually in.
        /// </summary>
        public void ShowOwner(SlotId id, PlayerId owner)
        {
            var slotView = GetSlotView(id);
            if (slotView != null)
                slotView.ShowOwner(owner);
        }

        /// <summary>Drops every presentation override so the views follow the model again.</summary>
        public void ClearOverrides()
        {
            foreach (var cellView in _cellViews.Values)
                cellView.ClearSlotOverrides();
        }

        /// <summary>Refreshes every tile. Call after any board mutation.</summary>
        public void RefreshAll()
        {
            foreach (var cellView in _cellViews.Values)
                cellView.RefreshAllSlots();
        }

        /// <summary>
        /// Removes previously spawned cells. Destroy() only runs at end of frame and
        /// throws outside play mode, so re-rendering twice in one frame (or from an
        /// Edit Mode test) needs the immediate variant.
        /// </summary>
        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    Destroy(child);
                }
                else
                    DestroyImmediate(child);
            }
        }
    }
}
