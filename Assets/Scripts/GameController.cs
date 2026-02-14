// GameController.cs（完整修改版）
using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("复活设置")]
    public float respawnDelay = 2f;

    [Header("关卡倒计时")]
    public float levelTime = 60f; // 默认60秒通关
    private float currentTime;
    private bool isLevelTimerActive = false;

    // 🔥 新增：通关控制
    public Transform player; // Inspector 拖入玩家
    public float mapBottomY = -6f; // 根据你的地图调整
    public GameObject exitArrow; // 向下箭头提示（可选）

    private bool hasClearedAllEnemies = false;
    private bool isRoundCompleted = false;

    [Header("金币系统")]
    private static int totalCoins = 0;
    public static int TotalCoins => totalCoins;

    // ===== 新增：金币变更事件 =====
    public static System.Action OnCoinsChanged;

    // ===== 新增：倒计时事件 =====
    public static System.Action<float, float> OnLevelTimeUpdated;   // (当前时间, 总时间)
    public static System.Action OnLevelTimeFinished;               // 倒计时结束

    public static System.Action OnAllEnemiesDefeated;
    public static System.Action OnLevelComplete;

    private bool isRespawning = false;
    
    // ===== 持久化玩家状态（跨关卡）=====
    public int persistentLives = 3; // 初始3条命
    public PowerupType? persistentHeldPowerup = null;

    // 👇 新增：商店升级的持久化数据
    public int bootsUpgradeLevel = 0;      // 靴子等级（0=初始）
    public int pistolUpgradeLevel = 0;     // 手枪射速等级
    public int ammoBagUpgradeLevel = 0;    // 弹药袋等级（0~2）

    [Header("Exit Arrow 设置")]
    public GameObject exitArrowPrefab; // 拖入你的 ExitArrow.prefab
    private GameObject spawnedExitArrow; // 动态生成的实例

    public static System.Action OnShopRequested;

    [Header("开始界面")]
    // public GameObject gameBeginCanvas; // 在 Inspector 拖入你的 GameBeginCanvas

    public bool hasGameStarted = false;

    public static bool HasGameStarted => Instance?.hasGameStarted == true;

    private const string GAME_BEGIN_CANVAS_NAME = "GameBeginCanvas";
    [Header("开始界面 Prefabs")]
    public GameObject gameBeginCanvasPrefab; // 拖入你的 GameBeginCanvas.prefab
    private GameObject currentGameBeginCanvasInstance; // 当前实例的引用

    public bool canDetectIsReachBottom = false;

    public bool isStopBGM = false;
    

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 🔑 关键修复：强制初始化持久化状态为默认值
            persistentLives = 3;
            persistentHeldPowerup = null;

            // 👇 新增：监听场景加载
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // 可选：如果你有“继续游戏”功能，可以用 PlayerPrefs 判断是否加载存档
            // 否则每次都从默认状态开始
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    if (scene.buildIndex == 0)
    {
        hasGameStarted = false;

        // 👇 销毁旧实例（安全起见）
        if (currentGameBeginCanvasInstance != null)
        {
            Destroy(currentGameBeginCanvasInstance);
        }

        // 👇 实例化新的开始界面
        if (gameBeginCanvasPrefab != null)
        {
            currentGameBeginCanvasInstance = Instantiate(gameBeginCanvasPrefab);
            currentGameBeginCanvasInstance.SetActive(true);

            // 确保它是 UI（通常 prefab 已设置为 Overlay，但可加日志）
            Debug.Log("✅ 实例化 GameBeginCanvas Prefab");
        }
        else
        {
            Debug.LogError("❌ GameBeginCanvas Prefab 未指定！");
            StartGame(); // 容错
        }

        // 暂停敌人生成器
        foreach (var spawner in FindObjectsOfType<EnemySpawner>())
            spawner.Pause();
    }
}

    void Start()
    {
        HideExitArrow();
        // 显示开始界面
        if (gameBeginCanvasPrefab != null)
        {
            gameBeginCanvasPrefab.SetActive(true);
            hasGameStarted = false;
        }
        else
        {
            // 如果没指定 Canvas，则直接开始（开发容错）
            StartGame();
        }
    }

    public void StartGame()
    {
        Debug.Log("🚀 StartGame() 被调用！");
        if (hasGameStarted) return;

        hasGameStarted = true;

        // 👇 隐藏并销毁开始界面
        if (currentGameBeginCanvasInstance != null)
        {
            Destroy(currentGameBeginCanvasInstance); // 或 SetActive(false)，但 Destroy 更干净
            currentGameBeginCanvasInstance = null;
        }

        // 👇 恢复当前场景中所有已存在的 EnemySpawner
        var spawners = FindObjectsOfType<EnemySpawner>();
        foreach (var spawner in spawners)
        {
            spawner.Resume();
        }

        // 启动关卡逻辑
        StartLevelTimer();

        Debug.Log("🎮 游戏正式开始！");
    }

    /// <summary>
    /// 彻底重置游戏状态，用于“重新开始”
    /// </summary>
    public void ResetForNewGame()
    {
        // 重置金币
        totalCoins = 0;
        OnCoinsChanged?.Invoke(); // 通知 UI 更新
    
        // 重置生命和道具
        persistentLives = 3;
        persistentHeldPowerup = null;
    
        // 重置商店升级
        bootsUpgradeLevel = 0;
        pistolUpgradeLevel = 0;
        ammoBagUpgradeLevel = 0;
    
        // 重置其他状态（按需添加）
        hasClearedAllEnemies = false;
        isRoundCompleted = false;
        isLevelTimerActive = false;
    
        // 如果有更多全局状态，继续重置...
    
        Debug.Log("🔄 游戏状态已重置为新游戏");
    }

    void Update()
    {
        if (!hasGameStarted && Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
        }
    }

    /// <summary>
    /// 启动关卡倒计时
    /// </summary>
    private Coroutine countdownCoroutine;

    public void StartLevelTimer()
    {
        if (!hasGameStarted) return; // 👈 新增保护
        // 停止旧协程
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }

        currentTime = levelTime;
        isLevelTimerActive = true;
        countdownCoroutine = StartCoroutine(LevelCountdown());
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

    /// <summary>
    /// 增加关卡剩余时间（例如：受到伤害但未死亡时奖励时间）
    /// </summary>
    public void AddTime(float seconds)
    {
        if (!hasGameStarted || !isLevelTimerActive) return; // 👈 新增保护
        if (!isLevelTimerActive) return;

        // 🔑 关键：增加后不能超过 levelTime
        currentTime = Mathf.Min(currentTime + seconds, levelTime);

        // 立即触发 UI 更新
        OnLevelTimeUpdated?.Invoke(currentTime, levelTime);

        Debug.Log($"⏳ 倒计时增加 {seconds} 秒，当前剩余: {currentTime:F1}s");
    }

    /// <summary>
    /// 增加金币（由 CoinPickup 调用）
    /// </summary>
    public static void AddCoins(int amount)
    {
        totalCoins += amount;
        OnCoinsChanged?.Invoke();
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
            HandleRoundEnd();
        }
    }

    void HandleRoundEnd()
    {
        isLevelTimerActive = false;
        foreach (var spawner in FindObjectsOfType<EnemySpawner>())
            spawner.StopSpawning(); // ⚠️ 确保 EnemySpawner 有这个方法

        // ShowExitArrow();
        StartCoroutine(CheckEnemiesClearance());
    }

    IEnumerator CheckEnemiesClearance()
    {
        while (!hasClearedAllEnemies)
        {
            yield return new WaitForSeconds(0.3f);

            // 只统计 active 且 enabled 的敌人
            var enemies = FindObjectsOfType<Enemy>();
            bool foundAlive = false;
            foreach (var e in enemies)
            {
                if (e != null && e.gameObject.activeInHierarchy)
                {
                    foundAlive = true;
                    break;
                }
            }

            if (!foundAlive)
            {
                hasClearedAllEnemies = true;
                isRoundCompleted = true;
                OnAllEnemiesDefeated?.Invoke();
                Debug.Log("✅ 所有敌人已清除！");

                ShowExitArrow();
                canDetectIsReachBottom = true;

                // ✅ 第二步：如果是商店关卡，同时打开商店
                int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                if (LevelManager.Instance != null && 
                    LevelManager.Instance.shopLevelIndices != null &&
                    System.Array.IndexOf(LevelManager.Instance.shopLevelIndices, currentSceneIndex) >= 0)
                {
                    // 直接调用，不再通过事件（避免耦合）
                    ShopSystem.Instance?.OpenShop();
                }
            }
        }
    }

    public void OnPlayerReachBottom()
    {
        if (isRoundCompleted)
            OnLevelComplete?.Invoke();
    }

    public void ShowExitArrow()
    {
        isStopBGM = true;
        Debug.Log($"🔍 ShowExitArrow() 被调用，堆栈：\n{System.Environment.StackTrace}");
        if (spawnedExitArrow != null)
        {
            spawnedExitArrow.SetActive(true);
        }
        else
        {
            // 如果还没生成，现在生成并显示
            SpawnExitArrowIfNeeded();
            if (spawnedExitArrow != null)
                spawnedExitArrow.SetActive(true);
        }
    }

    public void HideExitArrow()
    {
        if (spawnedExitArrow != null)
        {
            spawnedExitArrow.SetActive(false);
        }
    }

    /// <summary>
    /// 在当前关卡底部生成 ExitArrow（如果尚未生成）
    /// </summary>
    public void SpawnExitArrowIfNeeded()
    {
        if (spawnedExitArrow != null) return; // 已存在，不重复生成

        if (exitArrowPrefab == null)
        {
            Debug.LogError("❌ ExitArrow Prefab 未指定！");
            return;
        }

        // 计算生成位置：屏幕底部中央（世界坐标）
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("⚠️ 未找到主相机，使用默认位置");
            spawnedExitArrow = Instantiate(exitArrowPrefab, new Vector3(0, mapBottomY, 0), Quaternion.identity);
        }
        else
        {
            // 将屏幕底部中央转换为世界坐标
            Vector3 screenBottomCenter = new Vector3(Screen.width / 2f, 250f, mainCam.nearClipPlane); // 略高于底部
            Vector3 worldPos = mainCam.ScreenToWorldPoint(screenBottomCenter);
            worldPos.z = 0; // 2D 游戏通常 z=0
            spawnedExitArrow = Instantiate(exitArrowPrefab, worldPos, Quaternion.identity);
        }

        spawnedExitArrow.SetActive(false); // 初始隐藏
        Debug.Log($"✅ 动态生成 ExitArrow at {spawnedExitArrow.transform.position}");
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

    public void ResetLevelState()
    {
        hasClearedAllEnemies = false;
        isRoundCompleted = false;
        isLevelTimerActive = false;

        // ✅ 新增：重置玩家的关卡触发标志
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.hasTriggeredNextLevel = false;
        }

        // 停止任何旧协程
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        HideExitArrow(); // 确保隐藏
    }

    /// <summary>
    /// 跳过倒计时，直接进入“敌人清除后通关”模式（用于 Boss 关卡）
    /// </summary>
    public void StartBossLevel()
    {
        // 确保倒计时不运行
        isLevelTimerActive = false;
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
    
        currentTime = 0; // 视为时间已耗尽
    
        // 👇 直接触发“时间结束”后的流程（即等待所有敌人被消灭）
        HandleRoundEnd();
    
        Debug.Log("🎮 当前为 Boss 关卡，倒计时已禁用，等待击败所有敌人...");
    }
}