using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Utils
{
    /// <summary>
    /// Simple singleton base class. Inherit from this to create a MonoBehaviour singleton.
    /// Usage: public class MyManager : Singleton&lt;MyManager&gt; { }
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<T>();
                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
