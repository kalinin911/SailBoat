using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

namespace Infrastructure.UI
{
    public class LoadingScreenManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _loadingScreenPanel;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Animation _loadingAnimation;

        [Header("Loading Settings")]
        [SerializeField] private float _minimumLoadingTime = 2f;
        [SerializeField] private float _progressSmoothSpeed = 2f;
        [SerializeField] private bool _showPercentage = true;
        [SerializeField] private bool _showDetailedStatus = true;

        [Header("Visual Effects")]
        [SerializeField] private ParticleSystem _loadingParticles;
        [SerializeField] private AudioSource _loadingAudioSource;
        [SerializeField] private AudioClip _loadingMusic;

        private float _targetProgress = 0f;
        private float _currentProgress = 0f;
        private bool _isLoading = false;
        private float _loadingStartTime;

        private void Awake()
        {
            SetupLoadingScreen();
        }

        private void Start()
        {
            // Hide loading screen initially
            HideLoadingScreen();
        }

        private void Update()
        {
            if (_isLoading)
            {
                UpdateProgressBar();
            }
        }

        private void SetupLoadingScreen()
        {
            if (_loadingScreenPanel == null)
            {
                Debug.LogError("Loading screen panel not assigned!");
                return;
            }

            // Setup progress bar
            if (_progressBar != null)
            {
                _progressBar.minValue = 0f;
                _progressBar.maxValue = 1f;
                _progressBar.value = 0f;
            }

            // Setup text components
            if (_progressText != null)
                _progressText.text = "0%";
                
            if (_statusText != null)
                _statusText.text = "Initializing...";

            // Setup audio
            if (_loadingAudioSource != null && _loadingMusic != null)
            {
                _loadingAudioSource.clip = _loadingMusic;
                _loadingAudioSource.loop = true;
                _loadingAudioSource.volume = 0.3f;
            }
        }

        public void ShowLoadingScreen(string initialStatus = "Loading...")
        {
            _loadingScreenPanel.SetActive(true);
            _isLoading = true;
            _loadingStartTime = Time.time;
            _currentProgress = 0f;
            _targetProgress = 0f;

            if (_statusText != null)
                _statusText.text = initialStatus;

            if (_progressText != null)
                _progressText.text = "0%";

            if (_progressBar != null)
                _progressBar.value = 0f;

            // Start visual effects
            StartLoadingEffects();
        }

        public void HideLoadingScreen()
        {
            _loadingScreenPanel.SetActive(false);
            _isLoading = false;

            // Stop visual effects
            StopLoadingEffects();
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
            
            if (!string.IsNullOrEmpty(status) && _statusText != null)
            {
                _statusText.text = status;
            }

            if (_showDetailedStatus)
            {
                Debug.Log($"Loading progress: {progress * 100:F1}% - {status}");
            }
        }

        private void UpdateProgressBar()
        {
            // Smooth progress bar animation
            _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, 
                _progressSmoothSpeed * Time.deltaTime);

            if (_progressBar != null)
            {
                _progressBar.value = _currentProgress;
            }

            if (_progressText != null && _showPercentage)
            {
                _progressText.text = $"{_currentProgress * 100:F0}%";
            }

            // Check if loading is complete
            if (_currentProgress >= 0.99f && _targetProgress >= 1f)
            {
                OnLoadingComplete();
            }
        }

        private void OnLoadingComplete()
        {
            if (_statusText != null)
                _statusText.text = "Loading Complete!";

            StartCoroutine(DelayedHideLoadingScreen());
        }

        private IEnumerator DelayedHideLoadingScreen()
        {
            yield return new WaitForSeconds(0.5f); // Brief pause to show completion
            HideLoadingScreen();
        }

        private void StartLoadingEffects()
        {
            // Start loading animation
            if (_loadingAnimation != null)
            {
                _loadingAnimation.Play();
            }

            // Start particles
            if (_loadingParticles != null)
            {
                _loadingParticles.Play();
            }

            // Start loading music
            if (_loadingAudioSource != null && _loadingMusic != null)
            {
                _loadingAudioSource.Play();
            }

            // Start background fade animation
            if (_backgroundImage != null)
            {
                StartCoroutine(AnimateBackgroundFade());
            }
        }

        private void StopLoadingEffects()
        {
            // Stop loading animation
            if (_loadingAnimation != null)
            {
                _loadingAnimation.Stop();
            }

            // Stop particles
            if (_loadingParticles != null)
            {
                _loadingParticles.Stop();
            }

            // Stop loading music
            if (_loadingAudioSource != null)
            {
                _loadingAudioSource.Stop();
            }
        }

        private IEnumerator AnimateBackgroundFade()
        {
            if (_backgroundImage == null) yield break;

            var originalColor = _backgroundImage.color;
            var time = 0f;
            var duration = 2f;

            while (_isLoading)
            {
                time += Time.deltaTime;
                var alpha = Mathf.Lerp(0.3f, 0.8f, (Mathf.Sin(time / duration) + 1f) / 2f);
                _backgroundImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            _backgroundImage.color = originalColor;
        }

        // Utility methods for different loading scenarios
        public async UniTask LoadGameAsync(System.Func<System.Action<float, string>, UniTask> loadingTask)
        {
            ShowLoadingScreen("Initializing Game...");

            try
            {
                await loadingTask((progress, status) => UpdateProgress(progress, status));
                UpdateProgress(1f, "Loading Complete!");
                
                // Wait for minimum loading time
                await ShowLoadingScreenAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Loading failed: {ex.Message}");
                UpdateProgress(1f, "Loading Failed!");
                await UniTask.Delay(2000); // Show error for 2 seconds
            }
            finally
            {
                HideLoadingScreen();
            }
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            ShowLoadingScreen($"Loading {sceneName}...");

            var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.allowSceneActivation = false;

            while (!asyncOperation.isDone)
            {
                // Update progress (0.9f max because we control the final activation)
                var progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
                UpdateProgress(progress, $"Loading {sceneName}...");

                // Allow scene activation when loading is complete
                if (asyncOperation.progress >= 0.9f)
                {
                    UpdateProgress(1f, "Finalizing...");
                    await UniTask.Delay(500); // Brief pause
                    asyncOperation.allowSceneActivation = true;
                }

                await UniTask.NextFrame();
            }

            HideLoadingScreen();
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

        // Debug methods
        [ContextMenu("Test Loading Screen")]
        private void TestLoadingScreen()
        {
            StartCoroutine(TestLoadingCoroutine());
        }

        private IEnumerator TestLoadingCoroutine()
        {
            ShowLoadingScreen("Testing Loading Screen...");
            
            for (float i = 0; i <= 1f; i += 0.1f)
            {
                UpdateProgress(i, $"Test Phase {i * 100:F0}%");
                yield return new WaitForSeconds(0.5f);
            }
            
            yield return new WaitForSeconds(1f);
            HideLoadingScreen();
        }

        // Public API for external systems
        public bool IsLoadingScreenActive => _loadingScreenPanel.activeInHierarchy;
        public float CurrentProgress => _currentProgress;
        public float TargetProgress => _targetProgress;
    }
}