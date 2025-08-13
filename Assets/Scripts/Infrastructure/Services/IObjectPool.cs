using UnityEngine;

namespace Infrastructure.Services
{
    public interface IObjectPool<T> where T : Component
    {
        T Get();
        void Return(T item);
        void Prewarm(int count);
        void Clear();
    }
}