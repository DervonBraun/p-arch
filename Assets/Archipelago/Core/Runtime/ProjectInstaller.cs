using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// ProjectContext installer — биндинги которые должны жить МЕЖДУ сценами.
    /// Для АРХИПЕЛАГ (одна сцена) здесь пока ничего нет.
    /// Оставлен как точка расширения: если появится мультисцена,
    /// сюда переедут сервисы с DontDestroyOnLoad семантикой.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProjectInstaller",
        menuName  = "Archipelago/Installers/ProjectInstaller")]
    public sealed class ProjectInstaller : ScriptableObjectInstaller<ProjectInstaller>
    {
        public override void InstallBindings()
        {
            // Intentionally empty for single-scene project.
        }
    }
}