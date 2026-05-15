using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View
{
    public class SideView : MonoBehaviour
    {
        private Side _side;
        private TileView _tileView;

        public void Init(Side side)
        {
            _side = side;
            Refresh();
        }

        public void Refresh()
        {
            if (_side.Stack.IsEmpty)
            {
                if (_tileView != null)
                {
                    Destroy(_tileView.gameObject);
                    _tileView = null;
                }
                return;
            }

            if (_tileView == null)
            {
                var go = new GameObject("Tile");
                go.transform.SetParent(transform, false);
                _tileView = go.AddComponent<TileView>();
            }

            _tileView.Refresh(_side.Stack);
        }
    }
}
