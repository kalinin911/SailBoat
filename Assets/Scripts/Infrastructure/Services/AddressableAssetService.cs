using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infrastructure.Services
{
    public class AddressableAssetService : IAssetService
    {
        private readonly Dictionary<string, AsyncOperationHandle> _loadedAssets = new();

        public async UniTask<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (_loadedAssets.TryGetValue(key, out var existingHandle))
            {
                if (existingHandle.Result is T cachedAsset)
                    return cachedAsset;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
                _loadedAssets[key] = handle;

                var result = await handle.ToUniTask();
                
                if (result == null)
                {
                    Debug.LogError($"Failed to load asset with key: {key}");
                    return null;
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading asset {key}: {ex.Message}");
                
                // Try to load from Resources as fallback
                return await LoadFromResourcesAsync<T>(key);
            }
        }

        private async UniTask<T> LoadFromResourcesAsync<T>(string key) where T : Object
        {
            try
            {
                var resourceRequest = Resources.LoadAsync<T>(key);
                await resourceRequest.ToUniTask();
                return resourceRequest.asset as T;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load from Resources: {key}, Error: {ex.Message}");
                return null;
            }
        }

        public void ReleaseAsset(string key)
        {
            if (_loadedAssets.TryGetValue(key, out var handle))
            {
                try
                {
                    Addressables.Release(handle);
                    _loadedAssets.Remove(key);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error releasing asset {key}: {ex.Message}");
                }
            }
        }

        public void ReleaseAllAssets()
        {
            foreach (var kvp in _loadedAssets)
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
        }
    }
}