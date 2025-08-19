using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services
{
    public class ObjectPool<T> : IObjectPool<T> where T : Component
    {
        private readonly Stack<T> _pool = new();
        private readonly HashSet<T> _activeObjects = new();
        private readonly T _prefab;
        private readonly DiContainer _container;
        private readonly Transform _parent;
        private readonly string _poolName;

        [Header("Pool Configuration")]
        [SerializeField] private int _maxSize = 100;
        [SerializeField] private bool _expandable = true;
        [SerializeField] private bool _enableProfiling = false;
        [SerializeField] private int _prewarmedCount = 0;

        [Header("Performance Settings")]
        [SerializeField] private int _maxInstantiationsPerFrame = 5;
        [SerializeField] private bool _validateReturns = true;
        [SerializeField] private bool _resetOnReturn = true;

        // Statistics
        private int _totalCreated = 0;
        private int _totalGets = 0;
        private int _totalHits = 0;
        private Queue<System.Action> _instantiationQueue = new();
        private Coroutine _instantiationCoroutine;

        public ObjectPool(T prefab, DiContainer container, Transform parent = null, string poolName = null)
        {
            _prefab = prefab;
            _container = container;
            _parent = parent;
            _poolName = poolName ?? typeof(T).Name;

            if (_parent == null)
            {
                var poolGO = new GameObject($"Pool_{_poolName}");
                _parent = poolGO.transform;
                Object.DontDestroyOnLoad(poolGO);
            }
        }

        // Public Properties
        public int ActiveCount => _activeObjects.Count;
        public int AvailableCount => _pool.Count;
        public int TotalCreated => _totalCreated;
        public bool IsEmpty => _pool.Count == 0;
        public bool IsFull => _pool.Count >= _maxSize;

        public T Get()
        {
            _totalGets++;
            
            T item;
            
            if (_pool.Count > 0)
            {
                item = _pool.Pop();
                _totalHits++;
                
                if (_enableProfiling)
                {
                    Debug.Log($"Pool '{_poolName}': Reused object ({AvailableCount} remaining)");
                }
            }
            else
            {
                if (!_expandable && _totalCreated >= _maxSize)
                {
                    Debug.LogWarning($"Pool '{_poolName}' reached max size ({_maxSize}) and is not expandable!");
                    return null;
                }
                
                item = CreateNew();
                
                if (_enableProfiling)
                {
                    Debug.Log($"Pool '{_poolName}': Created new object (Total created: {_totalCreated})");
                }
            }

            if (item != null)
            {
                ActivateItem(item);
                _activeObjects.Add(item);
            }

            return item;
        }

        public void Return(T item)
        {
            if (item == null)
                return;

            if (_validateReturns && !_activeObjects.Contains(item))
            {
                Debug.LogWarning($"Pool '{_poolName}': Attempting to return object that wasn't taken from this pool!");
                return;
            }

            _activeObjects.Remove(item);
            
            if (_pool.Count >= _maxSize)
            {
                // Pool is full, destroy the object
                if (_enableProfiling)
                {
                    Debug.Log($"Pool '{_poolName}': Pool full, destroying returned object");
                }
                
                if (item != null)
                    Object.Destroy(item.gameObject);
                return;
            }

            DeactivateItem(item);
            
            if (_parent != null)
                item.transform.SetParent(_parent);

            _pool.Push(item);
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Returned object ({AvailableCount} available)");
            }
        }

        public void Prewarm(int count)
        {
            count = Mathf.Min(count, _maxSize);
            
            for (int i = 0; i < count; i++)
            {
                if (_pool.Count >= _maxSize)
                    break;
                    
                var item = CreateNew();
                if (item != null)
                {
                    Return(item);
                }
            }
            
            _prewarmedCount = count;
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Prewarmed {count} objects");
            }
        }

        public async UniTask PrewarmAsync(int count)
        {
            count = Mathf.Min(count, _maxSize);
            var itemsCreated = 0;
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Starting async prewarm of {count} objects");
            }

            for (int i = 0; i < count; i++)
            {
                if (_pool.Count >= _maxSize)
                    break;

                // Create items in batches to avoid frame drops
                if (itemsCreated % _maxInstantiationsPerFrame == 0 && itemsCreated > 0)
                {
                    await UniTask.NextFrame();
                }

                var item = CreateNew();
                if (item != null)
                {
                    Return(item);
                    itemsCreated++;
                }
            }
            
            _prewarmedCount = itemsCreated;
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Async prewarm completed - {itemsCreated} objects created");
            }
        }

        public void Clear()
        {
            // Clear active objects (force return them)
            var activeItems = new List<T>(_activeObjects);
            foreach (var item in activeItems)
            {
                if (item != null)
                {
                    _activeObjects.Remove(item);
                    Object.Destroy(item.gameObject);
                }
            }
            _activeObjects.Clear();

            // Clear pooled objects
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                if (item != null)
                    Object.Destroy(item.gameObject);
            }

            // Reset statistics
            _totalCreated = 0;
            _totalGets = 0;
            _totalHits = 0;
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Cleared all objects");
            }
        }

        private T CreateNew()
        {
            if (_prefab == null)
            {
                Debug.LogError($"Pool '{_poolName}': Prefab is null!");
                return null;
            }

            try
            {
                GameObject instance;
                
                if (_container != null)
                {
                    instance = _container.InstantiatePrefab(_prefab.gameObject, _parent);
                }
                else
                {
                    instance = Object.Instantiate(_prefab.gameObject, _parent);
                }
                
                var component = instance.GetComponent<T>();
                if (component == null)
                {
                    Debug.LogError($"Pool '{_poolName}': Instantiated object doesn't have component {typeof(T).Name}!");
                    Object.Destroy(instance);
                    return null;
                }

                _totalCreated++;
                return component;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Pool '{_poolName}': Failed to create new object - {ex.Message}");
                return null;
            }
        }

        private void ActivateItem(T item)
        {
            if (item == null) return;
            
            item.gameObject.SetActive(true);
            
            if (_resetOnReturn)
            {
                // Reset transform
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localScale = Vector3.one;
                
                // Reset common components
                var rigidbody = item.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.velocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
                
                var rigidbody2D = item.GetComponent<Rigidbody2D>();
                if (rigidbody2D != null)
                {
                    rigidbody2D.velocity = Vector2.zero;
                    rigidbody2D.angularVelocity = 0f;
                }
            }

            // Call OnPoolGet if the component implements it
            if (item is IPoolable poolable)
            {
                poolable.OnPoolGet();
            }
        }

        private void DeactivateItem(T item)
        {
            if (item == null) return;
            
            // Call OnPoolReturn if the component implements it
            if (item is IPoolable poolable)
            {
                poolable.OnPoolReturn();
            }
            
            item.gameObject.SetActive(false);
        }

        // Configuration methods
        public void SetMaxSize(int maxSize)
        {
            _maxSize = Mathf.Max(1, maxSize);
            
            // If current pool exceeds new max size, destroy excess objects
            while (_pool.Count > _maxSize)
            {
                var item = _pool.Pop();
                if (item != null)
                    Object.Destroy(item.gameObject);
            }
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Max size set to {_maxSize}");
            }
        }

        public void SetExpandable(bool expandable)
        {
            _expandable = expandable;
            
            if (_enableProfiling)
            {
                Debug.Log($"Pool '{_poolName}': Expandable set to {_expandable}");
            }
        }

        public void EnableProfiling(bool enable)
        {
            _enableProfiling = enable;
        }

        public PoolStatistics GetStatistics()
        {
            var hitRate = _totalGets > 0 ? (float)_totalHits / _totalGets : 0f;
            var memoryUsage = EstimateMemoryUsage();
            
            return new PoolStatistics
            {
                PoolName = _poolName,
                ActiveCount = ActiveCount,
                AvailableCount = AvailableCount,
                TotalCreated = _totalCreated,
                MaxSize = _maxSize,
                IsExpandable = _expandable,
                HitRate = hitRate,
                MemoryUsageBytes = memoryUsage
            };
        }

        private long EstimateMemoryUsage()
        {
            if (_prefab == null) return 0;
            
            // Rough estimation based on common component sizes
            long baseSize = 1024; // Base GameObject overhead
            
            // Add estimates for common components
            if (_prefab.GetComponent<Renderer>()) baseSize += 512;
            if (_prefab.GetComponent<Collider>()) baseSize += 256;
            if (_prefab.GetComponent<Rigidbody>()) baseSize += 128;
            if (_prefab.GetComponent<AudioSource>()) baseSize += 256;
            
            return baseSize * (_totalCreated);
        }

        // Debug and utility methods
        public void LogStatistics()
        {
            var stats = GetStatistics();
            Debug.Log($"=== Pool '{_poolName}' Statistics ===");
            Debug.Log($"Active: {stats.ActiveCount}, Available: {stats.AvailableCount}, Total Created: {stats.TotalCreated}");
            Debug.Log($"Hit Rate: {stats.HitRate:P2}, Memory: {stats.MemoryUsageBytes / 1024}KB");
            Debug.Log($"Max Size: {stats.MaxSize}, Expandable: {stats.IsExpandable}");
        }

        public bool ValidatePool()
        {
            var isValid = true;
            
            if (_prefab == null)
            {
                Debug.LogError($"Pool '{_poolName}': Prefab is null!");
                isValid = false;
            }
            
            if (_maxSize <= 0)
            {
                Debug.LogError($"Pool '{_poolName}': Max size must be greater than 0!");
                isValid = false;
            }
            
            // Check for orphaned objects
            _activeObjects.RemoveWhere(item => item == null);
            
            return isValid;
        }
    }
    
    public interface IPoolable
    {
        void OnPoolGet();
        void OnPoolReturn();
    }
}