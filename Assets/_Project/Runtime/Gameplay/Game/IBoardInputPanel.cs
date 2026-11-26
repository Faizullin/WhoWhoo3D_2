using UnityEngine;

namespace WhoWhoo3D.Gameplay.Game;

public interface IBoardInputPanel
{
    bool CoversScreenPoint(Vector2 screenPosition);
}
