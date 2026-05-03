// Минимальный diff для FirstPersonController.cs
// Добавить одно поле и одну строку в HandleLook()
// Всё остальное не трогать.

// ── 1. Добавить поле (после _sprinting): ──────────────────────────────────

// private PlayerFeelController _feelController;

// ── 2. Добавить в Awake() после строки с _cc: ─────────────────────────────

// _feelController = GetComponent<PlayerFeelController>();

// ── 3. Добавить в конец HandleLook(): ────────────────────────────────────

// _feelController?.NotifyLookDelta(_lookInput);

// Итоговый HandleLook() выглядит так:
//
// private void HandleLook()
// {
//     transform.Rotate(Vector3.up, _lookInput.x * _mouseSensitivity);
//
//     _pitch -= _lookInput.y * _mouseSensitivity;
//     _pitch  = Mathf.Clamp(_pitch, -_pitchClamp, _pitchClamp);
//
//     if (_cameraRoot != null)
//         _cameraRoot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
//
//     _feelController?.NotifyLookDelta(_lookInput);  // <-- добавить
// }
