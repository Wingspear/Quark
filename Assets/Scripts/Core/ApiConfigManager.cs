using OpenAI;
using UnityEngine;

namespace Jusvibes.Core
{
    /// <summary>
    /// Centralized singleton for managing API configurations (OpenAI, Suno).
    /// Only this class should hold references to the config ScriptableObjects.
    /// </summary>
    public class ApiConfigManager : MonoBehaviour
    {
        private static ApiConfigManager _instance;

        public static ApiConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ApiConfigManager>();

                    if (_instance == null)
                    {
                        var go = new GameObject("ApiConfigManager");
                        _instance = go.AddComponent<ApiConfigManager>();
                        DontDestroyOnLoad(go);
                    }
                }

                return _instance;
            }
        }

        [Header("API Configurations")]
        [SerializeField] private OpenAIConfiguration openAIConfiguration;
        [SerializeField] private SunoConfig sunoConfiguration;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ValidateConfigurations();
        }

        private void ValidateConfigurations()
        {
            if (openAIConfiguration == null)
            {
                Debug.LogError("[ApiConfigManager] OpenAIConfiguration is not assigned! Please assign it in the Inspector.");
            }

            if (sunoConfiguration == null)
            {
                Debug.LogError("[ApiConfigManager] SunoConfig is not assigned! Please assign it in the Inspector.");
            }
        }

        // OpenAI Configuration Access
        public OpenAIConfiguration GetOpenAIConfig()
        {
            if (openAIConfiguration == null)
            {
                throw new System.Exception("OpenAIConfiguration is not set in ApiConfigManager");
            }
            return openAIConfiguration;
        }

        public OpenAIClient CreateOpenAIClient()
        {
            var config = GetOpenAIConfig();
            return new OpenAIClient(new OpenAIAuthentication(config), new OpenAISettings(config));
        }

        // Suno Configuration Access
        public SunoConfig GetSunoConfig()
        {
            if (sunoConfiguration == null)
            {
                throw new System.Exception("SunoConfig is not set in ApiConfigManager");
            }
            return sunoConfiguration;
        }

        public string GetSunoApiKey()
        {
            return GetSunoConfig().sunoApiKey;
        }

        // Optional: Runtime config updates (for testing or switching accounts)
        public void SetOpenAIConfig(OpenAIConfiguration config)
        {
            openAIConfiguration = config;
            Debug.Log("[ApiConfigManager] OpenAI configuration updated");
        }

        public void SetSunoConfig(SunoConfig config)
        {
            sunoConfiguration = config;
            Debug.Log("[ApiConfigManager] Suno configuration updated");
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Configurations")]
        private void ValidateConfigurationsEditor()
        {
            ValidateConfigurations();

            if (openAIConfiguration != null)
            {
                Debug.Log("✅ OpenAI config found");
            }

            if (sunoConfiguration != null)
            {
                Debug.Log("✅ Suno config found");
            }
        }
#endif
    }
}
