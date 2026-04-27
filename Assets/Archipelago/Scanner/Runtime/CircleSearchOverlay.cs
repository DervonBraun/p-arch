using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Canvas overlay shown while CircleSearch is active.
    ///
    /// Canvas hierarchy (set up in Inspector):
    ///   CircleSearchCanvas  (Canvas, Screen Space - Overlay, Sort Order 50)
    ///   └── Root  (CanvasGroup, full-screen)
    ///       ├── DimImage      (Image, black ~20% alpha)
    ///       ├── DrawingArea   (CircleSearchDrawer, full-screen, anchored to Root)
    ///       └── StatusPanel
    ///           └── StatusText (TMP_Text)
    ///
    /// Assign all [SerializeField] references in the Inspector.
    /// </summary>
    public sealed class CircleSearchOverlay : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup _root;

        [Header("Path Drawing")]
        [SerializeField] private CircleSearchDrawer _drawer;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private float    _statusDisplaySeconds = 2f;

        private Coroutine _statusCoroutine;

        // ── Public API ────────────────────────────────────────────

        public void Show()
        {
            _root.gameObject.SetActive(true);
            ClearPath();
            SetStatus("");
        }

        public void Hide()
        {
            ClearPath();
            _root.gameObject.SetActive(false);
        }

        public void BeginPath()
        {
            _drawer.Clear();
        }

        public void ExtendPath(Vector2 screenPoint)
        {
            _drawer.AddPoint(screenPoint);
        }

        public void ClearPath()
        {
            _drawer.Clear();
        }

        public void ShowStatus(string message)
        {
            if (_statusCoroutine != null) StopCoroutine(_statusCoroutine);
            SetStatus(message);
            _statusCoroutine = StartCoroutine(ClearStatusAfterDelay());
        }

        // ── Helpers ───────────────────────────────────────────────

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }

        private IEnumerator ClearStatusAfterDelay()
        {
            yield return new WaitForSeconds(_statusDisplaySeconds);
            SetStatus("");
            _statusCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Canvas-based line renderer using Unity's Graphic API.
    // Works in any render pipeline (HDRP/URP/Built-in) because
    // Canvas rendering is pipeline-independent.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws the freehand path as a series of quad line segments on a Canvas.
    /// Attach to a full-screen RectTransform inside the CircleSearch canvas.
    ///
    /// Line width and colour are adjustable in the Inspector via the standard
    /// Graphic color and the lineWidth field.
    /// </summary>
    
}
