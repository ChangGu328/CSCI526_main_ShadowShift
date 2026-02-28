using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RetryLogger : MonoBehaviour
{
    [Header("Firebase Settings")]
    [Tooltip("Realtime Database base URL, e.g. https://your-project-id-default-rtdb.firebaseio.com/")]
    public string firebaseUrl = "https://your-project-id-default-rtdb.firebaseio.com/";

    [Tooltip("Web API Key (from Firebase Project Settings → Web API Key)")]
    public string apiKey = "";

    [Header("Runtime Settings")]
    [Tooltip("Listen for R key via new Input System (set false if not using Input System)")]
    public bool autoListenForR = true;

    // PlayerPrefs keys
    private const string PREF_UID = "FB_UID";
    private const string PREF_REFRESH = "FB_REFRESH";
    private const string PREF_IDTOKEN = "FB_IDTOKEN";
    private const string PREF_TOKEN_EXP_MS = "FB_TOKEN_EXP_MS";

    // Singleton
    public static RetryLogger Instance { get; private set; }

    // runtime auth state
    private string uid; // localId
    private string idToken;
    private string refreshToken;
    private long tokenExpiryMs = 0; // epoch ms when idToken expires (approx)

    // session id for this run
    private string sessionId;

    // in-memory queue of events to send
    private Queue<RetryEvent> retryQueue = new Queue<RetryEvent>();
    private bool isSending = false;

    // InputAction for new input system
    private InputAction retryAction;

    // debounce / duplicate prevention
    private long lastRetryTimestampMs = 0;
    private const int DEBOUNCE_MS = 150;

    [Serializable]
    private class RetryEvent
    {
        public string levelId;
        public long timestamp;
        public RetryEvent(string levelId)
        {
            this.levelId = levelId;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    // Response classes for parsing JSON
    [Serializable]
    private class SignUpResponse
    {
        public string idToken; // JWT
        public string refreshToken;
        public string expiresIn; // seconds as string
        public string localId; // uid
    }

    [Serializable]
    private class RefreshResponse
    {
        public string id_token;
        public string refresh_token;
        public string expires_in; // seconds
        public string user_id; // localId
    }

    private void Awake()
    {
        // singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        CreateNewSessionId();
        LoadStoredAuth();
    }

    private void Start()
    {
        // Try refresh if refreshToken exists; otherwise sign up anonymously
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

    private void OnEnable()
    {
        if (autoListenForR)
        {
            SetupRetryInputAction();
            retryAction?.Enable();
        }
    }

    private void OnDisable()
    {
        if (retryAction != null)
        {
            retryAction.performed -= OnRetryPerformed;
            retryAction.Disable();
            retryAction.Dispose();
            retryAction = null;
        }
    }

    private void SetupRetryInputAction()
    {
        if (retryAction != null) return;
        retryAction = new InputAction("Retry", InputActionType.Button, "<Keyboard>/r");
        retryAction.performed += OnRetryPerformed;
        Debug.Log("[RetryLogger] InputAction set up.");
    }

    private void OnRetryPerformed(InputAction.CallbackContext ctx)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastRetryTimestampMs < DEBOUNCE_MS)
        {
            Debug.Log("[RetryLogger] Debounced duplicate input.");
            return;
        }
        lastRetryTimestampMs = now;

        RegisterRetry(GetCurrentLevelId());
    }

    private void Update()
    {
        if (!isSending && retryQueue.Count > 0)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    // -------------------------------
    // Public API
    // -------------------------------
    
    // Public method to register a retry event (call from your reset logic).
    public void RegisterRetry(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogWarning("[RetryLogger] Empty levelId, skipping.");
            return;
        }

        // Prevent enqueue from non-singleton instance
        if (Instance != this)
        {
            Debug.LogWarning("[RetryLogger] RegisterRetry called on non-singleton instance. Ignored.");
            return;
        }

        var evt = new RetryEvent(levelId);
        retryQueue.Enqueue(evt);
        Debug.Log($"[RetryLogger] Enqueued retry for level {levelId} ts={evt.timestamp} (queue size {retryQueue.Count})");

        if (!isSending)
            StartCoroutine(ProcessQueueCoroutine());
    }

    // -------------------------------
    // Session / Auth persistence
    // -------------------------------

    private void CreateNewSessionId()
    {
        sessionId = Guid.NewGuid().ToString();
        Debug.Log($"[RetryLogger] New session created: {sessionId}");
    }

    private void LoadStoredAuth()
    {
        uid = PlayerPrefs.GetString(PREF_UID, "");
        refreshToken = PlayerPrefs.GetString(PREF_REFRESH, "");
        idToken = PlayerPrefs.GetString(PREF_IDTOKEN, "");
        tokenExpiryMs = long.TryParse(PlayerPrefs.GetString(PREF_TOKEN_EXP_MS, "0"), out long v) ? v : 0;

        if (!string.IsNullOrEmpty(uid))
            Debug.Log($"[RetryLogger] Loaded stored uid: {uid}");
    }

    private void SaveAuth(string localId, string newIdToken, string newRefreshToken, long expiresInSeconds)
    {
        uid = localId;
        idToken = newIdToken;
        refreshToken = newRefreshToken;
        tokenExpiryMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresInSeconds * 1000) - (60 * 1000); // expire 60s earlier as buffer

        PlayerPrefs.SetString(PREF_UID, uid);
        PlayerPrefs.SetString(PREF_IDTOKEN, idToken);
        PlayerPrefs.SetString(PREF_REFRESH, refreshToken);
        PlayerPrefs.SetString(PREF_TOKEN_EXP_MS, tokenExpiryMs.ToString());
        PlayerPrefs.Save();

        Debug.Log($"[RetryLogger] Saved auth uid={uid} tokenExp={tokenExpiryMs}");
    }
    
    // Force create a new anonymous session (new sessionId and new anonymous account).
    // Call if you want a fresh user instead of reusing stored anonymous account.
    public void StartNewSession()
    {
        CreateNewSessionId();
        // optionally clear stored auth if you want new anonymous id next time:
        // PlayerPrefs.DeleteKey(PREF_UID); PlayerPrefs.DeleteKey(PREF_REFRESH);
    }

    // -------------------------------
    // REST Auth: sign up & refresh
    // -------------------------------

    private IEnumerator SignInAnonymouslyCoroutine()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[RetryLogger] apiKey not set in Inspector.");
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
                Debug.LogError($"[RetryLogger] Anonymous sign-up failed: {req.error} raw={req.downloadHandler.text}");
                yield break;
            }

            string json = req.downloadHandler.text;
            SignUpResponse resp = null;
            try { resp = JsonUtility.FromJson<SignUpResponse>(json); } catch (Exception e) { Debug.LogError("[RetryLogger] Parse signUp response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.localId))
            {
                long expires = 0;
                long.TryParse(resp.expiresIn, out expires);
                SaveAuth(resp.localId, resp.idToken, resp.refreshToken, expires);
                Debug.Log("[RetryLogger] Anonymous sign-in success uid=" + resp.localId);
            }
            else
            {
                Debug.LogError("[RetryLogger] SignUp response invalid: " + json);
            }
        }
    }

    private IEnumerator RefreshIdTokenCoroutine(string refreshTokenParam, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[RetryLogger] apiKey not set in Inspector.");
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
                Debug.LogWarning($"[RetryLogger] Token refresh failed: {req.error} raw={req.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string json = req.downloadHandler.text;
            RefreshResponse resp = null;
            try { resp = JsonUtility.FromJson<RefreshResponse>(json); } catch (Exception e) { Debug.LogError("[RetryLogger] Parse refresh response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.id_token))
            {
                long expires = 0;
                long.TryParse(resp.expires_in, out expires);
                SaveAuth(resp.user_id, resp.id_token, resp.refresh_token, expires);
                Debug.Log("[RetryLogger] Token refreshed uid=" + resp.user_id);
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("[RetryLogger] Refresh response invalid: " + json);
                onComplete?.Invoke(false);
            }
        }
    }

    // Ensure idToken valid (refresh if near expiry or missing)
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

    // -------------------------------
    // Queue processing & POST event
    // -------------------------------

    private IEnumerator ProcessQueueCoroutine()
    {
        isSending = true;
        while (retryQueue.Count > 0)
        {
            var evt = retryQueue.Peek();

            // ensure idToken valid
            bool ready = false;
            yield return StartCoroutine(EnsureValidIdTokenCoroutine(success => ready = success));
            if (!ready)
            {
                Debug.LogWarning("[RetryLogger] Auth not ready; will retry later.");
                yield break;
            }

            yield return StartCoroutine(PostRetryEventCoroutine(evt));
            // slight delay
            yield return new WaitForSeconds(0.05f);
        }
        isSending = false;
    }

    // POST each event under: /analytics/retries/{levelId}/{uid}/{sessionId}/events.json?auth={idToken}
    private IEnumerator PostRetryEventCoroutine(RetryEvent evt)
    {
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[RetryLogger] uid missing; cannot post. Will retry after auth.");
            yield break;
        }

        string path = $"analytics/retries/{UnityWebRequest.EscapeURL(evt.levelId)}/{UnityWebRequest.EscapeURL(uid)}/{UnityWebRequest.EscapeURL(sessionId)}/events.json";
        string url = CombineUrl(firebaseUrl, path);

        // ensure idToken available
        string tokenToUse = idToken;
        if (string.IsNullOrEmpty(tokenToUse))
        {
            Debug.LogWarning("[RetryLogger] idToken empty when posting; skipping for now.");
            yield break;
        }
        url += $"?auth={UnityWebRequest.EscapeURL(tokenToUse)}";

        string jsonBody = $"{{\"timestamp\":{evt.timestamp}}}";
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
                Debug.LogWarning($"[RetryLogger] POST failed: {req.error} raw={req.downloadHandler.text}");
                // keep in queue for later retry
                yield break;
            }
            else
            {
                Debug.Log($"[RetryLogger] Posted event for level {evt.levelId} ts={evt.timestamp}");
                retryQueue.Dequeue();
            }
        }
    }

    private string CombineUrl(string baseUrl, string path)
    {
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return baseUrl + path;
    }

    private string GetCurrentLevelId()
    {
        try { return SceneManager.GetActiveScene().name; }
        catch { return "unknown_level"; }
    }
}