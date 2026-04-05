using System;
using System.IO;
using UnityEngine;

public static class FirebaseAnalyticsConfig
{
    public const string DefaultApiKeyEnvironmentVariable = "SHADOWSHIFT_FIREBASE_API_KEY";
    public const string DefaultSecretsFileName = "FirebaseAnalyticsSecrets.json";
    private const string LegacySecretsFileName = "RetryLoggerSecrets.json";
    private const string WebGlResourcesConfigPath = "FirebaseAnalyticsPublicConfig";
    private static bool hasLoadedSecrets;
    private static string cachedApiKey;
    private static string cachedFirebaseUrl;

    public struct RuntimeConfig
    {
        public string FirebaseUrl { get; }
        public string ApiKey { get; }

        public RuntimeConfig(string firebaseUrl, string apiKey)
        {
            FirebaseUrl = firebaseUrl;
            ApiKey = apiKey;
        }
    }

    [Serializable]
    private class SecretsFile
    {
        public string firebaseApiKey;
        public string firebaseUrl;
    }

    public static bool TryLoad(string fallbackFirebaseUrl, out RuntimeConfig config, string logPrefix = "FirebaseAnalytics")
    {
        EnsureSecretsLoaded(logPrefix);

        if (string.IsNullOrWhiteSpace(cachedApiKey))
        {
            config = new RuntimeConfig(fallbackFirebaseUrl, null);
            return false;
        }

        string firebaseUrl = string.IsNullOrWhiteSpace(cachedFirebaseUrl) ? fallbackFirebaseUrl : cachedFirebaseUrl;
        config = new RuntimeConfig(firebaseUrl, cachedApiKey);
        return true;
    }

    private static void EnsureSecretsLoaded(string logPrefix)
    {
        if (hasLoadedSecrets)
            return;

        hasLoadedSecrets = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (TryLoadResourcesConfig(out SecretsFile webGlConfig))
        {
            if (!string.IsNullOrWhiteSpace(webGlConfig.firebaseApiKey))
            {
                Debug.Log($"[{logPrefix}] Loaded API key from Resources/{WebGlResourcesConfigPath}.");
                cachedApiKey = webGlConfig.firebaseApiKey.Trim();
                cachedFirebaseUrl = string.IsNullOrWhiteSpace(webGlConfig.firebaseUrl) ? null : webGlConfig.firebaseUrl.Trim();
                return;
            }

            Debug.LogError($"[{logPrefix}] Resources/{WebGlResourcesConfigPath} is missing 'firebaseApiKey'.");
            return;
        }
#endif

        string envApiKey = Environment.GetEnvironmentVariable(DefaultApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            Debug.Log($"[{logPrefix}] Loaded API key from environment variable '{DefaultApiKeyEnvironmentVariable}'.");
            cachedApiKey = envApiKey.Trim();
            return;
        }

        if (TryLoadSecretsFile(out SecretsFile secrets, out string pathUsed))
        {
            if (!string.IsNullOrWhiteSpace(secrets.firebaseApiKey))
            {
                Debug.Log($"[{logPrefix}] Loaded API key from local file: {pathUsed}");
                cachedApiKey = secrets.firebaseApiKey.Trim();
                cachedFirebaseUrl = string.IsNullOrWhiteSpace(secrets.firebaseUrl) ? null : secrets.firebaseUrl.Trim();
                return;
            }

            Debug.LogError($"[{logPrefix}] Secret file missing 'firebaseApiKey': {pathUsed}");
            return;
        }

        Debug.LogError(
            $"[{logPrefix}] Firebase Web API Key not found. Set environment variable '{DefaultApiKeyEnvironmentVariable}' " +
            $"or create '{GetProjectSecretsPath(DefaultSecretsFileName)}' / '{GetPersistentSecretsPath(DefaultSecretsFileName)}'."
        );
    }

    private static bool TryLoadResourcesConfig(out SecretsFile secrets)
    {
        secrets = null;

        TextAsset textAsset = Resources.Load<TextAsset>(WebGlResourcesConfigPath);
        if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            return false;

        try
        {
            secrets = JsonUtility.FromJson<SecretsFile>(textAsset.text);
            return secrets != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAnalytics] Failed to parse Resources/{WebGlResourcesConfigPath}: {e}");
            return false;
        }
    }

    private static bool TryLoadSecretsFile(out SecretsFile secrets, out string pathUsed)
    {
        foreach (string fileName in GetSecretsFileNames())
        {
            string projectPath = GetProjectSecretsPath(fileName);
            if (TryReadSecretsFile(projectPath, out secrets))
            {
                pathUsed = projectPath;
                return true;
            }

            string persistentPath = GetPersistentSecretsPath(fileName);
            if (TryReadSecretsFile(persistentPath, out secrets))
            {
                pathUsed = persistentPath;
                return true;
            }
        }

        secrets = null;
        pathUsed = null;
        return false;
    }

    private static bool TryReadSecretsFile(string filePath, out SecretsFile secrets)
    {
        secrets = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            secrets = JsonUtility.FromJson<SecretsFile>(json);
            return secrets != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAnalytics] Failed to read secret file '{filePath}': {e}");
            return false;
        }
    }

    private static string[] GetSecretsFileNames()
    {
        return new[] { DefaultSecretsFileName, LegacySecretsFileName };
    }

    private static string GetProjectSecretsPath(string fileName)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, fileName);
    }

    private static string GetPersistentSecretsPath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }
}
