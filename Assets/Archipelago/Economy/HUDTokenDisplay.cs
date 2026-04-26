using System;
using System.Collections;
using Archipelago.Economy;
using MessagePipe;
using TMPro;
using UnityEngine;
using Zenject;

namespace Archipelago.UI
{
    /// <summary>
    /// HUD: три счётчика токенов (красный / зелёный / синий).
    /// Подписывается на TokensChangedMessage и TokensSyncedMessage.
    ///
    /// Анимация: DOTween если доступен, иначе корутина-фоллбэк.
    ///
    /// Иерархия Canvas:
    ///   HUDCanvas (Canvas, Screen Space Overlay)
    ///   └── TokensPanel (HorizontalLayoutGroup)
    ///       ├── RedCounter   (TextMeshProUGUI)
    ///       ├── GreenCounter (TextMeshProUGUI)
    ///       └── BlueCounter  (TextMeshProUGUI)
    /// </summary>
    public sealed class HUDTokenDisplay : MonoBehaviour
    {
        // ── Injected ──────────────────────────────────────────────

        [Inject] private ISubscriber<TokensChangedMessage> _changedSub;
        [Inject] private ISubscriber<TokensSyncedMessage>  _syncedSub;
        [Inject] private TokenService                      _tokenService;

        // ── Inspector ─────────────────────────────────────────────

        [Header("Counter Labels")]
        [SerializeField] private TMP_Text _redLabel;
        [SerializeField] private TMP_Text _greenLabel;
        [SerializeField] private TMP_Text _blueLabel;

        [Header("Pulse Animation")]
        [SerializeField] private float _pulseDuration = 0.25f;
        [SerializeField] private float _pulseScale    = 1.3f;

        // ── Subscriptions ─────────────────────────────────────────

        private IDisposable _subs;

        private void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            _changedSub.Subscribe(OnTokensChanged).AddTo(bag);
            _syncedSub .Subscribe(OnTokensSynced) .AddTo(bag);
            _subs = bag.Build();

            if (_tokenService.IsInitialized)
                RefreshAll(_tokenService.Balance);
        }

        private void OnDestroy() => _subs?.Dispose();

        // ── Handlers ──────────────────────────────────────────────

        private void OnTokensChanged(TokensChangedMessage msg)
        {
            RefreshAll(msg.NewBalance);
            AnimatePulse(msg.ChangedType);
        }

        private void OnTokensSynced(TokensSyncedMessage msg)
        {
            RefreshAll(msg.ServerBalance);
        }

        // ── Display ───────────────────────────────────────────────

        private void RefreshAll(TokenBalance balance)
        {
            SetLabel(_redLabel,   balance.Red);
            SetLabel(_greenLabel, balance.Green);
            SetLabel(_blueLabel,  balance.Blue);
        }

        private static void SetLabel(TMP_Text label, int value)
        {
            if (label != null)
                label.text = value.ToString("N0");
        }

        // ── Pulse animation ───────────────────────────────────────

        private void AnimatePulse(TokenType type)
        {
            TMP_Text target = type switch
            {
                TokenType.Red   => _redLabel,
                TokenType.Green => _greenLabel,
                TokenType.Blue  => _blueLabel,
                _               => null,
            };

            if (target == null) return;

            // Раскомментировать когда DOTween подключён в проекте:
            // target.transform
            //     .DOPunchScale(Vector3.one * (_pulseScale - 1f), _pulseDuration, 1, 0.5f)
            //     .SetUpdate(true); // работает даже при паузе

            // Корутина-фоллбэк (не требует внешних зависимостей)
            StopAllCoroutines();
            StartCoroutine(PulseCoroutine(target.transform));
        }

        private IEnumerator PulseCoroutine(Transform t)
        {
            Vector3 original = Vector3.one;
            Vector3 big      = Vector3.one * _pulseScale;
            float   half     = _pulseDuration * 0.5f;
            float   elapsed  = 0f;

            // Scale up
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                t.localScale = Vector3.Lerp(original, big, elapsed / half);
                yield return null;
            }

            elapsed = 0f;

            // Scale down
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                t.localScale = Vector3.Lerp(big, original, elapsed / half);
                yield return null;
            }

            t.localScale = original;
        }
    }
}