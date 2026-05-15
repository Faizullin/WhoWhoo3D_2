using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View
{
    public class TileView : MonoBehaviour
    {
        public PlayerId Owner { get; private set; }
        public int Count { get; private set; }

        private GameObject _visual;

        public void Refresh(TileStack stack)
        {
            Owner = stack.Owner;
            Count = stack.Count;
            gameObject.name = $"Tile_{stack.Owner}x{stack.Count}";

            if (_visual == null)
            {
                _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _visual.transform.SetParent(transform, false);
                Destroy(_visual.GetComponent<Collider>());
            }

            float size = 0.15f + Count * 0.04f;
            _visual.transform.localScale = Vector3.one * size;
            _visual.GetComponent<Renderer>().material.color = OwnerColor(Owner);
        }

        private static Color OwnerColor(PlayerId id)
        {
            switch (id.Value)
            {
                case 1: return Color.red;
                case 2: return Color.blue;
                case 3: return Color.green;
                case 4: return Color.yellow;
                default: return Color.white;
            }
        }
    }
}
