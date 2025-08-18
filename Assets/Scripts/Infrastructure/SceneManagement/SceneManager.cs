using Cysharp.Threading.Tasks;
using UnityEngine;
using Gameplay;
using Zenject;

namespace Infrastructure.SceneManagement
{
    public class SceneManager : ISceneManager
    {
        [Inject] private IGameController _gameController;

        public async UniTask InitializeSceneAsync()
        {
            Debug.Log("Initializing scene...");
            
            // Wait for dependencies to be ready
            await UniTask.NextFrame();
            
            // Initialize game
            await _gameController.InitializeGameAsync();
        }

        public void RestartScene()
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene.name);
        }

        public void LoadScene(string sceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}