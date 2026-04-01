using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LevelCompletionTimeLogger : MonoBehaviour
{
    [Header("Firebase Settings")]
    [Tooltip("Realtime Database base URL, e.g. https://your-project-id-default-rtdb.firebaseio.com/")]
    public string firebaseUrl = "https://your-project-id-default-rtdb.firebaseio.com/";

    private const string PREF_UID = "FB_UID";
    private const string PREF_REFRESH = "FB_REFRESH";
    private const string PREF_IDTOKEN = "FB_IDTOKEN";
    private const string PREF_TOKEN_EXP_MS = "FB_TOKEN_EXP_MS";

    public static LevelCompletionTimeLogger Instance { get; private set; }

    private string apiKey;
    private string uid;
    private string idToken;
    private string refreshToken;
    private long tokenExpiryMs;
    private string sessionId;
    private long levelStartTimestampMs;
    private Terminal[] trackedTerminals = Array.Empty<Terminal>();
    private bool completionLoggedThisScene;

    private readonly Queue<LevelCompletionEvent> completionQueue = new Queue<LevelCompletionEvent>();
    private bool isSending;

    [Serializable]
    private class LevelCompletionEvent
    {
        public string levelId;
        public string category;
        public long elapsedMs;
        public long timestamp;

        public LevelCompletionEvent(string levelId, string category, long elapsedMs)
        {
            this.levelId = levelId;
            this.category = category;
            this.elapsedMs = elapsedMs;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    [Serializable]
    private class SignUpResponse
    {
        public string idToken;
        public string refreshToken;
        public string expiresIn;
        public string localId;
    }

    [Serializable]
    private class RefreshResponse
    {
        public string id_token;
        public string refresh_token;
        public string expires_in;
        public string user_id;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        if (FirebaseAnalyticsConfig.TryLoad(firebaseUrl, out FirebaseAnalyticsConfig.RuntimeConfig config, nameof(LevelCompletionTimeLogger)))
        {
            firebaseUrl = config.FirebaseUrl;
            apiKey = config.ApiKey;
        }

        CreateNewSessionId();
        LoadStoredAuth();
        ResetLevelStartTime();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] Firebase Web API Key not configured; completion analytics disabled.");
            return;
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            StartCoroutine(RefreshIdTokenCoroutine(refreshToken, success =>
            {
                if (!success)
                {
                    StartCoroutine(SignInAnonymouslyCoroutine());
                }
            }));
        }
        else
        {
            StartCoroutine(SignInAnonymouslyCoroutine());
        }
    }

    private void Update()
    {
        TryDetectLevelCompletion();

        if (!isSending && completionQueue.Count > 0)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    public void RegisterLevelCompletion(bool allCollected)
    {
        string levelId = GetCurrentLevelId();
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] Empty levelId, skipping.");
            return;
        }

        if (Instance != this)
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] RegisterLevelCompletion called on non-singleton instance. Ignored.");
            return;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] Firebase Web API Key not configured; completion event skipped.");
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long elapsedMs = Math.Max(0L, now - levelStartTimestampMs);
        string category = allCollected ? "all-collected" : "missing-collectibles";

        var evt = new LevelCompletionEvent(levelId, category, elapsedMs);
        completionQueue.Enqueue(evt);
        Debug.Log($"[LevelCompletionTimeLogger] Enqueued completion for level {levelId} category={category} elapsedMs={elapsedMs} (queue size {completionQueue.Count})");

        if (!isSending)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetLevelStartTime();
        RefreshTrackedTerminals();
        completionLoggedThisScene = false;
    }

    private void ResetLevelStartTime()
    {
        levelStartTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Debug.Log($"[LevelCompletionTimeLogger] Level timer reset for {GetCurrentLevelId()} at {levelStartTimestampMs}");
    }

    private void CreateNewSessionId()
    {
        sessionId = Guid.NewGuid().ToString();
        Debug.Log($"[LevelCompletionTimeLogger] New session created: {sessionId}");
    }

    private void LoadStoredAuth()
    {
        uid = PlayerPrefs.GetString(PREF_UID, "");
        refreshToken = PlayerPrefs.GetString(PREF_REFRESH, "");
        idToken = PlayerPrefs.GetString(PREF_IDTOKEN, "");
        tokenExpiryMs = long.TryParse(PlayerPrefs.GetString(PREF_TOKEN_EXP_MS, "0"), out long value) ? value : 0;

        if (!string.IsNullOrEmpty(uid))
        {
            Debug.Log($"[LevelCompletionTimeLogger] Loaded stored uid: {uid}");
        }
    }

    private void RefreshTrackedTerminals()
    {
        trackedTerminals = FindObjectsOfType<Terminal>(true);
    }

    private void TryDetectLevelCompletion()
    {
        if (completionLoggedThisScene)
        {
            return;
        }

        if (trackedTerminals == null || trackedTerminals.Length == 0)
        {
            RefreshTrackedTerminals();
            if (trackedTerminals == null || trackedTerminals.Length == 0)
            {
                return;
            }
        }

        for (int i = 0; i < trackedTerminals.Length; i++)
        {
            Terminal terminal = trackedTerminals[i];
            if (terminal == null || !terminal.IsGameOver)
            {
                continue;
            }

            bool allCollected = CollectibleManager.IsInitialized && CollectibleManager.Instance.IsAllCollected();
            RegisterLevelCompletion(allCollected);
            completionLoggedThisScene = true;
            return;
        }
    }

    private void SaveAuth(string localId, string newIdToken, string newRefreshToken, long expiresInSeconds)
    {
        uid = localId;
        idToken = newIdToken;
        refreshToken = newRefreshToken;
        tokenExpiryMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresInSeconds * 1000) - (60 * 1000);

        PlayerPrefs.SetString(PREF_UID, uid);
        PlayerPrefs.SetString(PREF_IDTOKEN, idToken);
        PlayerPrefs.SetString(PREF_REFRESH, refreshToken);
        PlayerPrefs.SetString(PREF_TOKEN_EXP_MS, tokenExpiryMs.ToString());
        PlayerPrefs.Save();

        Debug.Log($"[LevelCompletionTimeLogger] Saved auth uid={uid} tokenExp={tokenExpiryMs}");
    }

    private IEnumerator SignInAnonymouslyCoroutine()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[LevelCompletionTimeLogger] Firebase Web API Key not configured.");
            yield break;
        }

        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";
        string body = "{\"returnSecureToken\": true}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool fail = req.result != UnityWebRequest.Result.Success;
