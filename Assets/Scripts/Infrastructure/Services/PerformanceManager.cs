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

        private float _deltaTime = 0.0f;
        private GUIStyle _style;
        private Rect _rect;
        private bool _showFPS = false;

        private void Start()
        {
            InitializePerformanceSettings();
            
            if (_enableFPSDisplay)
            {
                SetupFPSDisplay();
            }
            
            if (_enableMemoryOptimization)
            {
                StartCoroutine(MemoryCleanupRoutine());
            }
        }

        private void InitializePerformanceSettings()
        {
            // Target FPS for mobile devices
            Application.targetFrameRate = _targetFPS;
            
            // VSync settings
            QualitySettings.vSyncCount = 0;
            
            if (_enableMobileOptimizations)
            {
                ApplyMobileOptimizations();
            }
        }

        private void ApplyMobileOptimizations()
        {
            // Graphics optimizations
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 20f;
            
            // LOD optimizations
            QualitySettings.lodBias = _lodBias;
            QualitySettings.maximumLODLevel = _maxLODLevel;
            
            // Particle optimizations
            QualitySettings.particleRaycastBudget = _maxParticles;
            
            // Texture optimizations
            QualitySettings.globalTextureMipmapLimit = 1; // Half resolution textures
            
            // Physics optimizations
            Time.fixedDeltaTime = 1.0f / 30.0f; // 30 FPS physics
        }

        private void SetupFPSDisplay()
        {
            _rect = new Rect(10, 10, 150, 25);
            _style = new GUIStyle();
            _style.alignment = TextAnchor.UpperLeft;
            _style.fontSize = 20;
            _style.normal.textColor = Color.white;
            _showFPS = true;
        }

        private void Update()
        {
            if (_enableFPSDisplay)
            {
                _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            }
            
            // Toggle FPS display with F key
            if (Input.GetKeyDown(KeyCode.F))
            {
                _showFPS = !_showFPS;
            }
        }

        private void OnGUI()
        {
            if (_enableFPSDisplay && _showFPS)
            {
                float msec = _deltaTime * 1000.0f;
                float fps = 1.0f / _deltaTime;
                string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
                
                // Background
                GUI.backgroundColor = Color.black;
                GUI.Box(_rect, "");
                
                // Text
                GUI.Label(_rect, text, _style);
            }
        }

        private IEnumerator MemoryCleanupRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(30f); // Clean up every 30 seconds
                
                // Force garbage collection
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                
                // Unload unused assets
                Resources.UnloadUnusedAssets();
                
                Debug.Log("Memory cleanup performed");
            }
        }

        public void SetTargetFPS(int fps)
        {
            _targetFPS = fps;
            Application.targetFrameRate = _targetFPS;
        }

        public void EnableMobileMode(bool enable)
        {
            _enableMobileOptimizations = enable;
            if (enable)
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
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // App paused - reduce performance
                Application.targetFrameRate = 10;
            }
            else
            {
                // App resumed - restore performance
                Application.targetFrameRate = _targetFPS;
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}