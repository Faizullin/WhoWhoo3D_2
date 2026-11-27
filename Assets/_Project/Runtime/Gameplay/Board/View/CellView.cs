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
        _cell = cell;
        CellId = cell.Id;
        transform.localPosition = position;
        var mesh = GetComponent<MeshFilter>();
        if (mesh != null) mesh.sharedMesh = BoardMeshes.Cube;
        _body = GetComponent<Renderer>();
        if (_body != null)
        {
            _body.sharedMaterial = SlotView.SharedMaterial();
            _surface = new MaterialPropertyBlock();
            PaintBody(false);
        }
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

    private readonly Dictionary<int, Renderer> _readyFaces = new();
    private Cell _cell;
    private GameObject _iceRims;

    public void SetExpansionReady(int face, bool ready)
    {
        if (!_readyFaces.TryGetValue(face, out var mark))
        {
            if (!ready) return;
            var rotation = CubeGeometry.SlotLocalRotation(face, 0) * Quaternion.Euler(0, 45, 0);
            mark = BoardMeshes.Frame(transform, $"One claim away {face}", CubeGeometry.FaceNormal(face) * .506f,
                rotation, .225f, new Color32(255, 210, 113, 255));
            _readyFaces.Add(face, mark);
        }
        mark.enabled = ready;
    }

    private Renderer _body;
    private MaterialPropertyBlock _surface;
    private bool _frozen;

    public void SetFrozen(bool frozen)
    {
        if (_frozen == frozen) return;
        _frozen = frozen;
        PaintBody(frozen);
        if (frozen && _iceRims == null)
        {
            _iceRims = new GameObject("Frost rim");
            _iceRims.transform.SetParent(transform, false);
            foreach (var side in _cell.ExposedSides)
                BoardMeshes.Frame(_iceRims.transform, $"Face {side.LocalIndex}", CubeGeometry.FaceNormal(side.LocalIndex) * .508f,
                    CubeGeometry.SlotLocalRotation(side.LocalIndex, 0), .97f, new Color32(182, 242, 255, 255));
        }
        if (_iceRims != null) _iceRims.SetActive(frozen);
    }

    private void PaintBody(bool frozen)
    {
        if (_body == null) return;
        Color color = frozen ? new Color32(84, 168, 202, 255) : new Color32(65, 84, 101, 255);
        _surface.SetColor("_BaseColor", color);
        _surface.SetColor("_Color", color);
        _body.SetPropertyBlock(_surface);
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

