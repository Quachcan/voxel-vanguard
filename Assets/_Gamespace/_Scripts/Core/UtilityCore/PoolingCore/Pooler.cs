using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Workspace._Scripts.Core.UtilityCore.PoolingCore
{
    public class Pooler<TPoolableObject> : MonoBehaviour where TPoolableObject : MonoBehaviour, IPoolableWithInit<TPoolableObject>
    {
        [SerializeField] private List<PoolEntry> poolEntries;
        [SerializeField, Min(0)] private int poolSizePerType = 5;
        [SerializeField] private bool expandable = true;
        [SerializeField] private int maxPoolSizePerType = 20;
        
        private readonly Dictionary<string, Queue<TPoolableObject>> _pools = new();
        private readonly Dictionary<string, TPoolableObject> _prefabMap = new();
        private readonly HashSet<TPoolableObject> _allActive = new();
        
        public int TotalActiveCount => _allActive.Count;
        public bool IsSingleType => poolEntries != null && poolEntries.Count == 1;

        [Serializable]
        public class PoolEntry
        {
            public string id;
            public TPoolableObject prefab;
        }

        private void Awake()
        {
            InitializePools();
        }

        private void InitializePools()
        {
            if (poolEntries == null || poolEntries.Count == 0)
            {
                Debug.LogWarning("[Pooler] No pool entries defined!");
                return;
            }

            foreach (var entry in poolEntries)
            {
                if (string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogWarning("[Pooler] Pool entry has empty ID, skipping...");
                    continue;
                }

                if (!entry.prefab)
                {
                    Debug.LogWarning($"[Pooler] Pool entry '{entry.id}' has no prefab, skipping...");
                    continue;
                }

                if (_prefabMap.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[Pooler] Duplicate ID '{entry.id}', skipping...");
                    continue;
                }

                _prefabMap[entry.id] = entry.prefab;
                _pools[entry.id] = new Queue<TPoolableObject>();

                for (int i = 0; i < poolSizePerType; i++)
                {
                    TPoolableObject obj = Instantiate(entry.prefab, transform);
                    obj.gameObject.SetActive(false);
                    _pools[entry.id].Enqueue(obj);
                }
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public TPoolableObject Get(string id, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[Pooler] ID is null or empty!");
                return null;
            }

            if (!_pools.ContainsKey(id))
            {
                Debug.LogError($"[Pooler] No pool found for ID: {id}");
                return null;
            }

            TPoolableObject obj;
            
            if (_pools[id].Count > 0)
            {
                obj = _pools[id].Dequeue();
            }
            else
            {
                if (!expandable)
                {
                    Debug.LogWarning($"[Pooler] Pool '{id}' exhausted and not expandable!");
                    return null;
                }

                int currentCount = CountActiveOfType(id);
                if (currentCount >= maxPoolSizePerType)
                {
                    Debug.LogWarning($"[Pooler] Pool '{id}' reached max size ({maxPoolSizePerType})!");
                    return null;
                }

                obj = Instantiate(_prefabMap[id], transform);
            }

            obj.InitPool(this);
            obj.OnGetFromPool();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);
            
            _allActive.Add(obj);
            return obj;
        }

        public TPoolableObject GetFromPool(Vector3 position, Quaternion rotation)
        {
            if (IsSingleType)
            {
                string firstId = new List<string>(_prefabMap.Keys)[0];
                return Get(firstId, position, rotation);
            }
            
            Debug.LogWarning("[Pooler] GetFromPool() is for single-type pools. Use Get(id) or GetRandom() for multi-type pools.");
            return GetRandom(position, rotation);
        }

        public TPoolableObject GetRandom(Vector3 position, Quaternion rotation)
        {
            if (_prefabMap.Count == 0)
            {
                Debug.LogError("[Pooler] No prefabs available!");
                return null;
            }

            var keys = new List<string>(_prefabMap.Keys);
            string randomId = keys[UnityEngine.Random.Range(0, keys.Count)];
            return Get(randomId, position, rotation);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Return(TPoolableObject obj)
        {
            if (obj == null) return;
            if (!_allActive.Contains(obj)) return;
            if (!this || !transform) return;

            string typeId = FindTypeId(obj);
            if (string.IsNullOrEmpty(typeId))
            {
                Debug.LogWarning("[Pooler] Cannot find type ID for returned object, destroying...");
                Destroy(obj.gameObject);
                return;
            }

            _allActive.Remove(obj);
            obj.transform.SetParent(transform, false);
            obj.OnReturnToPool();
            obj.gameObject.SetActive(false);
            
            _pools[typeId].Enqueue(obj);
        }

        public void ReturnToPool(TPoolableObject obj)
        {
            Return(obj);
        }

        public void ReturnAll()
        {
            if (_allActive.Count == 0) return;
            
            var snapshot = new List<TPoolableObject>(_allActive);
            foreach (var obj in snapshot)
            {
                Return(obj);
            }
        }

        public void ReturnAllToPool()
        {
            ReturnAll();
        }

        public int GetActiveCount(string id)
        {
            return CountActiveOfType(id);
        }

        public int ActiveCount => _allActive.Count;

        public void ForEachActive(Action<TPoolableObject> action)
        {
            if (action == null) return;
            
            foreach (var obj in _allActive)
            {
                action(obj);
            }
        }

        public void ForEachActiveOfType(string id, Action<TPoolableObject> action)
        {
            if (action == null) return;

            foreach (var obj in _allActive)
            {
                string typeId = FindTypeId(obj);
                if (typeId == id)
                {
                    action(obj);
                }
            }
        }

        private string FindTypeId(TPoolableObject obj)
        {
            if (obj == null) return null;

            foreach (var kvp in _prefabMap)
            {
                if (obj.name.StartsWith(kvp.Value.name))
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        private int CountActiveOfType(string id)
        {
            int count = 0;
            foreach (var obj in _allActive)
            {
                string typeId = FindTypeId(obj);
                if (typeId == id)
                {
                    count++;
                }
            }
            return count;
        }

        public bool HasType(string id)
        {
            return _pools.ContainsKey(id);
        }

        public List<string> GetAllTypeIds()
        {
            return new List<string>(_prefabMap.Keys);
        }
    }
}