using UnityEngine;
using System.Collections;

namespace Infrastructure.Services
{
    public class PerformanceManager : MonoBehaviour
    {
        [Header("Performance Settings")]
        [SerializeField] private int _targetFPS = 30;
        [SerializeField] private bool _enableFPSDisplay = true;
        [SerializeField] private bool _enableMemoryOptimization = true;
        [SerializeField] private bool _enableMobileOptimizations = true;

        [Header("Quality Settings")]
        [SerializeField] private int _maxParticles = 100;
        [SerializeField] private float _lodBias = 0.7f;
        [SerializeField] private int _maxLODLevel = 1;

        [Header("Memory Management")]
        [SerializeField] private float _memoryCleanupInterval = 30f;

        private float _deltaTime = 0.0f;
        private GUIStyle _fpsStyle;
        private Rect _fpsRect;
        private bool _showFPS = false;

        private void Start()
        {
            InitializePerformanceSettings();
            SetupFPSDisplay();
            
            if (_enableMemoryOptimization)
            {
                StartCoroutine(MemoryCleanupRoutine());
            }
        }

        private void InitializePerformanceSettings()
        {
            Application.targetFrameRate = _targetFPS;
            QualitySettings.vSyncCount = 0;

            if (_enableMobileOptimizations && Application.isMobilePlatform)
            {
                ApplyMobileOptimizations();
            }
        }

        private void ApplyMobileOptimizations()
        {
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 20f;
            QualitySettings.lodBias = _lodBias;
            QualitySettings.maximumLODLevel = _maxLODLevel;
            QualitySettings.particleRaycastBudget = _maxParticles;
            QualitySettings.globalTextureMipmapLimit = 1;
            Time.fixedDeltaTime = 1.0f / 30.0f;

            Debug.Log("Mobile optimizations applied");
        }

        private void SetupFPSDisplay()
        {
            if (!_enableFPSDisplay) return;

            _fpsRect = new Rect(10, 10, 150, 25);
            _fpsStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 20,
                normal = { textColor = Color.white }
            };
            _showFPS = true;
        }

        private void Update()
        {
            if (_enableFPSDisplay)
            {
                UpdateFPSCalculation();
            }

            HandleFPSToggle();
        }

        private void UpdateFPSCalculation()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        private void HandleFPSToggle()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                _showFPS = !_showFPS;
            }
        }

        private void OnGUI()
        {
            if (_enableFPSDisplay && _showFPS)
            {
                DisplayFPS();
            }
        }

        private void DisplayFPS()
        {
            float msec = _deltaTime * 1000.0f;
            float fps = 1.0f / _deltaTime;
            string text = $"{msec:0.0} ms ({fps:0.} fps)";

            GUI.backgroundColor = Color.black;
            GUI.Box(_fpsRect, "");
            GUI.Label(_fpsRect, text, _fpsStyle);
        }

        private IEnumerator MemoryCleanupRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_memoryCleanupInterval);
                PerformMemoryCleanup();
            }
        }

        private void PerformMemoryCleanup()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            Resources.UnloadUnusedAssets();
        }

        public void SetTargetFPS(int fps)
        {
            _targetFPS = Mathf.Clamp(fps, 15, 120);
            Application.targetFrameRate = _targetFPS;
        }

        public void EnableMobileMode(bool enable)
        {
            _enableMobileOptimizations = enable;
            if (enable && Application.isMobilePlatform)
            {
                ApplyMobileOptimizations();
            }
        }

        public float GetCurrentFPS()
        {
            return 1.0f / _deltaTime;
        }

        public void ForceMemoryCleanup()
        {
            PerformMemoryCleanup();
        }

        public void SetMemoryCleanupInterval(float interval)
        {
            _memoryCleanupInterval = Mathf.Max(5f, interval);
        }

        public void EnableFPSDisplay(bool enable)
        {
            _enableFPSDisplay = enable;
            if (!enable)
            {
                _showFPS = false;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Application.targetFrameRate = pauseStatus ? 10 : _targetFPS;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Application.targetFrameRate = hasFocus ? _targetFPS : 10;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}