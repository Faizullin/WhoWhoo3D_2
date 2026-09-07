using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View;

/// <summary>
/// One cube. Spawns a SlotView for every tile slot the model gives it — only on
/// exposed faces, since an interior face is buried between cells and holds nothing.
/// </summary>
public class CellView : MonoBehaviour
{
    public CellId CellId { get; private set; }

    private readonly Dictionary<SlotId, SlotView> _slotViews = new();

    public void Init(Cell cell, Vector3 position)
    {
        CellId = cell.Id;
        transform.localPosition = position;
        gameObject.name = cell.Id.Value;

        foreach (var side in cell.ExposedSides)
        {
            foreach (var slot in side.Slots)
            {
                var go = new GameObject("Slot");
                go.transform.SetParent(transform, false);

                var view = go.AddComponent<SlotView>();
                view.Init(slot, side.LocalIndex, slot.Id.Edge);
                _slotViews[slot.Id] = view;
            }
        }
    }

    public SlotView GetSlotView(SlotId id) =>
        _slotViews.TryGetValue(id, out var view) ? view : null;

    public void RefreshAllSlots()
    {
        foreach (var view in _slotViews.Values)
            view.Refresh();
    }

    public void ClearSlotOverrides()
    {
        foreach (var view in _slotViews.Values)
            view.ClearOverride();
    }
}

