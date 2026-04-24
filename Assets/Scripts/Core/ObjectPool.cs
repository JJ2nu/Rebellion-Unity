using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Core
{
    /// <summary>
    /// Generic object pool to recycle GameObjects and avoid runtime allocation.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [System.Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int initialSize;
        }

        [SerializeField] private List<Pool> pools = new List<Pool>();

        private Dictionary<string, Queue<GameObject>> poolDictionary;
        private Dictionary<string, Pool> poolLookup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            poolLookup = new Dictionary<string, Pool>();

            foreach (Pool pool in pools)
                InitializePool(pool);
        }

        private void InitializePool(Pool pool)
        {
            if (poolDictionary.ContainsKey(pool.tag)) return;

            Queue<GameObject> objectQueue = new Queue<GameObject>();
            poolLookup[pool.tag] = pool;

            for (int i = 0; i < pool.initialSize; i++)
            {
                GameObject obj = CreatePooledObject(pool);
                objectQueue.Enqueue(obj);
            }

            poolDictionary[pool.tag] = objectQueue;
        }

        private GameObject CreatePooledObject(Pool pool)
        {
            GameObject obj = Instantiate(pool.prefab, transform);
            obj.SetActive(false);
            return obj;
        }

        public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"[ObjectPool] Pool with tag '{tag}' not found.");
                return null;
            }

            Queue<GameObject> queue = poolDictionary[tag];

            if (queue.Count == 0)
            {
                Pool pool = poolLookup[tag];
                GameObject newObj = CreatePooledObject(pool);
                queue.Enqueue(newObj);
            }

            GameObject obj = queue.Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        public void Despawn(string tag, GameObject obj)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"[ObjectPool] Pool with tag '{tag}' not found. Destroying object instead.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            poolDictionary[tag].Enqueue(obj);
        }
    }
}
