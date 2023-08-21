using DC.Data.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DC.Runtime.Creation.Pools
{
    public class PrefabPool<T> : IObjectPool<T> where T : MonoBehaviour
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly List<T> _pool = new();

        public PrefabPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        private IEnumerable<T> Available => _pool.Where(x => x != null && !x.gameObject.activeSelf);
        private IEnumerable<T> InUse => _pool.Where(x => x != null && x.gameObject.activeSelf);

        public T GetObject()
        {

            foreach (var prefab in Available)
            {
                prefab.gameObject.SetActive(true);

                return prefab;
            }

            return ExtendPool();

            T ExtendPool()
            {
                var newPrefabs = new T[_pool.Count + 1];

                for (var i = 0; i < newPrefabs.Length; i++)
                {
                    newPrefabs[i] = Object.Instantiate(_prefab, _parent);
                    newPrefabs[i].gameObject.SetActive(false);
                }

                _pool.AddRange(newPrefabs);

                newPrefabs[0].gameObject.SetActive(true);

                return newPrefabs[0];
            }
        }

        public void ReleaseObject(T released) => released.gameObject.SetActive(false);

        public void Cull()
        {
            foreach (var clutter in Available)
            {
                _pool.Remove(clutter);
#if UNITY_EDITOR
                Object.DestroyImmediate(clutter.gameObject);
#else
                Object.Destroy(clutter.gameObject);
#endif
            }
        }

        public void ReleaseAll()
        {
            foreach (var prefab in InUse)
                ReleaseObject(prefab);
        }
    }
}
