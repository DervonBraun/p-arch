namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Контракт для систем участвующих в сохранении.
    /// SaveService обходит все ISaveable через ServiceLocator.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Заполнить свой раздел SaveData перед записью на диск.</summary>
        void OnSave(SaveData data);

        /// <summary>Восстановить состояние из SaveData после чтения с диска.</summary>
        void OnLoad(SaveData data);

        /// <summary>Сбросить состояние к начальному (новая игра).</summary>
        void OnReset();
    }
}
