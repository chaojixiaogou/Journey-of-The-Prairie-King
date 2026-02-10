// GameController.cs（完整修改版）
using UnityEngine;
using System.Collections;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("复活设置")]
    public float respawnDelay = 2f;

    [Header("关卡倒计时")]
    public float levelTime = 60f; // 默认60秒通关
    private float currentTime;
    private bool isLevelTimerActive = false;

    // ===== 新增：倒计时事件 =====
    public static System.Action<float, float> OnLevelTimeUpdated;   // (当前时间, 总时间)
    public static System.Action OnLevelTimeFinished;               // 倒计时结束

    private bool isRespawning = false;

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
        }
    }

    void Start()
    {
        StartLevelTimer();
    }

    /// <summary>
    /// 启动关卡倒计时
    /// </summary>
    public void StartLevelTimer()
    {
        currentTime = levelTime;
        isLevelTimerActive = true;
        StartCoroutine(LevelCountdown());
    }

    /// <summary>
    /// 暂停倒计时（可选，比如暂停菜单）
    /// </summary>
    public void PauseLevelTimer()
    {
        isLevelTimerActive = false;
    }

    /// <summary>
    /// 继续倒计时
    /// </summary>
    public void ResumeLevelTimer()
    {
        if (!isLevelTimerActive)
        {
            isLevelTimerActive = true;
            StartCoroutine(LevelCountdown());
        }
    }

    IEnumerator LevelCountdown()
    {
        while (currentTime > 0 && isLevelTimerActive)
        {
            yield return new WaitForSeconds(0.1f); // 高精度更新（每0.1秒）
            currentTime -= 0.1f;
            currentTime = Mathf.Max(0, currentTime);

            // 触发UI更新事件
            OnLevelTimeUpdated?.Invoke(currentTime, levelTime);
        }

        if (currentTime <= 0)
        {
            OnLevelTimeFinished?.Invoke(); // 通知：玩家通关！
            HandleLevelComplete();
        }
    }

    void HandleLevelComplete()
    {
        Debug.Log("🎉 关卡时间到！玩家通关！");
        // TODO: 加载下一关 或 显示胜利界面
        // 例如：
        // UnityEngine.SceneManagement.SceneManager.LoadScene("WinScene");
    }

    public void OnPlayerLoseLife(System.Action onRespawnCallback)
    {
        if (isRespawning) return;
        StartCoroutine(DelayedRespawn(onRespawnCallback));
    }

    IEnumerator DelayedRespawn(System.Action onRespawnCallback)
    {
        isRespawning = true;
        yield return new WaitForSeconds(respawnDelay);
        onRespawnCallback?.Invoke();

        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (var spawner in spawners)
        {
            spawner.Resume();
        }

        isRespawning = false;
    }
}