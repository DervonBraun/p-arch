using Archipelago.Core;
using Archipelago.Economy;
using Archipelago.Effects;
using Archipelago.Player;
using Archipelago.PlayerProfile;
using Archipelago.Session;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Archipelago.SaveSystem
{
    // ══════════════════════════════════════════════════════════════
    //  SessionSaveable
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Сохраняет: GameClock время, индекс дня, позицию игрока.
    /// </summary>
    public sealed class SessionSaveable : ISaveable
    {
        private readonly GameClock              _clock;
        private readonly FirstPersonController  _fpc;

        [Inject]
        public SessionSaveable(GameClock clock, FirstPersonController fpc)
        {
            _clock = clock;
            _fpc   = fpc;
        }

        public void OnSave(SaveData data)
        {
            data.Session.TotalGameTime = _clock.TotalGameTime;
            data.Session.DayIndex      = _clock.DayIndex;

            var pos = _fpc.transform.position;
            data.Session.PlayerX = pos.x;
            data.Session.PlayerY = pos.y;
            data.Session.PlayerZ = pos.z;
        }

        public void OnLoad(SaveData data)
        {
            _clock.Restore(data.Session.TotalGameTime, data.Session.DayIndex);

            var pos = new Vector3(
                data.Session.PlayerX,
                data.Session.PlayerY,
                data.Session.PlayerZ);
            _fpc.Teleport(pos);
        }

        public void OnReset()
        {
            _clock.Restore(0f, 0);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  EconomySaveable
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Сохраняет локальный кэш баланса токенов.
    /// При загрузке синхронизирует с сервером.
    /// </summary>
    public sealed class EconomySaveable : ISaveable
    {
        private readonly TokenWallet  _wallet;
        private readonly TokenService _tokenService;

        [Inject]
        public EconomySaveable(TokenWallet wallet, TokenService tokenService)
        {
            _wallet       = wallet;
            _tokenService = tokenService;
        }

        public void OnSave(SaveData data)
        {
            data.Economy.Red   = _wallet.Balance.Red;
            data.Economy.Green = _wallet.Balance.Green;
            data.Economy.Blue  = _wallet.Balance.Blue;
        }

        public void OnLoad(SaveData data)
        {
            _wallet.SetBalance(new Archipelago.Core.TokenBalance(
                data.Economy.Red,
                data.Economy.Green,
                data.Economy.Blue));

            // Принудительная синхронизация с сервером после загрузки
            _tokenService.ForceSyncAsync().Forget();
        }

        public void OnReset()
        {
            _wallet.SetBalance(Archipelago.Core.TokenBalance.Zero);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  EffectsSaveable
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Сохраняет активные эффекты (стаки + оставшееся время).
    /// При загрузке восстанавливает через EffectService.
    /// </summary>
    public sealed class EffectsSaveable : ISaveable
    {
        private readonly EffectService               _effectService;
        private readonly EffectDefinitionLibrary     _library;

        [Inject]
        public EffectsSaveable(EffectService effectService, EffectDefinitionLibrary library)
        {
            _effectService = effectService;
            _library       = library;
        }

        public void OnSave(SaveData data)
        {
            data.Effects.ActiveEffects.Clear();

            foreach (var id in _effectService.GetActiveEffectIds())
            {
                var effect = _effectService.Get(id);
                if (effect == null) continue;

                data.Effects.ActiveEffects.Add(new ActiveEffectSaveData
                {
                    EffectId      = id,
                    CurrentStacks = effect.CurrentStacks,
                    RemainingTime = effect.RemainingTime,
                });
            }
        }

        public void OnLoad(SaveData data)
        {
            foreach (var saved in data.Effects.ActiveEffects)
            {
                var def = _library.Get(saved.EffectId);
                if (def == null)
                {
                    Debug.LogWarning($"[EffectsSaveable] Unknown effectId '{saved.EffectId}' — skipping.");
                    continue;
                }
                _effectService.Restore(def, saved.CurrentStacks, saved.RemainingTime);
            }
        }

        public void OnReset() => _effectService.ClearAll();
    }

    // ══════════════════════════════════════════════════════════════
    //  ProfileSaveable
    // ══════════════════════════════════════════════════════════════

    /// <summary>Сохраняет PlayerProfileData (метрики + флаги).</summary>
    public sealed class ProfileSaveable : ISaveable
    {
        private readonly PlayerProfileData _profile;

        [Inject]
        public ProfileSaveable(PlayerProfileData profile) => _profile = profile;

        public void OnSave(SaveData data)
        {
            data.Profile = _profile;
        }

        public void OnLoad(SaveData data)
        {
            if (data.Profile == null) return;
            _profile.Metrics   = data.Profile.Metrics;
            _profile.Flags     = data.Profile.Flags;
            _profile.UpdatedAt = data.Profile.UpdatedAt;
        }

        public void OnReset()
        {
            _profile.Metrics = new Archipelago.PlayerProfile.BehaviorMetrics();
            _profile.Flags   = new Archipelago.PlayerProfile.FlagProfile();
        }
    }
}
