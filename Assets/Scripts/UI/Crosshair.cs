using UnityEngine;
using UnityEngine.UI;
using Galatea.Player;

namespace Galatea.UI
{
    // Three-element procedural crosshair (dot + horizontal bar + vertical bar).
    // Tints to AccentWarm when the player is aiming at something interactable.
    // Auto-creates the Image children when the Inspector slots are left empty.
    [RequireComponent(typeof(RectTransform))]
    public class Crosshair : MonoBehaviour
    {
        private static readonly Color BarIdle = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color DotIdle = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color Aimed   = new Color(1f, 0.95f, 0.8f, 1f);

        [Header("Systems")]
        [SerializeField] private InteractionSystem interactionSystem;

        [Header("Elements (leave empty to auto-create)")]
        [SerializeField] private Image dotImage;
        [SerializeField] private Image hBarImage;
        [SerializeField] private Image vBarImage;

        [Header("Auto-create sizes")]
        [SerializeField] private float dotSize  = 6f;
        [SerializeField] private float barWidth  = 10f;
        [SerializeField] private float barHeight = 2f;

        private void Start()
        {
            if (dotImage  == null) dotImage  = CreateElement("Dot",  dotSize,  dotSize);
            if (hBarImage == null) hBarImage = CreateElement("HBar", barWidth, barHeight);
            if (vBarImage == null) vBarImage = CreateElement("VBar", barHeight, barWidth);

            ApplyColors(idle: true);
        }

        private void Update()
        {
            if (interactionSystem == null) return;
            ApplyColors(idle: !interactionSystem.IsLookingAtInteractable);
        }

        private void ApplyColors(bool idle)
        {
            if (dotImage  != null) dotImage.color  = idle ? DotIdle : Aimed;
            if (hBarImage != null) hBarImage.color = idle ? BarIdle : Aimed;
            if (vBarImage != null) vBarImage.color = idle ? BarIdle : Aimed;
        }

        private Image CreateElement(string elementName, float w, float h)
        {
            var go = new GameObject(elementName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, worldPositionStays: false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = Color.white;
            return img;
        }
    }
}
