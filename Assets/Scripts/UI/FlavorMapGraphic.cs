using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Galatea.UI
{
    // 5-axis pentagon radar chart (sweet · acidic · bitter · salty · spicy).
    // Values are normalized to [0,1]; VisorHUD.ProfileToDict handles the 0–10
    // scaling. Neutralizer is intentionally absent — it's a meta-attribute that
    // dampens others rather than a flavor the player directly tastes.
    [RequireComponent(typeof(CanvasRenderer))]
    public class FlavorMapGraphic : MaskableGraphic
    {
        private static readonly string[] AxisNames =
            { "sweet", "acidic", "bitter", "salty", "spicy" };

        private const int AxisCount = 5;

        [Header("Shape")]
        [SerializeField, Range(1, 8)] private int   ringCount     = 4;
        [SerializeField]              private float  lineThickness = 1.5f;
        [SerializeField]              private float  dotRadius     = 4f;

        [Header("Colors")]
        [SerializeField] private Color gridColor    = new Color32(0x2A, 0x3A, 0x4A, 0xFF);
        [SerializeField] private Color spokeColor   = new Color32(0x2A, 0x3A, 0x4A, 0xFF);
        [SerializeField] private Color fillColor    = new Color32(0x7E, 0xFF, 0xD4, 0x4D);
        [SerializeField] private Color outlineColor = new Color32(0x7E, 0xFF, 0xD4, 0xFF);
        [SerializeField] private Color dotColor     = new Color32(0x7E, 0xFF, 0xD4, 0xFF);

        [Header("Axis Labels")]
        [SerializeField] private bool autoCreateLabels = true;
        [SerializeField] private float labelPadding = 10f;
        [SerializeField] private float labelFontSize = 10f;
        [SerializeField] private Color labelColor = new Color32(0x7E, 0xFF, 0xD4, 0xFF);

        private readonly float[] _values = new float[AxisCount];
        private readonly TMP_Text[] _labels = new TMP_Text[AxisCount];

        // Pre: v keys are lowercase ("sweet"/"acidic"/…) and values are in [0,1].
        // Post: missing keys default to 0; the chart re-renders next frame.
        public void SetValues(Dictionary<string, float> v)
        {
            for (int i = 0; i < AxisCount; i++)
            {
                _values[i] = (v != null && v.TryGetValue(AxisNames[i], out float val))
                    ? Mathf.Clamp01(val)
                    : 0f;
            }
            SetVerticesDirty();
        }

        [ContextMenu("Test: Fill to 70%")]
        private void TestFill()
        {
            for (int i = 0; i < AxisCount; i++) _values[i] = 0.7f;
            SetVerticesDirty();
        }

        [ContextMenu("Test: Clear")]
        private void TestClear()
        {
            for (int i = 0; i < AxisCount; i++) _values[i] = 0f;
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        protected override void Start()
        {
            base.Start();
            if (autoCreateLabels) EnsureLabels();
        }

        private void EnsureLabels()
        {
            for (int i = 0; i < AxisCount; i++)
            {
                string labelName = $"AxisLabel_{AxisNames[i]}";
                var existing = transform.Find(labelName);
                if (existing != null)
                {
                    _labels[i] = existing.GetComponent<TMP_Text>();
                    continue;
                }

                var go = new GameObject(labelName, typeof(RectTransform));
                go.transform.SetParent(transform, worldPositionStays: false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(60, 16);

                float angle = Mathf.PI * 0.5f - i * (2f * Mathf.PI / AxisCount);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Rect rect = rectTransform.rect;
                float radius = Mathf.Min(rect.width, rect.height) * 0.5f - dotRadius - 2f;
                rt.anchoredPosition = dir * (radius + labelPadding);

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = AxisNames[i];
                tmp.fontSize = labelFontSize;
                tmp.color = labelColor;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;

                _labels[i] = tmp;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect    rect   = rectTransform.rect;
            Vector2 center = rect.center;
            float   radius = Mathf.Min(rect.width, rect.height) * 0.5f - dotRadius - 2f;
            if (radius <= 0f) return;

            // Pentagon vertex directions: start at top (90°), step clockwise.
            var dirs = new Vector2[AxisCount];
            for (int i = 0; i < AxisCount; i++)
            {
                float angle = Mathf.PI * 0.5f - i * (2f * Mathf.PI / AxisCount);
                dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            for (int r = 1; r <= ringCount; r++)
            {
                float t = (float)r / ringCount;
                for (int i = 0; i < AxisCount; i++)
                {
                    Vector2 a = center + dirs[i]               * radius * t;
                    Vector2 b = center + dirs[(i+1)%AxisCount] * radius * t;
                    AddLine(vh, a, b, lineThickness, gridColor);
                }
            }

            for (int i = 0; i < AxisCount; i++)
                AddLine(vh, center, center + dirs[i] * radius, lineThickness, spokeColor);

            var verts = new Vector2[AxisCount];
            for (int i = 0; i < AxisCount; i++)
                verts[i] = center + dirs[i] * radius * _values[i];

            AddFilledPolygon(vh, center, verts, fillColor);

            for (int i = 0; i < AxisCount; i++)
                AddLine(vh, verts[i], verts[(i+1)%AxisCount], lineThickness, outlineColor);

            for (int i = 0; i < AxisCount; i++)
            {
                if (_values[i] > 0f) AddDot(vh, verts[i], dotRadius, dotColor);
            }
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            d.Normalize();
            Vector2 perp = new Vector2(-d.y, d.x) * (thickness * 0.5f);

            int i0 = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = a - perp; vh.AddVert(v);
            v.position = a + perp; vh.AddVert(v);
            v.position = b + perp; vh.AddVert(v);
            v.position = b - perp; vh.AddVert(v);
            vh.AddTriangle(i0, i0+1, i0+2);
            vh.AddTriangle(i0, i0+2, i0+3);
        }

        private static void AddFilledPolygon(VertexHelper vh, Vector2 fanCenter, Vector2[] verts, Color c)
        {
            int startIdx = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = fanCenter;
            vh.AddVert(v);
            for (int i = 0; i < verts.Length; i++)
            {
                v.position = verts[i];
                vh.AddVert(v);
            }
            for (int i = 0; i < verts.Length; i++)
                vh.AddTriangle(startIdx, startIdx+1+i, startIdx+1+(i+1)%verts.Length);
        }

        private static void AddDot(VertexHelper vh, Vector2 c, float r, Color col)
        {
            const int segments = 12;
            int startIdx = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = col;
            v.position = c;
            vh.AddVert(v);
            for (int i = 0; i < segments; i++)
            {
                float ang = i * (2f * Mathf.PI / segments);
                v.position = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                vh.AddVert(v);
            }
            for (int i = 0; i < segments; i++)
                vh.AddTriangle(startIdx, startIdx+1+i, startIdx+1+(i+1)%segments);
        }
    }
}
