using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Services
{
    public interface IAssetService
    {
        UniTask<T> LoadAssetAsync<T>(string key) where T : Object;
        void ReleaseAsset(string key);
        void ReleaseAllAssets();
    }
}