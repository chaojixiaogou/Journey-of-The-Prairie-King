// ShopSystem.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class ShopSystem : MonoBehaviour
{
    public static ShopSystem Instance;

    [Header("商人")]
    public GameObject merchantPrefab;
    private GameObject merchantInstance;
    private SpriteRenderer merchantRenderer;
    private Vector3 merchantEntryPos = new Vector3(0, 8, 0);
    private Vector3 merchantStandPos = new Vector3(0, 3, 0);

    [Header("商人动画素材")]
    public Sprite idleSprite;
    public Sprite walkSprite1;
    public Sprite walkSprite2;

    [Header("商品预制体")]
    public GameObject bootsItemPrefab;
    public GameObject pistolItemPrefab;
    public GameObject ammoBagItemPrefab;
    public Transform[] itemPositions;

    [Header("背景")]
    public GameObject shopBackdrop;
    private GameObject backdropInstance;

    private bool isActive = false;
    private List<GameObject> spawnedItems = new List<GameObject>();

    [Header("价格标签")]
    public GameObject priceTextPrefab;

    // 👇 新增：左下角升级图标 UI
    [Header("左下角升级图标 UI")]
    public Canvas gameCanvas; // 拖入主 Canvas
    private Image bootsIconUI;
    private Image pistolIconUI;
    private Image ammoBagIconUI;
    private GameObject upgradeDisplayPanel;

    // ====== 可配置参数 ======
    private Vector2 panelOffset = new Vector2(140f, 320f);   // 面板距离左下角的偏移 (x, y)
    private Vector2 iconSize = new Vector2(48f, 48f);      // 每个图标的宽高
    private float iconSpacing = 16f;                       // 图标之间的垂直间距

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 不再 DontDestroyOnLoad
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 初始化左下角 UI（只创建一次）
        InitializeUpgradeDisplayUI();
        // 首次刷新图标（加载存档状态）
        RefreshUpgradeIcons();
    }

    /// <summary>
    /// 创建左下角升级图标面板（运行时动态生成）
    /// </summary>
    void InitializeUpgradeDisplayUI()
    {
        if (gameCanvas == null)
        {
            Debug.LogError("ShopSystem: 未指定 GameCanvas！");
            return;
        }

        // 创建父面板
        upgradeDisplayPanel = new GameObject("UpgradeDisplayPanel");
        upgradeDisplayPanel.transform.SetParent(gameCanvas.transform, false);
        upgradeDisplayPanel.AddComponent<RectTransform>();
        var rect = upgradeDisplayPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;      // 锚点：左下
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = panelOffset; // 👈 使用自定义偏移
        rect.sizeDelta = Vector2.zero;

        // 纵向排列：从上到下（Boots → Pistol → AmmoBag）
        // 第一个图标 y = 0，第二个 y = -(size.y + spacing)，依此类推
        float yPos = 0f;
        bootsIconUI = CreateIconImage("BootsIcon", new Vector2(0, yPos));

        yPos -= (iconSize.y + iconSpacing);
        pistolIconUI = CreateIconImage("PistolIcon", new Vector2(0, yPos));

        yPos -= (iconSize.y + iconSpacing);
        ammoBagIconUI = CreateIconImage("AmmoBagIcon", new Vector2(0, yPos));

        HideAllIcons();
    }

    Image CreateIconImage(string name, Vector2 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(upgradeDisplayPanel.transform, false);
        Image img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = iconSize;           // 👈 使用自定义尺寸
        rt.anchoredPosition = localPos;    // 局部位置（相对于面板）

        return img;
    }

    void HideAllIcons()
    {
        if (bootsIconUI) bootsIconUI.gameObject.SetActive(false);
        if (pistolIconUI) pistolIconUI.gameObject.SetActive(false);
        if (ammoBagIconUI) ammoBagIconUI.gameObject.SetActive(false);
    }

    /// <summary>
    /// 公共方法：刷新左下角所有图标（可在购买后调用）
    /// </summary>
    public void RefreshUpgradeIcons()
    {
        if (GameController.Instance == null) return;

        UpdateIcon(bootsIconUI, GameController.Instance.bootsUpgradeLevel, bootsItemPrefab);
        UpdateIcon(pistolIconUI, GameController.Instance.pistolUpgradeLevel, pistolItemPrefab);
        UpdateIcon(ammoBagIconUI, GameController.Instance.ammoBagUpgradeLevel, ammoBagItemPrefab);
    }

    void UpdateIcon(Image image, int level, GameObject prefab)
    {
        if (image == null || prefab == null) return;

        if (level <= 0)
        {
            image.gameObject.SetActive(false);
            return;
        }

        ShopItem item = prefab.GetComponent<ShopItem>();
        if (item == null || item.levels == null) return;

        int index = level - 1; // level=1 → index=0
        if (index >= 0 && index < item.levels.Length && item.levels[index].icon != null)
        {
            image.sprite = item.levels[index].icon;
            image.gameObject.SetActive(true);
        }
        else
        {
            image.gameObject.SetActive(false);
        }
    }

    // ========== 原有商店逻辑保持不变 ==========

    public void OpenShop()
    {
        if (isActive) return;
        isActive = true;
        spawnedItems.Clear();
        StartCoroutine(SpawnMerchantAndRevealShop());
    }

    IEnumerator SpawnMerchantAndRevealShop()
    {
        merchantInstance = Instantiate(merchantPrefab, merchantEntryPos, Quaternion.identity);
        merchantRenderer = merchantInstance.GetComponent<SpriteRenderer>();
        if (merchantRenderer == null)
        {
            Debug.LogError("商人预制体缺少 SpriteRenderer！");
            yield break;
        }

        float walkTime = 1.5f;
        float elapsed = 0f;
        float walkAnimInterval = 0.2f;
        float lastSwitchTime = Time.time;
        int frameIndex = 0;

        while (elapsed < walkTime)
        {
            merchantInstance.transform.position = Vector3.Lerp(merchantEntryPos, merchantStandPos, elapsed / walkTime);
            if (Time.time - lastSwitchTime >= walkAnimInterval)
            {
                frameIndex = 1 - frameIndex;
                merchantRenderer.sprite = (frameIndex == 0) ? walkSprite1 : walkSprite2;
                lastSwitchTime = Time.time;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        merchantInstance.transform.position = merchantStandPos;
        merchantRenderer.sprite = idleSprite;

        if (shopBackdrop != null)
        {
            Vector3 backposition = new Vector3(0f, 1f, 0f);
            backdropInstance = Instantiate(shopBackdrop, backposition, Quaternion.identity);
        }

        if (itemPositions != null && itemPositions.Length >= 3)
        {
            if (GameController.Instance.bootsUpgradeLevel < 2)
                CreateShopItem(UpgradeType.Boots, itemPositions[0]);
            if (GameController.Instance.pistolUpgradeLevel < 3)
                CreateShopItem(UpgradeType.Pistol, itemPositions[1]);
            if (GameController.Instance.ammoBagUpgradeLevel < 3)
                CreateShopItem(UpgradeType.AmmoBag, itemPositions[2]);
        }
    }

    void CreateShopItem(UpgradeType type, Transform pos)
    {
        GameObject prefabToSpawn = null;
        switch (type)
        {
            case UpgradeType.Boots: prefabToSpawn = bootsItemPrefab; break;
            case UpgradeType.Pistol: prefabToSpawn = pistolItemPrefab; break;
            case UpgradeType.AmmoBag: prefabToSpawn = ammoBagItemPrefab; break;
        }

        if (prefabToSpawn != null)
        {
            GameObject itemObj = Instantiate(prefabToSpawn, pos.position, Quaternion.identity);
            spawnedItems.Add(itemObj);

            ShopItem shopItem = itemObj.GetComponent<ShopItem>();
            if (shopItem == null)
            {
                Debug.LogError("商品预制体缺少 ShopItem 组件！");
                return;
            }

            int price = shopItem.GetNextPrice();
            if (price <= 0) return;

            if (priceTextPrefab != null)
            {
                Vector3 pricePos = pos.position + Vector3.down * 0.8f;
                GameObject priceObj = Instantiate(priceTextPrefab, pricePos, Quaternion.identity);
                TextMeshPro tmp = priceObj.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = price.ToString();
                }
                priceObj.transform.SetParent(itemObj.transform);
            }
        }
        else
        {
            Debug.LogWarning($"未指定 {type} 的商品预制体！");
        }
    }

    public void OnItemPurchased()
    {
        if (!isActive) return;
        isActive = false;

        if (backdropInstance != null)
        {
            Destroy(backdropInstance);
            backdropInstance = null;
        }

        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();

        StartCoroutine(MerchantWalkBackAndDestroy());

        // 👇 关键：购买后刷新左下角图标
        RefreshUpgradeIcons();
    }

    IEnumerator MerchantWalkBackAndDestroy()
    {
        if (merchantInstance == null || merchantRenderer == null) yield break;

        Vector3 startPos = merchantStandPos;
        Vector3 endPos = merchantEntryPos;
        float walkBackTime = 1.5f;
        float elapsed = 0f;
        float walkAnimInterval = 0.2f;
        float lastSwitchTime = Time.time;
        int frameIndex = 0;

        while (elapsed < walkBackTime)
        {
            merchantInstance.transform.position = Vector3.Lerp(startPos, endPos, elapsed / walkBackTime);
            if (Time.time - lastSwitchTime >= walkAnimInterval)
            {
                frameIndex = 1 - frameIndex;
                merchantRenderer.sprite = (frameIndex == 0) ? walkSprite1 : walkSprite2;
                lastSwitchTime = Time.time;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(merchantInstance);
        merchantInstance = null;
        merchantRenderer = null;
    }
}