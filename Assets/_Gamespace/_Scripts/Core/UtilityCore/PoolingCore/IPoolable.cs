using UnityEngine;

namespace _Workspace._Scripts.Core.UtilityCore.PoolingCore
{
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReturnToPool();
    }
    
    public interface IPoolableWithInit<T> : IPoolable where T : MonoBehaviour, IPoolableWithInit<T>
    {
        void InitPool(Pooler<T> pool);
    }
}