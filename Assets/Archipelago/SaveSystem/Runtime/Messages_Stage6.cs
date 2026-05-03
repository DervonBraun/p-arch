// ============================================================
//  АРХИПЕЛАГ — Дополнения к Messages.cs для Этапа 6
//  Добавить в namespace Archipelago.Core в Messages.cs
// ============================================================

// Заменить существующий SaveDeniedMessage (он уже есть) и добавить SaveLoadedMessage:

// public readonly struct SaveLoadedMessage
// {
//     public readonly int SlotIndex;
//     public SaveLoadedMessage(int slot) => SlotIndex = slot;
// }

// В SceneInstaller.InstallMessagePipe() добавить:
// Container.BindMessageBroker<SaveLoadedMessage>(options);
