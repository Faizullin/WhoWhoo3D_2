using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View
{
    public class CellView : MonoBehaviour
    {
        private static readonly Vector3[] FaceOffsets =
        {
            new Vector3( 0,     0.5f,  0),    // 0: top
            new Vector3( 0,    -0.5f,  0),    // 1: bottom
            new Vector3( 0.5f,  0,     0),    // 2: right  (+X)
            new Vector3(-0.5f,  0,     0),    // 3: left   (-X)
            new Vector3( 0,     0,     0.5f), // 4: front  (+Z)
            new Vector3( 0,     0,    -0.5f), // 5: back   (-Z)
        };

        public CellId CellId { get; private set; }

        private readonly List<SideView> _sideViews = new List<SideView>();

        public void Init(CellId id, Vector3 position, IReadOnlyList<Side> sides)
        {
            CellId = id;
            transform.localPosition = position;
            gameObject.name = id.Value;

            for (int i = 0; i < sides.Count; i++)
            {
                var sideGo = new GameObject($"Side_{i}");
                sideGo.transform.SetParent(transform, false);
                sideGo.transform.localPosition = i < FaceOffsets.Length
                    ? FaceOffsets[i]
                    : Vector3.zero;

                var sideView = sideGo.AddComponent<SideView>();
                sideView.Init(sides[i]);
                _sideViews.Add(sideView);
            }
        }

        public SideView GetSideView(int localIndex) => _sideViews[localIndex];
    }
}
