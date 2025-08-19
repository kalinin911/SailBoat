using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Services
{
    public interface IObjectPool<T> where T : Component
    {
        T Get();
        void Return(T item);
        void Prewarm(int count);
        UniTask PrewarmAsync(int count);
        void Clear();
        
        // Performance monitoring
        int ActiveCount { get; }
        int AvailableCount { get; }
        int TotalCreated { get; }
        bool IsEmpty { get; }
        bool IsFull { get; }
        
        // Configuration
        void SetMaxSize(int maxSize);
        void SetExpandable(bool expandable);
        void EnableProfiling(bool enable);
        
        // Statistics
        PoolStatistics GetStatistics();
    }
    
    [System.Serializable]
    public class PoolStatistics
    {
        public string PoolName;
        public int ActiveCount;
        public int AvailableCount;
        public int TotalCreated;
        public int MaxSize;
        public bool IsExpandable;
        public float HitRate; // Successful Gets / Total Gets
        public long MemoryUsageBytes;
    }
}