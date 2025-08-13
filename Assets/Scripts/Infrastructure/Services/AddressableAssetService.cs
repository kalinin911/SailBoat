using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor.VersionControl;
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
                if(existingHandle.Result is T cachedAsset)
                    return cachedAsset;
            }
            
            var handle = Addressables.LoadAssetAsync<T>(key);
            _loadedAssets[key] = handle;

            var result = await handle.ToUniTask();
            return result;
        }

        public void ReleaseAsset(string key)
        {
            if (!_loadedAssets.TryGetValue(key, out var handle))
            {
                return;
            }
            
            Addressables.Release(handle);
            _loadedAssets.Remove(key);
        }

        public void ReleaseAllAssets()
        {
            foreach (var handle in _loadedAssets.Values)
            {
                Addressables.Release(handle);
            }
            
            _loadedAssets.Clear();
        }
    }
}