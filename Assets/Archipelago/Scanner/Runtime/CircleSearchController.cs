using System.Collections.Generic;
using Archipelago.Core;
using Archipelago.Player;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Manages the Circle Search overlay mechanic.
    ///
    /// Flow:
    ///   Tab hold  → Open() — overlay appears, camera freezes (InputReader.IsCircleSearchOpen)
    ///   LMB down  → begin drawing path
    ///   LMB drag  → accumulate screen-space points (sampled every MinSampleDistance px)
    ///   LMB up    → FinishCapture(): test each ScannableObject in scene for 60% screen coverage
    ///               → add captured objects to ScanCollection
    ///   Tab press → Close() — overlay disappears, camera unfreezes
    ///
    /// Object detection: project world-space bounding box (8 corners) to screen space,
    /// count how many lie inside the drawn polygon (ray-casting PIP test).
    /// Threshold: CaptureThreshold (default 0.6 = 60%).
    ///
    /// Attach on Player GameObject, inject via ScannerInstaller.
    /// </summary>
    public sealed class CircleSearchController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────

        [SerializeField] private float _captureThreshold = 0.6f;
        [SerializeField] private float _minSampleDistancePx = 8f;

        // ── Injected ──────────────────────────────────────────────

        [Inject] private FirstPersonController    _fpsController;
        [Inject] private InputReader         _inputReader;
        [Inject] private ScanCollection      _collection;
        [Inject] private CircleSearchOverlay _overlay;
        [Inject] private IPublisher<ObjectCapturedMessage> _capturePub;

        // ── State ─────────────────────────────────────────────────

        public bool IsOpen { get; private set; }

        private bool            _drawing;
        private Vector2         _lastSamplePos;
        private readonly List<Vector2> _path = new(256);
        
        private bool _injected;
        
        [Inject]
        private void Construct()
        {
            _injected = true;
            if (isActiveAndEnabled) SubscribeInput();
        }

        // ── Unity Lifecycle ──────────────────────────────────────

        private void OnEnable()
        {
            if (!_injected) return;
            SubscribeInput();
        }

        private void OnDisable()
        {
            if (!_injected) return;
            UnsubscribeInput();
        }

        private void SubscribeInput()
        {
            _inputReader.CircleSearchOpenRequested  += Open;
            _inputReader.CircleSearchCloseRequested += Close;
        }

        private void UnsubscribeInput()
        {
            _inputReader.CircleSearchOpenRequested  -= Open;
            _inputReader.CircleSearchCloseRequested -= Close;
        }

        private void Update()
        {
            if (!IsOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _drawing = true;
                _path.Clear();
                _path.Add(mousePos);
                _lastSamplePos = mousePos;
                _overlay.BeginPath();
            }

            if (_drawing && mouse.leftButton.isPressed)
            {
                if (Vector2.Distance(mousePos, _lastSamplePos) >= _minSampleDistancePx)
                {
                    _path.Add(mousePos);
                    _lastSamplePos = mousePos;
                    _overlay.ExtendPath(mousePos);
                }
            }

            if (_drawing && mouse.leftButton.wasReleasedThisFrame)
            {
                _drawing = false;
                FinishCapture();
            }
        }

        // ── Open / Close ──────────────────────────────────────────

        private void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _inputReader.IsCircleSearchOpen = true;
            _drawing = false;
            _path.Clear();
            _overlay.Show();
            _fpsController.SetCircleSearchCursor(true);   // <-- добавить
        }

        private void Close()
        {
            if (!IsOpen) return;
            _drawing = false;
            IsOpen   = false;
            _inputReader.IsCircleSearchOpen = false;
            _overlay.Hide();
            _fpsController.SetCircleSearchCursor(false);  // <-- добавить
        }

        // ── Capture Detection ─────────────────────────────────────

        private void FinishCapture()
        {
            _overlay.ClearPath();

            if (_path.Count < 6)
            {
                _overlay.ShowStatus("Обведите объект.");
                return;
            }

            var camera    = Camera.main;
            var scannables = FindObjectsByType<ScannableObject>(FindObjectsSortMode.None);
            bool anyNew   = false;

            foreach (var s in scannables)
            {
                if (s.Data == null) continue;
                if (_collection.Contains(s.Data.objectId)) continue;

                float coverage = ComputeScreenCoverage(s, camera, _path);
                if (coverage < _captureThreshold) continue;

                _collection.TryAdd(s.Data, s.Data.thumbnailSprite);
                _capturePub.Publish(new ObjectCapturedMessage(s.Data.objectId, s.Data.displayName, success: true));
                _overlay.ShowStatus($"Добавлено: {s.Data.displayName}");
                anyNew = true;
                break; // capture one per draw; player can draw again for others
            }

            if (!anyNew)
            {
                _capturePub.Publish(new ObjectCapturedMessage("", "", success: false));
                _overlay.ShowStatus("Объект не распознан.");
            }
        }

        // ── Screen Coverage ───────────────────────────────────────

        private static float ComputeScreenCoverage(
            ScannableObject obj,
            Camera          cam,
            List<Vector2>   polygon)
        {
            var rend = obj.GetComponentInChildren<Renderer>();
            if (rend == null) return 0f;

            Bounds b = rend.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            // 8 corners of the AABB
            var corners = new[]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x,  e.y,  e.z),
            };

            int inside = 0;
            foreach (var corner in corners)
            {
                Vector3 screen = cam.WorldToScreenPoint(corner);
                if (screen.z < 0f) continue; // behind camera
                if (PointInPolygon(new Vector2(screen.x, screen.y), polygon))
                    inside++;
            }

            return (float)inside / corners.Length;
        }

        // Ray-casting point-in-polygon test (screen space, Y-up).
        private static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            int n = poly.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = poly[i];
                Vector2 pj = poly[j];

                if (((pi.y > p.y) != (pj.y > p.y)) &&
                    (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
