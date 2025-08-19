using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.UI
{
    public class LoadingScreenManager : MonoBehaviour
    {
        [Header("Loading Settings")]
        [SerializeField] private float _minimumLoadingTime = 2f;
        [SerializeField] private bool _showDetailedStatus = true;

        private float _targetProgress = 0f;
        private float _currentProgress = 0f;
        private bool _isLoading = false;
        private float _loadingStartTime;
        private string _currentStatus = "";

        public void ShowLoadingScreen(string initialStatus = "Loading...")
        {
            _isLoading = true;
            _loadingStartTime = Time.time;
            _currentProgress = 0f;
            _targetProgress = 0f;
            _currentStatus = initialStatus;

            if (_showDetailedStatus)
            {
                Debug.Log($"[LOADING] {initialStatus}");
            }
        }

        public void HideLoadingScreen()
        {
            _isLoading = false;
            
            if (_showDetailedStatus)
            {
                Debug.Log("[LOADING] Loading screen hidden");
            }
        }

        public async UniTask ShowLoadingScreenAsync(string initialStatus = "Loading...")
        {
            ShowLoadingScreen(initialStatus);
            
            // Wait for minimum loading time to ensure smooth experience
            var elapsedTime = Time.time - _loadingStartTime;
            var remainingTime = _minimumLoadingTime - elapsedTime;
            
            if (remainingTime > 0)
            {
                await UniTask.Delay((int)(remainingTime * 1000));
            }
        }

        public void UpdateProgress(float progress, string status = null)
        {
            _targetProgress = Mathf.Clamp01(progress);
            
            if (!string.IsNullOrEmpty(status))
            {
                _currentStatus = status;
            }

            if (_showDetailedStatus)
            {
                Debug.Log($"[LOADING] {_currentStatus} - {progress * 100:F1}%");
            }

            // Check if loading is complete
            if (_targetProgress >= 1f)
            {
                OnLoadingComplete();
            }
        }

        private void OnLoadingComplete()
        {
            if (_showDetailedStatus)
            {
                Debug.Log("[LOADING] Loading Complete!");
            }
        }

        // Custom loading phases
        public async UniTask LoadWithPhases(LoadingPhase[] phases)
        {
            ShowLoadingScreen("Starting...");

            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                var phaseProgress = (float)i / phases.Length;
                
                UpdateProgress(phaseProgress, phase.Description);
                
                try
                {
                    await phase.LoadingTask();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Loading phase '{phase.Description}' failed: {ex.Message}");
                    UpdateProgress(1f, $"Failed: {phase.Description}");
                    await UniTask.Delay(2000);
                    HideLoadingScreen();
                    return;
                }
            }

            UpdateProgress(1f, "Complete!");
            await ShowLoadingScreenAsync();
            HideLoadingScreen();
        }

        [System.Serializable]
        public class LoadingPhase
        {
            public string Description;
            public System.Func<UniTask> LoadingTask;

            public LoadingPhase(string description, System.Func<UniTask> loadingTask)
            {
                Description = description;
                LoadingTask = loadingTask;
            }
        }

        // Public API for external systems
        public bool IsLoadingScreenActive => _isLoading;
        public float CurrentProgress => _currentProgress;
        public float TargetProgress => _targetProgress;
    }
}