using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Pools
{
    //TODO: extend prefabPool to support IDisplay<T> that update the Display(newData) before activating the object

    public class PrefabPool<T> : IObjectPool<T> where T : MonoBehaviour
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly List<T> pool = new();

        public PrefabPool(T prefab, Transform parent = null, uint amount = 0)
        {
            if (prefab == null)
                Debug.LogError("Prefab is null! Cant create a pool of nothing...");

            this.prefab = prefab;

            if (parent == null)
                parent = prefab.transform.parent;

            this.parent = parent;

            ExtendPool(amount);
        }

        private IEnumerable<T> Available => pool.Where(x => x != null && !x.gameObject.activeSelf);
        private IEnumerable<T> InUse => pool.Where(x => x != null && x.gameObject.activeSelf);

        public T GetObject(bool activated = true)
        {
            foreach (var prefab in Available)
            {
                prefab.gameObject.SetActive(activated);

                return prefab;
            }

            var newPrefab = ExtendPool();

            newPrefab.gameObject.SetActive(activated);

            return newPrefab;
        }

        private T ExtendPool(uint amount = 0)
        {
            if (amount <= 0)
                amount = (uint)pool.Count + 1;

            var newPrefabs = new T[amount];

            for (var i = 0; i < newPrefabs.Length; i++)
            {
                newPrefabs[i] = Object.Instantiate(prefab, parent);
                newPrefabs[i].gameObject.SetActive(false);
            }

            pool.AddRange(newPrefabs);

            return newPrefabs[0];
        }

        public void ReleaseObject(T released) => released.gameObject.SetActive(false);

        public void Cull()
        {
            foreach (var clutter in Available)
            {
                pool.Remove(clutter);
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

    public interface IObjectPool<T>
    {
        T GetObject(bool activated = true);
        void ReleaseObject(T released);
    }
}
