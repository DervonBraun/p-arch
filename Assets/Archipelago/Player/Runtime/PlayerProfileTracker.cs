using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.PlayerProfile
{
    /// <summary>
    /// Подписывается на игровые события и обновляет BehaviorMetrics.
    /// Не содержит бизнес-логики — только сбор данных.
    ///
    /// Подписки:
    ///   ScanCompletedMessage  → TotalScans, StrangeScans, FollowUpQueries
    ///   GameTickMessage       → TotalGameHours, TimeInRoom
    ///   RoomChangedMessage    → текущая комната
    /// </summary>
    public sealed class PlayerProfileTracker : IInitializable, IDisposable
    {
        // ── State ─────────────────────────────────────────────────

        private string _currentRoom = "hub";
        private IDisposable _subs;

        // ── Dependencies ──────────────────────────────────────────

        private readonly PlayerProfileData                   _profile;
        private readonly FlagService                         _flagService;
        private readonly ISubscriber<ScanCompletedMessage>   _scanSub;
        private readonly ISubscriber<GameTickMessage>         _tickSub;
        private readonly ISubscriber<RoomChangedMessage>      _roomSub;

        [Inject]
        public PlayerProfileTracker(
            PlayerProfileData                  profile,
            FlagService                        flagService,
            ISubscriber<ScanCompletedMessage>  scanSub,
            ISubscriber<GameTickMessage>        tickSub,
            ISubscriber<RoomChangedMessage>     roomSub)
        {
            _profile     = profile;
            _flagService = flagService;
            _scanSub     = scanSub;
            _tickSub     = tickSub;
            _roomSub     = roomSub;
        }

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            _scanSub.Subscribe(OnScanCompleted).AddTo(bag);
            _tickSub.Subscribe(OnGameTick)     .AddTo(bag);
            _roomSub.Subscribe(OnRoomChanged)  .AddTo(bag);
            _subs = bag.Build();
        }

        public void Dispose() => _subs?.Dispose();

        // ── Handlers ──────────────────────────────────────────────

        private void OnScanCompleted(ScanCompletedMessage msg)
        {
            ref var m = ref _profile.Metrics;
            m.TotalScans++;

            // TODO: когда ScannableObjectSO будет доступен в сообщении —
            // проверять objectType == strange для StrangeScans
            // Пока инкрементируем через отдельный метод TrackStrangeScan()

            _profile.UpdatedAt = DateTime.UtcNow;
            _flagService.RefreshBehaviorFlags();
        }

        private void OnGameTick(GameTickMessage msg)
        {
            ref var m = ref _profile.Metrics;
            m.TotalGameHours += msg.DeltaGameTime / 3600f;

            // Время в текущей комнате
            switch (_currentRoom)
            {
                case "hub":         m.TimeInHub         += msg.DeltaGameTime; break;
                case "garden":      m.TimeInGarden      += msg.DeltaGameTime; break;
                case "gallery":     m.TimeInGallery     += msg.DeltaGameTime; break;
                case "residential": m.TimeInResidential += msg.DeltaGameTime; break;
                case "generator":   m.TimeInGenerator   += msg.DeltaGameTime; break;
                case "reservoir":   m.TimeInReservoir   += msg.DeltaGameTime; break;
                case "street":      m.TimeInStreet      += msg.DeltaGameTime; break;
            }
        }

        private void OnRoomChanged(RoomChangedMessage msg)
        {
            _currentRoom = msg.RoomId;
        }

        // ── Public helpers (вызываются ScannerService) ────────────

        /// <summary>Отмечает что последнее сканирование было аномального объекта.</summary>
        public void TrackStrangeScan()    => _profile.Metrics.StrangeScans++;

        /// <summary>Отмечает уточняющий вопрос (не первый в сессии).</summary>
        public void TrackFollowUpQuery()  => _profile.Metrics.FollowUpQueries++;

        /// <summary>Строит человекочитаемую строку профиля для промпта.</summary>
        public string BuildProfileString()
        {
            var m = _profile.Metrics;
            var f = _profile.Flags;

            string anomalyLevel = m.AnomalyInterest switch
            {
                > 0.7f => "высокий",
                > 0.4f => "средний",
                _      => "низкий",
            };

            string queryStyle = m.QueryDetailRatio switch
            {
                > 0.6f => "детальный",
                > 0.2f => "стандартный",
                _      => "минималистичный",
            };

            return $"Время на базе: {m.TotalGameHours:F1}ч. " +
                   $"Интерес к аномалиям: {anomalyLevel}. " +
                   $"Стиль запросов: {queryStyle}. " +
                   $"Чаще всего: {m.DominantRoom}. " +
                   $"Флаги: {_flagService.BuildFlagInjection()}";
        }
    }
}
