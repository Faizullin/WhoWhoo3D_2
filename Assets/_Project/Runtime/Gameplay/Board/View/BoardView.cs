using System.IO;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;

namespace WhoWhoo3D.Gameplay.Board.View
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private string mapFileName = "sample";

        private void Start()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Maps", mapFileName + ".json");
            var json = File.ReadAllText(path);
            var board = MapSerializer.Deserialize(json);
            Render(board);
        }

        public void Render(BoardModel board)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            foreach (var cell in board.Cells.Values)
            {
                var go = Instantiate(cubePrefab, transform);
                var cellView = go.AddComponent<CellView>();
                var pos = new Vector3(cell.Position.X, cell.Position.Y, cell.Position.Z);
                cellView.Init(cell.Id, pos, cell.Sides);
            }
        }
    }
}
