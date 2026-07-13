using UnityEngine;

namespace SHIN
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    if (_instance == null)
                    {
                        Debug.LogError($"[Singleton] {typeof(T).Name} 인스턴스를 찾을 수 없습니다.");
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// true면 씬 전환 시에도 유지됩니다. GameManager 등에서 override하세요.
        /// </summary>
        protected virtual bool PersistAcrossScenes => false;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;

            if (PersistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

}

