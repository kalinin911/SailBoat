using UnityEngine;
using Zenject;

namespace Infrastructure.DI
{
    public class SceneContextSetup : MonoBehaviour
    {
        [SerializeField] private GameInstaller _gameInstaller;

        private void Awake()
        {
            var sceneContext = FindObjectOfType<SceneContext>();
            if (sceneContext == null)
            {
                var contextGO = new GameObject("SceneContext");
                sceneContext = contextGO.AddComponent<SceneContext>();
                sceneContext.Installers = new MonoInstaller[] { _gameInstaller };
            }
        }
    }
}