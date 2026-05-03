using System;

namespace Archipelago.PlayerProfile
{
    /// <summary>
    /// Сырые счётчики поведения игрока. Value type — безопасно копировать.
    /// Обновляется PlayerProfileTracker при каждом релевантном событии.
    /// Сбрасывается частично при DayChangedMessage (dailyXxx поля).
    /// </summary>
    [Serializable]
    public struct BehaviorMetrics
    {
        // ── Session totals ────────────────────────────────────────

        public float TotalGameHours;       // из GameClock.TotalHours
        public int   TotalScans;           // все запросы к сканеру
        public int   StrangeScans;         // objectType == strange
        public int   TechnicalScans;       // уточняющие технические вопросы
        public int   FollowUpQueries;      // уточняющие вопросы (не первый запрос)

        // ── Room tracking ─────────────────────────────────────────

        public float TimeInHub;
        public float TimeInGarden;
        public float TimeInGallery;
        public float TimeInResidential;
        public float TimeInGenerator;
        public float TimeInReservoir;
        public float TimeInStreet;

        // ── Derived (пересчитываются перед каждым запросом) ──────

        /// <summary>Доля сканирований аномальных объектов [0,1].</summary>
        public readonly float AnomalyInterest =>
            TotalScans > 0 ? (float)StrangeScans / TotalScans : 0f;

        /// <summary>Доля уточняющих вопросов [0,1].</summary>
        public readonly float QueryDetailRatio =>
            TotalScans > 0 ? (float)FollowUpQueries / TotalScans : 0f;

        /// <summary>Зона где игрок провёл больше всего времени.</summary>
        public readonly string DominantRoom
        {
            get
            {
                float max = 0f;
                string room = "hub";
                Check("hub",         TimeInHub,         ref max, ref room);
                Check("garden",      TimeInGarden,      ref max, ref room);
                Check("gallery",     TimeInGallery,     ref max, ref room);
                Check("residential", TimeInResidential, ref max, ref room);
                Check("generator",   TimeInGenerator,   ref max, ref room);
                Check("reservoir",   TimeInReservoir,   ref max, ref room);
                Check("street",      TimeInStreet,      ref max, ref room);
                return room;
            }
        }

        private static void Check(string name, float time, ref float max, ref string room)
        {
            if (time > max) { max = time; room = name; }
        }
    }
}
