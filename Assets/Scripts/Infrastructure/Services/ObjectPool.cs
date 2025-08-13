using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services
{
    public class ObjectPool<T> : IObjectPool<T> where T : Component
    {
        private readonly Stack<T> _pool = new();
        private readonly T _prefab;
        private readonly DiContainer _container;
        private readonly Transform _parent;

        public ObjectPool(T prefab, DiContainer container, Transform parent = null)
        {
            _prefab = prefab;
            _container = container;
            _parent = parent;
        }
        
        public T Get()
        {
            if (_pool.Count > 0)
            {
                var item = _pool.Pop();
                item.gameObject.SetActive(true);
                return item;
            }

            return CreateNew();
        }

        public void Return(T item)
        {
            if (item == null)
                return;
            
            item.gameObject.SetActive(false);
            
            if(_parent != null)
                item.transform.SetParent(_parent);

            _pool.Push(item);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var item = CreateNew();
                Return(item);
            }
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                if(item != null)
                    Object.Destroy(item.gameObject);
            }
        }

        private T CreateNew()
        {
            var instance = _container.InstantiatePrefab(_prefab, _parent);
            return instance.GetComponent<T>();
        }
    }
}