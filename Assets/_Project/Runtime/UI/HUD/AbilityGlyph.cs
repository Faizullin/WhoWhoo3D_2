using UnityEngine;
using UnityEngine.UI;
using WhoWhoo3D.Domain.Match;

namespace WhoWhoo3D.UI.HUD
{
    /// <summary>Resolution-independent HUD symbols drawn in a 24-unit square.</summary>
    internal sealed class AbilityGlyph : MaskableGraphic
    {
        private MatchAction _action;
        public static AbilityGlyph Create(Transform parent, MatchAction action, Color tint)
        {
            var glyph = HudControls.Node(parent, "Symbol").gameObject.AddComponent<AbilityGlyph>();
            glyph._action = action;
            glyph.color = tint;
            glyph.raycastTarget = false;
            return glyph;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float unit = Mathf.Min(rect.width, rect.height) / 24;
            void Stroke(Vector2 a, Vector2 b, float weight = 1.6f)
            {
                var n = Vector2.Perpendicular((b - a).normalized) * (weight * .5f);
                int first = mesh.currentVertCount;
                void Vertex(Vector2 p) => mesh.AddVert(rect.center + p * unit, color, Vector2.zero);
                Vertex(a - n); Vertex(a + n); Vertex(b + n); Vertex(b - n);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }
            void Ring(float radius, float start, float end)
            {
                Vector2 Point(float angle) => radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                const int segments = 24;
                for (int i = 0; i < segments; i++)
                    Stroke(Point(Mathf.Lerp(start, end, i / (float)segments)), Point(Mathf.Lerp(start, end, (i + 1f) / segments)));
            }
            switch (_action)
            {
                case MatchAction.Relay:
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Stroke(new Vector2(-8, 3) * sign, new Vector2(7, 3) * sign);
                        Stroke(new Vector2(3, 7) * sign, new Vector2(7.6f, 2.7f) * sign);
                        Stroke(new Vector2(3, -1) * sign, new Vector2(7.6f, 3.3f) * sign);
                    }
                    break;
                case MatchAction.Freeze:
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = (90 + 60 * i) * Mathf.Deg2Rad;
                        var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var side = Vector2.Perpendicular(axis) * 2.6f;
                        Stroke(Vector2.zero, axis * 10);
                        Stroke(axis * 4.8f, axis * 7 + side);
                        Stroke(axis * 4.8f, axis * 7 - side);
                    }
                    break;
                case MatchAction.Beacon:
                    Ring(2.1f, 0, 2 * Mathf.PI);
                    for (int i = 0; i < 2; i++)
                    {
                        float offset = i * Mathf.PI;
                        Ring(6, -.92f + offset, .92f + offset);
                        Ring(10, -.92f + offset, .92f + offset);
                    }
                    break;
                default:
                    Ring(9.4f, 0, 2 * Mathf.PI);
                    Stroke(new Vector2(0, -5), new Vector2(0, 1), 2.2f);
                    Stroke(new Vector2(0, 3.4f), new Vector2(0, 5.4f), 2.2f);
                    break;
            }
        }
    }
}
