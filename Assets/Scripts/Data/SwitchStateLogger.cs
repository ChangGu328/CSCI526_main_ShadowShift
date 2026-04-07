using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class SwitchStateLogger : MonoBehaviour
{
    [Header("Firebase Settings")]
    [Tooltip("Realtime Database base URL, e.g. https://your-project-id-default-rtdb.firebaseio.com/")]
    public string firebaseUrl = "https://your-project-id-default-rtdb.firebaseio.com/";

    [Header("Runtime Settings")]
    [Tooltip("Listen for Q key via new Input System")]
    public bool autoListenForQ = true;

    private const string PREF_UID = "FB_UID";
    private const string PREF_REFRESH = "FB_REFRESH";
    private const string PREF_IDTOKEN = "FB_IDTOKEN";
    private const string PREF_TOKEN_EXP_MS = "FB_TOKEN_EXP_MS";

    public static SwitchStateLogger Instance { get; private set; }

    private string apiKey;
    private string uid;
    private string idToken;
    private string refreshToken;
    private long tokenExpiryMs;
    private string sessionId;

    private readonly Queue<SwitchStateEvent> switchQueue = new Queue<SwitchStateEvent>();
    private bool isSending;

    private InputAction switchAction;
    private long lastSwitchTimestampMs;
    private const int DEBOUNCE_MS = 150;

    [Serializable]
    private class SwitchStateEvent
    {
        public string levelId;
        public long timestamp;
        public PositionData position;

        public SwitchStateEvent(string levelId, Vector3 position)
        {
            this.levelId = levelId;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            this.position = new PositionData(position);
        }
    }

    [Serializable]
    private class PositionData
    {
        public float x;
        public float y;

        public PositionData(Vector3 position)
        {
            x = position.x;
            y = position.y;
        }
    }

    [Serializable]
    private class SwitchStateEventPayload
    {
        public long timestamp;
        public PositionData position;

        public SwitchStateEventPayload(SwitchStateEvent evt)
        {
            timestamp = evt.timestamp;
            position = evt.position;
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

        if (FirebaseAnalyticsConfig.TryLoad(firebaseUrl, out FirebaseAnalyticsConfig.RuntimeConfig config, nameof(SwitchStateLogger)))
        {
            firebaseUrl = config.FirebaseUrl;
            apiKey = config.ApiKey;
        }

        CreateNewSessionId();
        LoadStoredAuth();
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[SwitchStateLogger] Firebase Web API Key not configured; switch analytics disabled.");
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

    private void OnEnable()
    {
        if (!autoListenForQ)
        {
            return;
        }

        SetupSwitchInputAction();
        switchAction?.Enable();
    }

    private void OnDisable()
    {
        if (switchAction == null)
        {
            return;
        }

        switchAction.performed -= OnSwitchPerformed;
        switchAction.Disable();
        switchAction.Dispose();
        switchAction = null;
    }

    private void Update()
    {
        if (!isSending && switchQueue.Count > 0)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    public void RegisterSwitchState(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogWarning("[SwitchStateLogger] Empty levelId, skipping.");
            return;
        }

        if (Instance != this)
        {
            Debug.LogWarning("[SwitchStateLogger] RegisterSwitchState called on non-singleton instance. Ignored.");
            return;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[SwitchStateLogger] Firebase Web API Key not configured; switch event skipped.");
            return;
        }

        var evt = new SwitchStateEvent(levelId, GetCurrentPlayerPosition());
        switchQueue.Enqueue(evt);
        Debug.Log($"[SwitchStateLogger] Enqueued switch for level {levelId} ts={evt.timestamp} pos=({evt.position.x}, {evt.position.y}) (queue size {switchQueue.Count})");

        if (!isSending)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    private void SetupSwitchInputAction()
    {
        if (switchAction != null)
        {
            return;
        }

        switchAction = new InputAction("SwitchState", InputActionType.Button, "<Keyboard>/q");
        switchAction.performed += OnSwitchPerformed;
        Debug.Log("[SwitchStateLogger] InputAction set up.");
    }

    private void OnSwitchPerformed(InputAction.CallbackContext ctx)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastSwitchTimestampMs < DEBOUNCE_MS)
        {
            Debug.Log("[SwitchStateLogger] Debounced duplicate input.");
            return;
        }

        lastSwitchTimestampMs = now;
        RegisterSwitchState(GetCurrentLevelId());
    }

    private void CreateNewSessionId()
    {
        sessionId = Guid.NewGuid().ToString();
        Debug.Log($"[SwitchStateLogger] New session created: {sessionId}");
    }

    private void LoadStoredAuth()
    {
        uid = PlayerPrefs.GetString(PREF_UID, "");
        refreshToken = PlayerPrefs.GetString(PREF_REFRESH, "");
        idToken = PlayerPrefs.GetString(PREF_IDTOKEN, "");
        tokenExpiryMs = long.TryParse(PlayerPrefs.GetString(PREF_TOKEN_EXP_MS, "0"), out long value) ? value : 0;

        if (!string.IsNullOrEmpty(uid))
        {
            Debug.Log($"[SwitchStateLogger] Loaded stored uid: {uid}");
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

        Debug.Log($"[SwitchStateLogger] Saved auth uid={uid} tokenExp={tokenExpiryMs}");
    }

    private IEnumerator SignInAnonymouslyCoroutine()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[SwitchStateLogger] Firebase Web API Key not configured.");
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
                Debug.LogError($"[SwitchStateLogger] Anonymous sign-up failed: {req.error} raw={req.downloadHandler.text}");
                yield break;
            }

            string json = req.downloadHandler.text;
            SignUpResponse resp = null;
            try { resp = JsonUtility.FromJson<SignUpResponse>(json); } catch (Exception e) { Debug.LogError("[SwitchStateLogger] Parse signUp response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.localId))
            {
                long expires = 0;
                long.TryParse(resp.expiresIn, out expires);
                SaveAuth(resp.localId, resp.idToken, resp.refreshToken, expires);
                Debug.Log("[SwitchStateLogger] Anonymous sign-in success uid=" + resp.localId);
            }
            else
            {
                Debug.LogError("[SwitchStateLogger] SignUp response invalid: " + json);
            }
        }
    }

    private IEnumerator RefreshIdTokenCoroutine(string refreshTokenParam, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[SwitchStateLogger] Firebase Web API Key not configured.");
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
                Debug.LogWarning($"[SwitchStateLogger] Token refresh failed: {req.error} raw={req.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string json = req.downloadHandler.text;
            RefreshResponse resp = null;
            try { resp = JsonUtility.FromJson<RefreshResponse>(json); } catch (Exception e) { Debug.LogError("[SwitchStateLogger] Parse refresh response error: " + e); }

            if (resp != null && !string.IsNullOrEmpty(resp.id_token))
            {
                long expires = 0;
                long.TryParse(resp.expires_in, out expires);
                SaveAuth(resp.user_id, resp.id_token, resp.refresh_token, expires);
                Debug.Log("[SwitchStateLogger] Token refreshed uid=" + resp.user_id);
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("[SwitchStateLogger] Refresh response invalid: " + json);
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
        while (switchQueue.Count > 0)
        {
            var evt = switchQueue.Peek();

            bool ready = false;
            yield return StartCoroutine(EnsureValidIdTokenCoroutine(success => ready = success));
            if (!ready)
            {
                Debug.LogWarning("[SwitchStateLogger] Auth not ready; will retry later.");
                isSending = false;
                yield break;
            }

            yield return StartCoroutine(PostSwitchStateEventCoroutine(evt));
            yield return new WaitForSeconds(0.05f);
        }

        isSending = false;
    }

    private IEnumerator PostSwitchStateEventCoroutine(SwitchStateEvent evt)
    {
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[SwitchStateLogger] uid missing; cannot post. Will retry after auth.");
            yield break;
        }

        string path = $"analytics/switches/{UnityWebRequest.EscapeURL(evt.levelId)}/{UnityWebRequest.EscapeURL(uid)}/{UnityWebRequest.EscapeURL(sessionId)}/events.json";
        string url = CombineUrl(firebaseUrl, path);

        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogWarning("[SwitchStateLogger] idToken empty when posting; skipping for now.");
            yield break;
        }

        url += $"?auth={UnityWebRequest.EscapeURL(idToken)}";

        string jsonBody = JsonUtility.ToJson(new SwitchStateEventPayload(evt));
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
                Debug.LogWarning($"[SwitchStateLogger] POST failed: {req.error} raw={req.downloadHandler.text}");
                yield break;
            }

            Debug.Log($"[SwitchStateLogger] Posted event for level {evt.levelId} ts={evt.timestamp}");
            switchQueue.Dequeue();
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

    private Vector3 GetCurrentPlayerPosition()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            Transform activeTransform = playerController.currentState == PLAYERSTATE.BODY
                ? playerController.body?.transform
                : playerController.soul?.transform;

            if (activeTransform != null)
            {
                return activeTransform.position;
            }
        }

        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null)
        {
            return playerMove.transform.position;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer.transform.position;
        }

        Debug.LogWarning("[SwitchStateLogger] Player position not found; using Vector3.zero.");
        return Vector3.zero;
    }
}
