using Cysharp.Threading.Tasks;
using UnityEngine;
using Infrastructure.SceneManagement;
using Zenject;

namespace Infrastructure.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        [Inject] private ISceneManager _sceneManager;

        private async void Start()
        {
            await BootstrapAsync();
        }

        private async UniTask BootstrapAsync()
        {
            Debug.Log("Starting game bootstrap...");
            
            // Initialize application settings
            InitializeApplicationSettings();
            
            // Initialize scene
            await _sceneManager.InitializeSceneAsync();
            
            Debug.Log("Game bootstrap completed!");
        }

        private void InitializeApplicationSettings()
        {
            // Set target frame rate for mobile optimization
            Application.targetFrameRate = 60;
            
            // Ensure proper quality settings
            QualitySettings.vSyncCount = 0;
            
            // Set up input settings
            Input.multiTouchEnabled = false;
        }
    }
}