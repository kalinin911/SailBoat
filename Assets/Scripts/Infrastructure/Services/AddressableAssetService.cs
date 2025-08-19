using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infrastructure.Services
{
    public class AddressableAssetService : IAssetService
    {
        [System.Serializable]
        public class AssetPreloadGroup
        {
            public string groupName;
            public string[] assetKeys;
            public bool preloadOnStart;
        }

        private readonly Dictionary<string, AsyncOperationHandle> _loadedAssets = new();
        private readonly Dictionary<string, object> _cachedAssets = new();
        private readonly List<AssetPreloadGroup> _preloadGroups = new();
        
        private bool _isInitialized = false;
        private float _preloadProgress = 0f;

        public float PreloadProgress => _preloadProgress;
        public bool IsInitialized => _isInitialized;

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
                return;

            Debug.Log("Initializing Addressable Asset Service...");
            
            await InitializeAddressables();
            await PreloadEssentialAssets();
            
            _isInitialized = true;
            Debug.Log("Addressable Asset Service initialized successfully!");
        }

        private async UniTask InitializeAddressables()
        {
            try
            {
                var initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask();
                Debug.Log("Addressables initialized");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to initialize Addressables: {ex.Message}");
            }
        }

        public async UniTask<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("Asset key is null or empty");
                return null;
            }

            // Check cache first
            if (_cachedAssets.TryGetValue(key, out var cachedAsset))
            {
                if (cachedAsset is T castedAsset)
                    return castedAsset;
            }

            // Check if already loaded
            if (_loadedAssets.TryGetValue(key, out var existingHandle))
            {
                if (existingHandle.Result is T existingAsset)
                {
                    _cachedAssets[key] = existingAsset;
                    return existingAsset;
                }
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
                _loadedAssets[key] = handle;

                var result = await handle.ToUniTask();
                
                if (result == null)
                {
                    Debug.LogWarning($"Failed to load asset with key: {key}");
                    return await LoadFromResourcesAsync<T>(key);
                }

                _cachedAssets[key] = result;
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading asset {key}: {ex.Message}");
                return await LoadFromResourcesAsync<T>(key);
            }
        }

        public async UniTask<T[]> LoadAssetsAsync<T>(string[] keys) where T : Object
        {
            var tasks = keys.Select(key => LoadAssetAsync<T>(key));
            var results = await UniTask.WhenAll(tasks);
            return results.Where(result => result != null).ToArray();
        }

        public async UniTask PreloadAssetsAsync(string[] keys, System.Action<float> progressCallback = null)
        {
            if (keys == null || keys.Length == 0)
                return;

            Debug.Log($"Preloading {keys.Length} assets...");
            
            var totalAssets = keys.Length;
            var loadedAssets = 0;

            var tasks = new List<UniTask>();

            foreach (var key in keys)
            {
                var task = PreloadSingleAssetAsync(key, () =>
                {
                    loadedAssets++;
                    var progress = (float)loadedAssets / totalAssets;
                    _preloadProgress = progress;
                    progressCallback?.Invoke(progress);
                });
                
                tasks.Add(task);
            }

            await UniTask.WhenAll(tasks);
            Debug.Log("Asset preloading completed!");
        }

        private async UniTask PreloadSingleAssetAsync(string key, System.Action onComplete)
        {
            try
            {
                await LoadAssetAsync<Object>(key);
                onComplete?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to preload asset {key}: {ex.Message}");
                onComplete?.Invoke(); // Still call complete to maintain progress
            }
        }

        public async UniTask PreloadAssetGroup(string groupName, System.Action<float> progressCallback = null)
        {
            var group = _preloadGroups.FirstOrDefault(g => g.groupName == groupName);
            if (group == null)
            {
                Debug.LogWarning($"Asset group '{groupName}' not found");
                return;
            }

            await PreloadAssetsAsync(group.assetKeys, progressCallback);
        }

        private async UniTask PreloadEssentialAssets()
        {
            // Define essential assets that should be loaded immediately
            var essentialAssets = new[]
            {
                "DefaultMap",
                "WaterTile",
                "TerrainTile"
            };

            await PreloadAssetsAsync(essentialAssets, progress =>
            {
                Debug.Log($"Essential assets preload progress: {progress * 100:F1}%");
            });
        }

        private async UniTask<T> LoadFromResourcesAsync<T>(string key) where T : Object
        {
            try
            {
                // Try different possible paths in Resources
                var possiblePaths = new[]
                {
                    key,
                    $"Prefabs/{key}",
                    $"Maps/{key}",
                    $"Materials/{key}",
                    $"Textures/{key}"
                };

                foreach (var path in possiblePaths)
                {
                    var resourceRequest = Resources.LoadAsync<T>(path);
                    await resourceRequest.ToUniTask();
                    
                    if (resourceRequest.asset != null)
                    {
                        var asset = resourceRequest.asset as T;
                        if (asset != null)
                        {
                            _cachedAssets[key] = asset;
                            Debug.Log($"Loaded {key} from Resources at path: {path}");
                            return asset;
                        }
                    }
                }
                
                Debug.LogWarning($"Asset {key} not found in Resources");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load from Resources: {key}, Error: {ex.Message}");
                return null;
            }
        }

        public bool IsAssetLoaded(string key)
        {
            return _cachedAssets.ContainsKey(key) || _loadedAssets.ContainsKey(key);
        }

        public T GetCachedAsset<T>(string key) where T : Object
        {
            if (_cachedAssets.TryGetValue(key, out var asset))
            {
                return asset as T;
            }
            return null;
        }

        public void ReleaseAsset(string key)
        {
            if (_loadedAssets.TryGetValue(key, out var handle))
            {
                try
                {
                    Addressables.Release(handle);
                    _loadedAssets.Remove(key);
                    _cachedAssets.Remove(key);
                    Debug.Log($"Released asset: {key}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error releasing asset {key}: {ex.Message}");
                }
            }
        }

        public void ReleaseAssets(string[] keys)
        {
            foreach (var key in keys)
            {
                ReleaseAsset(key);
            }
        }

        public void ReleaseAllAssets()
        {
            Debug.Log($"Releasing {_loadedAssets.Count} loaded assets...");
            
            foreach (var kvp in _loadedAssets.ToList())
            {
                try
                {
                    Addressables.Release(kvp.Value);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error releasing asset {kvp.Key}: {ex.Message}");
                }
            }
            
            _loadedAssets.Clear();
            _cachedAssets.Clear();
            
            // Force garbage collection after releasing assets
            System.GC.Collect();
            Debug.Log("All assets released");
        }

        public void AddPreloadGroup(AssetPreloadGroup group)
        {
            _preloadGroups.Add(group);
        }

        public void RemovePreloadGroup(string groupName)
        {
            _preloadGroups.RemoveAll(g => g.groupName == groupName);
        }

        // Memory management
        public void ClearCache()
        {
            _cachedAssets.Clear();
            System.GC.Collect();
            Debug.Log("Asset cache cleared");
        }

        public long GetCacheMemoryUsage()
        {
            long totalSize = 0;
            
            foreach (var asset in _cachedAssets.Values)
            {
                if (asset is Texture2D texture)
                {
                    totalSize += texture.width * texture.height * 4; // Approximate RGBA size
                }
                else if (asset is Mesh mesh)
                {
                    totalSize += mesh.vertexCount * 32; // Approximate vertex data size
                }
                else if (asset is AudioClip audio)
                {
                    totalSize += audio.samples * audio.channels * 2; // 16-bit audio
                }
                // Add more asset types as needed
            }
            
            return totalSize;
        }

        public void LogCacheStatus()
        {
            Debug.Log($"Asset Cache Status:");
            Debug.Log($"- Cached Assets: {_cachedAssets.Count}");
            Debug.Log($"- Loaded Handles: {_loadedAssets.Count}");
            Debug.Log($"- Memory Usage: {GetCacheMemoryUsage() / 1024 / 1024:F2} MB");
        }

        // Async resource monitoring
        public async UniTask<bool> WaitForAssetAsync(string key, float timeout = 10f)
        {
            var startTime = Time.time;
            
            while (!IsAssetLoaded(key))
            {
                if (Time.time - startTime > timeout)
                {
                    Debug.LogWarning($"Timeout waiting for asset: {key}");
                    return false;
                }
                
                await UniTask.Delay(100); // Check every 100ms
            }
            
            return true;
        }

        // Batch operations
        public async UniTask<Dictionary<string, T>> LoadAssetBatchAsync<T>(string[] keys) where T : Object
        {
            var results = new Dictionary<string, T>();
            var tasks = keys.Select(async key =>
            {
                var asset = await LoadAssetAsync<T>(key);
                return new { Key = key, Asset = asset };
            });

            var loadResults = await UniTask.WhenAll(tasks);
            
            foreach (var result in loadResults)
            {
                if (result.Asset != null)
                {
                    results[result.Key] = result.Asset;
                }
            }

            return results;
        }
    }
}