// ============================================================
//  Дополнения к EffectService.cs для поддержки SaveSystem
//  Добавить эти методы в существующий класс EffectService
// ============================================================

// 1. GetActiveEffectIds() — для OnSave:
//
// public IEnumerable<string> GetActiveEffectIds() => _active.Keys;

// 2. Restore() — для OnLoad (восстанавливает эффект без вызова OnApply):
//
// public void Restore(EffectDefinitionSO definition, int stacks, float remainingTime)
// {
//     var effect = new ActiveEffect(definition);
//     // Добавляем стаки молча (без хендлеров — состояние уже было)
//     for (int i = 1; i < stacks; i++)
//         effect.AddStack();
//     // Перезаписываем таймер напрямую через reflection или через новый метод
//     // Проще всего добавить internal метод в ActiveEffect:
//     effect.SetRemainingTime(remainingTime);
//     _active[definition.effectId] = effect;
//     // Публикуем чтобы UI обновился
//     _appliedPub.Publish(new EffectAppliedMessage(definition.effectId, stacks, remainingTime));
// }

// 3. ClearAll() — для OnReset:
//
// public void ClearAll()
// {
//     foreach (var (id, effect) in _active)
//     {
//         if (_handlers.TryGetValue(id, out var h))
//             h.OnExpire(effect);
//         _expiredPub.Publish(new EffectExpiredMessage(id));
//     }
//     _active.Clear();
// }

// ── Дополнение к ActiveEffect.cs ─────────────────────────────
// Добавить метод SetRemainingTime для восстановления из сохранения:
//
// public void SetRemainingTime(float time)
// {
//     RemainingTime = Mathf.Clamp(time, 0f, Definition.maxDuration);
// }

// ── Дополнение к GameClock.cs ─────────────────────────────────
// Добавить метод Restore для загрузки сохранения:
//
// public void Restore(float totalGameTime, int dayIndex)
// {
//     TotalGameTime = totalGameTime;
//     DayIndex      = dayIndex;
// }

// ── Дополнение к FirstPersonController.cs ────────────────────
// Добавить метод Teleport для загрузки позиции:
//
// public void Teleport(Vector3 position)
// {
//     var cc = GetComponent<CharacterController>();
//     if (cc != null) cc.enabled = false;
//     transform.position = position;
//     if (cc != null) cc.enabled = true;
// }
