using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMController : MonoBehaviour
{
    // 单例（可选）
    public static BGMController Instance { get; private set; }

    [System.Serializable]
    public class SceneBGM
    {
        public string sceneName;
        public AudioClip bgmClip;
    }

    [Header("场景与BGM映射")]
    public SceneBGM[] sceneBGMs;

    [Header("音量设置")]
    [Range(0f, 1f)] public float volume = 0.7f;

    private AudioSource audioSource;
    private string currentSceneName;
    private bool isPlaying = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        // 获取状态
        bool hasGameStarted = GameController.Instance ? GameController.Instance.hasGameStarted : true;
        bool isStopBGM = GameController.Instance ? GameController.Instance.isStopBGM : false;
        bool isGameOver = PlayerController.Instance ? PlayerController.Instance.isGameOver : false;

        // 判断是否应该停止 BGM
        bool shouldStop = !hasGameStarted || isGameOver;

        if (shouldStop)
        {
            if (isPlaying)
            {
                audioSource.Stop();
                isPlaying = false;
            }
        }
        else
        {
            // 如果当前场景有 BGM 且未播放，则播放
            if (!isPlaying && currentSceneName != null)
            {
                AudioClip clip = GetBGMForScene(currentSceneName);
                if (clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    isPlaying = true;
                }
            }
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        isPlaying = false; // 等待 Update 决定是否播放
    }

    AudioClip GetBGMForScene(string sceneName)
    {
        foreach (var item in sceneBGMs)
        {
            if (item.sceneName == sceneName)
                return item.bgmClip;
        }
        return null; // 该场景无 BGM
    }
}