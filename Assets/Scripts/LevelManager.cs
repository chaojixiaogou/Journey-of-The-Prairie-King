// LevelManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("黑屏过渡")]
    public float fadeDuration = 1.0f;

    [Header("地图重置（单场景复用模式）")]
    public bool useSingleScene = false;

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

        // 2. 创建全屏黑色 Image
        GameObject imageObj = new GameObject("FadePanel");
        imageObj.transform.SetParent(canvasObj.transform, false);
        Image image = imageObj.AddComponent<Image>();
        image.color = Color.black;

        // 拉满全屏
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 3. 添加 CanvasGroup 并初始化
        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f; // 初始透明

        // 4. 关键：防止被场景切换销毁！
        DontDestroyOnLoad(canvasObj);

        // 5. 保存引用
        fadePanel = canvasObj;
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

        GameController.Instance.StartLevelTimer();
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
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }
}