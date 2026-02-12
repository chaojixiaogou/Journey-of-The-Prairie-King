// ShopSystem.cs
using UnityEngine;
using System.Collections; 
using System.Collections.Generic;
using TMPro;

public class ShopSystem : MonoBehaviour
{
    public static ShopSystem Instance;

    [Header("商人")]
    public GameObject merchantPrefab;
    private GameObject merchantInstance;
    private SpriteRenderer merchantRenderer;
    private Vector3 merchantEntryPos = new Vector3(0, 8, 0);   // 入场起点（上方）
    private Vector3 merchantStandPos = new Vector3(0, 3, 0);   // 站立位置

    [Header("商人动画素材")]
    public Sprite idleSprite;      // 静止贴图
    public Sprite walkSprite1;     // 行走帧1
    public Sprite walkSprite2;     // 行走帧2

    [Header("商品预制体")]
    public GameObject bootsItemPrefab;    // 靴子
    public GameObject pistolItemPrefab;   // 手枪
    public GameObject ammoBagItemPrefab;  // 子弹袋
    public Transform[] itemPositions;     // Inspector 拖入三个空物体作为位置

    [Header("背景")]
    public GameObject shopBackdrop;
    private GameObject backdropInstance;

    private bool isActive = false;
    private List<GameObject> spawnedItems = new List<GameObject>();

    [Header("价格标签")]
    public GameObject priceTextPrefab; // 我们会创建一个简单的 TMP 预制体

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 外部调用：开启商店（通常由 GameController 在敌人清空后调用）
    /// </summary>
    public void OpenShop()
    {
        if (isActive) return;
        isActive = true;
        spawnedItems.Clear();
        StartCoroutine(SpawnMerchantAndRevealShop());
    }

    /// <summary>
    /// 协程：商人走入 → 到位 → 显示商店 UI
    /// </summary>
    IEnumerator SpawnMerchantAndRevealShop()
    {
        // 实例化商人
        merchantInstance = Instantiate(merchantPrefab, merchantEntryPos, Quaternion.identity);
        merchantRenderer = merchantInstance.GetComponent<SpriteRenderer>();
        if (merchantRenderer == null)
        {
            Debug.LogError("商人预制体缺少 SpriteRenderer！");
            yield break;
        }

        // 行走参数
        float walkTime = 1.5f;
        float elapsed = 0f;
        float walkAnimInterval = 0.2f;
        float lastSwitchTime = Time.time;
        int frameIndex = 0;

        // 商人向下走（入场）
        while (elapsed < walkTime)
        {
            // 更新位置
            merchantInstance.transform.position = Vector3.Lerp(merchantEntryPos, merchantStandPos, elapsed / walkTime);

            // 行走动画：交替切换贴图
            if (Time.time - lastSwitchTime >= walkAnimInterval)
            {
                frameIndex = 1 - frameIndex;
                merchantRenderer.sprite = (frameIndex == 0) ? walkSprite1 : walkSprite2;
                lastSwitchTime = Time.time;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 到达站立位置
        merchantInstance.transform.position = merchantStandPos;
        merchantRenderer.sprite = idleSprite; // 切换为静止贴图

        // ✅ 同时显示背景和商品
        if (shopBackdrop != null)
        {
            Vector3 backposition = new Vector3(0f, 1f, 0f);
            backdropInstance = Instantiate(shopBackdrop, backposition, Quaternion.identity);
        }

        if (itemPositions != null && itemPositions.Length >= 3)
        {
            CreateShopItem(UpgradeType.Boots, itemPositions[0]);
            CreateShopItem(UpgradeType.Pistol, itemPositions[1]);
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
            // 实例化商品
            GameObject itemObj = Instantiate(prefabToSpawn, pos.position, Quaternion.identity);
            spawnedItems.Add(itemObj);

            // 👇 关键：从 ShopItem 组件获取价格
            ShopItem shopItem = itemObj.GetComponent<ShopItem>();
            if (shopItem == null)
            {
                Debug.LogError("商品预制体缺少 ShopItem 组件！");
                return;
            }

            int price = shopItem.GetNextPrice();
            if (price <= 0) return; // 已满级，不显示价格（或可显示“MAX”）

            // 创建价格标签
            if (priceTextPrefab != null)
            {
                Vector3 pricePos = pos.position + Vector3.down * 0.8f;
                GameObject priceObj = Instantiate(priceTextPrefab, pricePos, Quaternion.identity);
                TextMeshPro tmp = priceObj.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = price.ToString();
                }
                // 可选：设为商品子对象，方便一起销毁
                priceObj.transform.SetParent(itemObj.transform);
            }
        }
        else
        {
            Debug.LogWarning($"未指定 {type} 的商品预制体！");
        }
    }

    /// <summary>
    /// 由 ShopItem 调用：任一商品成功购买后触发
    /// </summary>
    public void OnItemPurchased()
    {
        if (!isActive) return;
        isActive = false;

        // ✅ 立即销毁背景和所有商品
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

        // 启动商人回走动画
        StartCoroutine(MerchantWalkBackAndDestroy());
    }

    /// <summary>
    /// 商人回走到上方并销毁
    /// </summary>
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

        // 商人向上走（离场）
        while (elapsed < walkBackTime)
        {
            merchantInstance.transform.position = Vector3.Lerp(startPos, endPos, elapsed / walkBackTime);

            // 回走时也播放行走动画
            if (Time.time - lastSwitchTime >= walkAnimInterval)
            {
                frameIndex = 1 - frameIndex;
                merchantRenderer.sprite = (frameIndex == 0) ? walkSprite1 : walkSprite2;
                lastSwitchTime = Time.time;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 到达顶部后销毁
        Destroy(merchantInstance);
        merchantInstance = null;
        merchantRenderer = null;
    }
}