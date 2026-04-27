using System.Collections.Generic;
using Archipelago.Core;

namespace Archipelago.Economy
{
    /// <summary>
    /// Вычисляет стоимость запроса к сканеру.
    /// Чистая статическая функция — нет зависимостей, полностью покрывается unit-тестами.
    /// 
    /// Формула (из архдока v1.1):
    ///   Red  = (прикреплённых объектов × costPerObject) + (символов без пробелов × costPerChar)
    ///   Green = isFirstRequest ? firstRequestGreenCost : 0
    /// </summary>
    public static class ScanCostCalculator
    {
        public readonly struct ScanCost
        {
            public readonly int Red;
            public readonly int Green;

            public ScanCost(int red, int green)
            {
                Red   = red;
                Green = green;
            }

            public bool IsAffordable(TokenBalance balance)
                => balance.Red >= Red && balance.Green >= Green;

            public override string ToString() => $"{Red}🔴 + {Green}🟢";
        }

        /// <summary>
        /// Рассчитывает стоимость запроса.
        /// </summary>
        /// <param name="attachedObjectCount">Количество прикреплённых объектов.</param>
        /// <param name="questionText">Текст вопроса игрока.</param>
        /// <param name="isFirstRequest">true если это первый запрос в сессии.</param>
        /// <param name="config">Конфиг экономики.</param>
        public static ScanCost Calculate(
            int           attachedObjectCount,
            string        questionText,
            bool          isFirstRequest,
            EconomyConfig config)
        {
            int charCount = CountNonWhitespace(questionText);

            int red   = (attachedObjectCount * config.ScanAttachedObjectRedCost)
                      + (charCount           * config.ScanCharRedCost);

            int green = isFirstRequest ? config.ScanFirstRequestGreenCost : 0;

            return new ScanCost(red, green);
        }

        /// <summary>
        /// Перегрузка с явными параметрами (удобна для тестов).
        /// </summary>
        public static ScanCost Calculate(
            int    attachedObjectCount,
            string questionText,
            bool   isFirstRequest,
            int    costPerObject      = 500,
            int    costPerChar        = 1,
            int    firstRequestGreen  = 1000)
        {
            int charCount = CountNonWhitespace(questionText);

            int red   = (attachedObjectCount * costPerObject)
                      + (charCount           * costPerChar);

            int green = isFirstRequest ? firstRequestGreen : 0;

            return new ScanCost(red, green);
        }

        /// <summary>
        /// Считает символы, исключая пробел, Tab и перенос строки.
        /// </summary>
        public static int CountNonWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 0;
            // PERF: span iteration, нет аллокаций
            foreach (char c in text)
            {
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Проверяет нужен ли Reasoning Mode (визуальная задержка).
        /// </summary>
        public static bool IsReasoningMode(string questionText, EconomyConfig config)
            => CountNonWhitespace(questionText) >= config.ReasoningModeThreshold;
    }
}