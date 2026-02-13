// LevelManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("黑屏过渡")]
    public float fadeDuration = 1.0f;
    private Image fadeImage; // 👈 新增字段

    [Header("地图重置（单场景复用模式）")]
    public bool useSingleScene = false;

    [Header("关卡配置")]
    public int[] bossLevelIndices; // 在 Inspector 中填写哪些 Build Index 是 Boss 关卡（从 0 开始）

    // 示例：如果你的 Boss 在第 2 关和第 5 关（Build Settings 中索引为 1 和 4），就填 [1, 4]

    [Header("商店设置")]
    public int[] shopLevelIndices; // 在 Inspector 填写哪些关卡后出现商店（Build Index）

    [Header("=== 地图切换配置 ===")]
    public GameObject originalMap;   // 拖入 Grid
    public GameObject newMap;        // 拖入 Grid_new

    // === Game Win UI ===
    [Header("Game Win")]
    public GameObject winCanvas; // 拖入你的 Canvas

    // 🔥 不再需要 public GameObject fadePanel;
    private GameObject fadePanel;
    private CanvasGroup fadeCanvasGroup;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadePanel(); // 👈 自动创建黑屏面板
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void CreateFadePanel()
    {
        // 1. 创建 Canvas
        GameObject canvasObj = new GameObject("GlobalFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 确保在最顶层

        // 可选：添加 CanvasScaler 适配不同分辨率
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>(); // 防止阻挡 UI 交互（过渡期可忽略）

        // 2. 创建全屏 Image（初始为黑色，兼容原有黑屏逻辑）
        GameObject imageObj = new GameObject("FadePanel");
        imageObj.transform.SetParent(canvasObj.transform, false);

        // 👇 关键：保存 Image 引用到成员变量
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = Color.black; // 初始为黑色

        // 拉满全屏
        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 3. 添加 CanvasGroup 并初始化
        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f; // 初始透明

        // 4. 防止被场景切换销毁
        DontDestroyOnLoad(canvasObj);

        // 5. 保存引用（fadePanel 可选，但建议保留）
        fadePanel = canvasObj;
    }

    /// <summary>
    /// 执行白色闪光过渡：淡入白 → 全白时执行 action → 淡出
    /// </summary>
    public void StartWhiteFlashTransition(System.Action onFullWhite = null)
    {
        StartCoroutine(WhiteFlashRoutine(onFullWhite));
    }

    IEnumerator WhiteFlashRoutine(System.Action onFullWhite = null)
    {
        // 1. 切换为白色
        fadeImage.color = Color.white;

        // 2. 淡入到全白
        yield return FadeTo(1f, fadeDuration);

        // 3. 全白瞬间：执行地图切换等操作
        onFullWhite?.Invoke();

        // 4. 淡出恢复
        yield return FadeTo(0f, fadeDuration);

        // 5. （可选）恢复为黑色，避免影响后续黑屏过渡
        fadeImage.color = Color.black;
    }

    public void ResetOrChangeTilemap()
    {
        // 查找两个地图对象（建议通过名字或标签，这里用名字）
        GameObject oldGrid = GameObject.Find("Grid");
        GameObject newGrid = GameObject.Find("Grid_new");
    
        if (oldGrid != null)
        {
            oldGrid.SetActive(false);
            Debug.Log("🗺️ 原地图 'Grid' 已停用。");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到名为 'Grid' 的地图对象！");
        }
    
        if (newGrid != null)
        {
            newGrid.SetActive(true);
            Debug.Log("🗺️ 新地图 'Grid_new' 已激活。");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到名为 'Grid_new' 的地图对象！");
        }
    }

    void Start()
    {
        Debug.Log("🔷 LevelManager 启动，订阅 OnLevelComplete");
        GameController.OnLevelComplete += StartLevelTransition;
    }

    void OnDestroy()
    {
        GameController.OnLevelComplete -= StartLevelTransition;
    }

    public void StartLevelTransition()
    {
        StartCoroutine(LevelTransitionRoutine());
    }

    IEnumerator LevelTransitionRoutine()
    {
        yield return FadeTo(1f, fadeDuration);

        if (useSingleScene)
        {
            ResetCurrentLevel();
        }
        else
        {
            LoadNextScene();

            // ⏳ 等待新场景完全加载
            yield return new WaitForSeconds(0.1f); // 或使用 SceneManager.sceneLoaded 事件（更严谨）
        }

        yield return FadeTo(0f, fadeDuration);

        // 👇 新增：重新绑定 exitArrow
        GameController.Instance.SpawnExitArrowIfNeeded();

        // ✅ 新逻辑：根据关卡类型决定
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

        bool isBossLevel = System.Array.IndexOf(bossLevelIndices, currentSceneIndex) >= 0;

        if (isBossLevel)
        {
            GameController.Instance.StartBossLevel(); // 无倒计时
        }
        else
        {
            GameController.Instance.StartLevelTimer(); // 正常倒计时
        }
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }

    // 🔁 重置当前场景（适用于程序生成或动态地图）
    void ResetCurrentLevel()
    {
        Debug.Log("🔄 重置当前关卡...");

        var enemies = FindObjectsOfType<Enemy>();
        foreach (var enemy in enemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }

        var coins = FindObjectsOfType<CoinPickup>();
        foreach (var coin in coins)
        {
            if (coin != null) Destroy(coin.gameObject);
        }

        var hearts = FindObjectsOfType<HeartPickup>();
        foreach (var heart in hearts)
        {
            if (heart != null) Destroy(heart.gameObject);
        }
    }

    // ➕ 加载下一场景（多场景模式）
    void LoadNextScene()
    {
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex >= UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("🏆 已完成所有关卡！");
            // 显示 Game Win UI
            if (winCanvas != null)
            {
                // 实例化 winCanvas 预制体
                Instantiate(winCanvas);
            }
            else
            {
                Debug.LogError("winCanvas 未赋值！");
            }
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }
}