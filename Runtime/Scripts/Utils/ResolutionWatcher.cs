using System;
using UnityEngine;

namespace Machamy.Utils
{
    public class ResolutionWatcher : MonoBehaviour
    {
        private static ResolutionWatcher _instance;

        public static ResolutionWatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var obj = new GameObject("ResolutionWatcher");
                    _instance = obj.AddComponent<ResolutionWatcher>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        public Vector2Int CurrentResolution { get; private set; }
        public event System.Action<Vector2Int> OnResolutionChanged;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            CurrentResolution = new Vector2Int(Screen.width, Screen.height);
        }

        private void Update()
        {
            if (Screen.width != CurrentResolution.x || Screen.height != CurrentResolution.y)
            {
                CurrentResolution = new Vector2Int(Screen.width, Screen.height);
                OnResolutionChanged?.Invoke(CurrentResolution);
            }
        }
    }
}