#else
            bool fail = req.isNetworkError || req.isHttpError;
#endif
            if (fail)
            {
                Debug.LogError($"[LevelCompletionTimeLogger] Anonymous sign-up failed: {req.error} raw={req.downloadHandler.text}");
                yield break;
            }

            string json = req.downloadHandler.text;
            SignUpResponse resp = null;
            try { resp = JsonUtility.FromJson<SignUpResponse>(json); } catch (Exception e) { Debug.LogError("[LevelCompletionTimeLogger] Parse signUp response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.localId))
            {
                long expires = 0;
                long.TryParse(resp.expiresIn, out expires);
                SaveAuth(resp.localId, resp.idToken, resp.refreshToken, expires);
                Debug.Log("[LevelCompletionTimeLogger] Anonymous sign-in success uid=" + resp.localId);
            }
            else
            {
                Debug.LogError("[LevelCompletionTimeLogger] SignUp response invalid: " + json);
            }
        }
    }

    private IEnumerator RefreshIdTokenCoroutine(string refreshTokenParam, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[LevelCompletionTimeLogger] Firebase Web API Key not configured.");
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"https://securetoken.googleapis.com/v1/token?key={apiKey}";
        string body = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshTokenParam)}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            req.timeout = 10;

            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool fail = req.result != UnityWebRequest.Result.Success;
#else
            bool fail = req.isNetworkError || req.isHttpError;
#endif
            if (fail)
            {
                Debug.LogWarning($"[LevelCompletionTimeLogger] Token refresh failed: {req.error} raw={req.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string json = req.downloadHandler.text;
            RefreshResponse resp = null;
            try { resp = JsonUtility.FromJson<RefreshResponse>(json); } catch (Exception e) { Debug.LogError("[LevelCompletionTimeLogger] Parse refresh response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.id_token))
            {
                long expires = 0;
                long.TryParse(resp.expires_in, out expires);
                SaveAuth(resp.user_id, resp.id_token, resp.refresh_token, expires);
                Debug.Log("[LevelCompletionTimeLogger] Token refreshed uid=" + resp.user_id);
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("[LevelCompletionTimeLogger] Refresh response invalid: " + json);
                onComplete?.Invoke(false);
            }
        }
    }

    private IEnumerator EnsureValidIdTokenCoroutine(Action<bool> onReady)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!string.IsNullOrEmpty(idToken) && tokenExpiryMs > now + 5000)
        {
            onReady?.Invoke(true);
            yield break;
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            yield return StartCoroutine(RefreshIdTokenCoroutine(refreshToken, success => onReady?.Invoke(success)));
        }
        else
        {
            yield return StartCoroutine(SignInAnonymouslyCoroutine());
            onReady?.Invoke(!string.IsNullOrEmpty(idToken));
        }
    }

    private IEnumerator ProcessQueueCoroutine()
    {
        isSending = true;
        while (completionQueue.Count > 0)
        {
            var evt = completionQueue.Peek();

            bool ready = false;
            yield return StartCoroutine(EnsureValidIdTokenCoroutine(success => ready = success));
            if (!ready)
            {
                Debug.LogWarning("[LevelCompletionTimeLogger] Auth not ready; will retry later.");
                isSending = false;
                yield break;
            }

            yield return StartCoroutine(PostCompletionEventCoroutine(evt));
            yield return new WaitForSeconds(0.05f);
        }

        isSending = false;
    }

    private IEnumerator PostCompletionEventCoroutine(LevelCompletionEvent evt)
    {
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] uid missing; cannot post. Will retry after auth.");
            yield break;
        }

        string path = $"analytics/completion-times/{UnityWebRequest.EscapeURL(evt.category)}/{UnityWebRequest.EscapeURL(evt.levelId)}/{UnityWebRequest.EscapeURL(uid)}/{UnityWebRequest.EscapeURL(sessionId)}/events.json";
        string url = CombineUrl(firebaseUrl, path);

        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogWarning("[LevelCompletionTimeLogger] idToken empty when posting; skipping for now.");
            yield break;
        }

        url += $"?auth={UnityWebRequest.EscapeURL(idToken)}";

        string jsonBody = $"{{\"timestamp\":{evt.timestamp},\"elapsedMs\":{evt.elapsedMs}}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool fail = req.result != UnityWebRequest.Result.Success;
#else
            bool fail = req.isNetworkError || req.isHttpError;
#endif
            if (fail)
            {
                Debug.LogWarning($"[LevelCompletionTimeLogger] POST failed: {req.error} raw={req.downloadHandler.text}");
                yield break;
            }

            Debug.Log($"[LevelCompletionTimeLogger] Posted completion for level {evt.levelId} category={evt.category} elapsedMs={evt.elapsedMs}");
            completionQueue.Dequeue();
        }
    }

    private string CombineUrl(string baseUrl, string path)
    {
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }

        return baseUrl + path;
    }

    private string GetCurrentLevelId()
    {
        try { return SceneManager.GetActiveScene().name; }
        catch { return "unknown_level"; }
    }
}
