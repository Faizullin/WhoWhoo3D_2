using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Input;

// Local presentation owned by the scene's pointer handler. Match snapshots never contain camera state.
public sealed class BoardCamera
{
    private readonly Camera _camera;
    private Bounds _bounds;
    private Vector3 _pivot;
    private float _yaw, _pitch, _zoom, _fitDistance;
    private float _aspect;
    private BoardModel _board;

    public BoardCamera(Camera camera) => _camera = camera;

    public void Frame(BoardModel board)
    {
        if (board == null || board.Cells.Count == 0) return;
        if (_board != board)
        {
            _board = board;
            bool first = true;
            foreach (var cell in board.Cells.Values)
            {
                var p = cell.Position;
                var cube = new Bounds(new Vector3(p.X, p.Y, p.Z), Vector3.one * 1.12f);
                if (first) _bounds = cube;
                else _bounds.Encapsulate(cube);
                first = false;
            }
            Reset();
        }
        else if (!Mathf.Approximately(_aspect, _camera.aspect)) Apply();
    }

    public void Reset()
    {
        _pivot = _bounds.center;
        _yaw = _bounds.size.y > 1.5f ? -28f : 0f;
        _pitch = 52f;
        _zoom = 1f;
        Apply();
    }

    public void Orbit(float yaw, float pitch)
    {
        _yaw = Mathf.Repeat(_yaw + yaw, 360f);
        _pitch = Mathf.Clamp(_pitch + pitch, 15f, 82f);
        Apply();
    }

    public void Pan(Vector2 pixels)
    {
        var plane = Quaternion.Euler(0, _yaw, 0);
        float units = 2 * _fitDistance * _zoom * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad / 2)
            / Mathf.Max(1, _camera.pixelHeight);
        var position = _pivot - plane * new Vector3(pixels.x, 0, pixels.y) * units;
        _pivot = _bounds.ClosestPoint(position);
        Apply();
    }

    public void Zoom(float steps)
    {
        _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-steps * .13f), .45f, 2.2f);
        Apply();
    }

    private void Apply()
    {
        _aspect = Mathf.Max(.01f, _camera.aspect);
        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        var inverse = Quaternion.Inverse(rotation);
        var ext = _bounds.extents;
        float tan = Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad / 2);
        _fitDistance = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var p = inverse * new Vector3(ext.x * x, ext.y * y, ext.z * z);
                    float projection = Mathf.Max(Mathf.Abs(p.x) / _aspect, Mathf.Abs(p.y)) / tan;
                    _fitDistance = Mathf.Max(_fitDistance, projection * 1.18f - p.z);
                }
        float distance = Mathf.Max(_fitDistance * _zoom, _bounds.extents.magnitude * .75f + _camera.nearClipPlane);
        _camera.transform.SetPositionAndRotation(_pivot - rotation * Vector3.forward * distance, rotation);
    }
}
