using UnityEngine;
using System;

namespace Infrastructure.Services
{
    /// <summary>
    /// Detects device capabilities and recommends optimal settings for mobile devices
    /// </summary>
    public class DeviceCapabilityDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private bool _enableAutoDetection = true;
        [SerializeField] private bool _logDetectionResults = true;

        public static DeviceCapabilityDetector Instance { get; private set; }

        public DeviceCapability CurrentDevice { get; private set; }
        public OptimalSettings RecommendedSettings { get; private set; }

        public event Action<DeviceCapability> OnDeviceDetected;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (_enableAutoDetection)
                {
                    DetectDevice();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void DetectDevice()
        {
            CurrentDevice = AnalyzeDevice();
            RecommendedSettings = GenerateOptimalSettings(CurrentDevice);
            
            if (_logDetectionResults)
            {
                LogDetectionResults();
            }
            
            OnDeviceDetected?.Invoke(CurrentDevice);
        }

        private DeviceCapability AnalyzeDevice()
        {
            var capability = new DeviceCapability
            {
                Platform = Application.platform,
                DeviceModel = SystemInfo.deviceModel,
                DeviceName = SystemInfo.deviceName,
                ProcessorType = SystemInfo.processorType,
                ProcessorFrequency = SystemInfo.processorFrequency,
                ProcessorCount = SystemInfo.processorCount,
                MemorySize = SystemInfo.systemMemorySize,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsMemorySize = SystemInfo.graphicsMemorySize,
                GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                MaxTextureSize = SystemInfo.maxTextureSize,
                SupportsRenderTextures = SystemInfo.supportsRenderTextures,
                SupportsComputeShaders = SystemInfo.supportsComputeShaders,
                BatteryLevel = SystemInfo.batteryLevel,
                BatteryStatus = SystemInfo.batteryStatus
            };

            // Calculate performance tier
            capability.PerformanceTier = CalculatePerformanceTier(capability);
            capability.QualityLevel = RecommendQualityLevel(capability.PerformanceTier);

            return capability;
        }

        private PerformanceTier CalculatePerformanceTier(DeviceCapability device)
        {
            int score = 0;

            // Memory scoring (40% weight)
            if (device.MemorySize >= 6000) score += 40;
            else if (device.MemorySize >= 4000) score += 30;
            else if (device.MemorySize >= 2000) score += 20;
            else score += 10;

            // CPU scoring (30% weight)
            if (device.ProcessorCount >= 8) score += 30;
            else if (device.ProcessorCount >= 6) score += 25;
            else if (device.ProcessorCount >= 4) score += 20;
            else score += 10;

            // GPU scoring (30% weight)
            if (device.GraphicsMemorySize >= 2000) score += 30;
            else if (device.GraphicsMemorySize >= 1000) score += 25;
            else if (device.GraphicsMemorySize >= 512) score += 20;
            else score += 10;

            // Platform adjustments
            if (device.Platform == RuntimePlatform.IPhonePlayer)
            {
                if (device.DeviceModel.Contains("iPhone1") && 
                    (device.DeviceModel.Contains("3") || device.DeviceModel.Contains("4") || device.DeviceModel.Contains("5")))
                    score += 20; // Modern iPhones generally perform better
            }

            // Determine tier based on score
            if (score >= 85) return PerformanceTier.High;
            if (score >= 65) return PerformanceTier.Medium;
            if (score >= 45) return PerformanceTier.Low;
            return PerformanceTier.VeryLow;
        }

        private QualityLevel RecommendQualityLevel(PerformanceTier tier)
        {
            return tier switch
            {
                PerformanceTier.High => QualityLevel.High,
                PerformanceTier.Medium => QualityLevel.Medium,
                PerformanceTier.Low => QualityLevel.Low,
                PerformanceTier.VeryLow => QualityLevel.VeryLow,
                _ => QualityLevel.Medium
            };
        }

        private OptimalSettings GenerateOptimalSettings(DeviceCapability device)
        {
            var settings = new OptimalSettings();

            switch (device.PerformanceTier)
            {
                case PerformanceTier.High:
                    settings.TargetFPS = 60;
                    settings.TextureQuality = 1.0f;
                    settings.ShadowDistance = 50f;
                    settings.MaxParticles = 200;
                    settings.AntiAliasing = 2;
                    settings.EnableShadows = true;
                    settings.CullingDistance = 50f;
                    settings.LODBias = 1.0f;
                    settings.TilesPerBatch = 100;
                    break;

                case PerformanceTier.Medium:
                    settings.TargetFPS = 45;
                    settings.TextureQuality = 0.75f;
                    settings.ShadowDistance = 30f;
                    settings.MaxParticles = 100;
                    settings.AntiAliasing = 0;
                    settings.EnableShadows = false;
                    settings.CullingDistance = 35f;
                    settings.LODBias = 0.8f;
                    settings.TilesPerBatch = 75;
                    break;

                case PerformanceTier.Low:
                    settings.TargetFPS = 30;
                    settings.TextureQuality = 0.5f;
                    settings.ShadowDistance = 15f;
                    settings.MaxParticles = 50;
                    settings.AntiAliasing = 0;
                    settings.EnableShadows = false;
                    settings.CullingDistance = 25f;
                    settings.LODBias = 0.6f;
                    settings.TilesPerBatch = 50;
                    break;

                case PerformanceTier.VeryLow:
                    settings.TargetFPS = 24;
                    settings.TextureQuality = 0.25f;
                    settings.ShadowDistance = 10f;
                    settings.MaxParticles = 25;
                    settings.AntiAliasing = 0;
                    settings.EnableShadows = false;
                    settings.CullingDistance = 15f;
                    settings.LODBias = 0.4f;
                    settings.TilesPerBatch = 25;
                    break;
            }

            // Battery level adjustments
            if (device.BatteryLevel < 0.2f) // Less than 20% battery
            {
                settings.TargetFPS = Mathf.Max(15, settings.TargetFPS - 15);
                settings.MaxParticles /= 2;
                settings.CullingDistance *= 0.7f;
            }

            return settings;
        }

        private void LogDetectionResults()
        {
            Debug.Log($"=== Device Detection Results ===");
            Debug.Log($"Device: {CurrentDevice.DeviceModel}");
            Debug.Log($"Platform: {CurrentDevice.Platform}");
            Debug.Log($"Performance Tier: {CurrentDevice.PerformanceTier}");
            Debug.Log($"Memory: {CurrentDevice.MemorySize}MB");
            Debug.Log($"CPU: {CurrentDevice.ProcessorCount} cores @ {CurrentDevice.ProcessorFrequency}MHz");
            Debug.Log($"GPU: {CurrentDevice.GraphicsDeviceName} ({CurrentDevice.GraphicsMemorySize}MB)");
            Debug.Log($"Recommended Quality: {CurrentDevice.QualityLevel}");
            Debug.Log($"Target FPS: {RecommendedSettings.TargetFPS}");
            Debug.Log($"Battery: {CurrentDevice.BatteryLevel * 100:F0}% ({CurrentDevice.BatteryStatus})");
        }

        public void ApplyRecommendedSettings()
        {
            if (RecommendedSettings == null) return;

            Application.targetFrameRate = RecommendedSettings.TargetFPS;
            QualitySettings.antiAliasing = RecommendedSettings.AntiAliasing;
            QualitySettings.shadows = RecommendedSettings.EnableShadows ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.shadowDistance = RecommendedSettings.ShadowDistance;
            QualitySettings.lodBias = RecommendedSettings.LODBias;
            QualitySettings.particleRaycastBudget = RecommendedSettings.MaxParticles;
            
            // Apply texture quality
            var textureLimit = Mathf.RoundToInt((1f - RecommendedSettings.TextureQuality) * 3);
            QualitySettings.globalTextureMipmapLimit = textureLimit;

            Debug.Log($"Applied recommended settings for {CurrentDevice.PerformanceTier} tier device");
        }

        public bool ShouldReduceQuality()
        {
            return CurrentDevice.PerformanceTier <= PerformanceTier.Low || 
                   CurrentDevice.BatteryLevel < 0.2f;
        }

        public bool IsLowEndDevice()
        {
            return CurrentDevice.PerformanceTier == PerformanceTier.VeryLow ||
                   CurrentDevice.MemorySize < 2000;
        }
    }

    [Serializable]
    public class DeviceCapability
    {
        public RuntimePlatform Platform;
        public string DeviceModel;
        public string DeviceName;
        public string ProcessorType;
        public int ProcessorFrequency;
        public int ProcessorCount;
        public int MemorySize;
        public string GraphicsDeviceName;
        public int GraphicsMemorySize;
        public string GraphicsDeviceVersion;
        public int MaxTextureSize;
        public bool SupportsRenderTextures;
        public bool SupportsComputeShaders;
        public float BatteryLevel;
        public BatteryStatus BatteryStatus;
        
        public PerformanceTier PerformanceTier;
        public QualityLevel QualityLevel;
    }

    [Serializable]
    public class OptimalSettings
    {
        public int TargetFPS = 30;
        public float TextureQuality = 0.75f;
        public float ShadowDistance = 20f;
        public int MaxParticles = 100;
        public int AntiAliasing = 0;
        public bool EnableShadows = false;
        public float CullingDistance = 30f;
        public float LODBias = 0.7f;
        public int TilesPerBatch = 50;
    }

    public enum PerformanceTier
    {
        VeryLow,
        Low, 
        Medium,
        High
    }

    public enum QualityLevel
    {
        VeryLow,
        Low,
        Medium, 
        High
    }
}