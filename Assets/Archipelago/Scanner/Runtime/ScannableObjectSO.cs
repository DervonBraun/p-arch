using UnityEngine;

namespace Archipelago.Scanner
{
    public enum ScanObjectType { Technical, Strange, Neutral }

    /// <summary>
    /// ScriptableObject — конфиг одного сканируемого объекта.
    /// Создаётся через Assets → Create → Archipelago → ScannableObject.
    /// Один SO на каждый уникальный объект в сцене.
    ///
    /// sensorData инжектируется в system[2] блок промпта дословно —
    /// пиши как показания датчиков, не как описание для игрока.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScannableObject_New",
        menuName  = "Archipelago/Scanner/ScannableObject")]
    public sealed class ScannableObjectSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID. Используется как ключ кэша и в промпте.")]
        public string objectId = "obj_unknown";

        [Tooltip("Человекочитаемое имя для редактора (не попадает в промпт).")]
        public string displayName = "Unknown Object";

        [Header("AI Prompt Data")]
        [Tooltip("Тип объекта — влияет на тон ответа AI.")]
        public ScanObjectType objectType = ScanObjectType.Neutral;

        [Tooltip("Данные датчиков. Попадают в system[2] промпта дословно.\n" +
                 "Пример: 'Material: ferrous alloy. Temperature: 4°C above ambient. " +
                 "Radiation: nominal. Surface integrity: 73%.'")]
        [TextArea(3, 8)]
        public string sensorData = "";

        [Header("Economy")]
        [Tooltip("Стоимость первичного скана в красных токенах.")]
        public int primaryScanCostRed   = 1;

        [Tooltip("Стоимость первичного скана в зелёных токенах.")]
        public int primaryScanCostGreen = 0;

        [Tooltip("Стоимость уточняющего вопроса в красных токенах.")]
        public int followUpCostRed   = 2;

        [Tooltip("Стоимость уточняющего вопроса в зелёных токенах.")]
        public int followUpCostGreen = 1;

        [Tooltip("Награда синими токенами за первичный скан.")]
        public int blueRewardPrimary  = 3;

        [Tooltip("Награда синими токенами за уточняющий вопрос.")]
        public int blueRewardFollowUp = 1;

        [Header("UI")]
        [Tooltip("Спрайт превью — отображается чипом в чате сканера.")]
        public Sprite thumbnailSprite;

        [Header("Lore")]
        [Tooltip("Фрагмент лора, добавляемый в архив после первого скана.")]
        [TextArea(2, 5)]
        public string loreFragment = "";
    }
}