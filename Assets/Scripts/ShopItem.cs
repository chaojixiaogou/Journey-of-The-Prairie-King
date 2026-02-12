// ShopItem.cs
using UnityEngine;
using UnityEngine.UI;

public enum UpgradeType
{
    Boots,
    Pistol,
    AmmoBag
}

[System.Serializable]
public class UpgradeLevel
{
    public Sprite icon;
    public int price;
    public string description;
}

public class ShopItem : MonoBehaviour
{
    public UpgradeType type;
    public int currentLevel = 0; // 0 = 未购买，1/2/3 = 已升级次数
    public UpgradeLevel[] levels; // 靴子2级，手枪3级，子弹袋3级

    private SpriteRenderer spriteRenderer;
    private Text priceText; // 可选：显示价格的 UI（也可用 Tooltip）

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // UpdateVisual();
    }

    void Start()
    {
        LoadCurrentLevelFromGameController();
        UpdateVisual();
    }

    void LoadCurrentLevelFromGameController()
    {
        if (GameController.Instance == null)
        {
            currentLevel = 0;
            return;
        }

        switch (type)
        {
            case UpgradeType.Boots:
                currentLevel = GameController.Instance.bootsUpgradeLevel;
                break;
            case UpgradeType.Pistol:
                currentLevel = GameController.Instance.pistolUpgradeLevel;
                break;
            case UpgradeType.AmmoBag:
                currentLevel = GameController.Instance.ammoBagUpgradeLevel;
                break;
            default:
                currentLevel = 0;
                break;
        }

        // 安全限制：防止存档等级超出配置
        currentLevel = Mathf.Clamp(currentLevel, 0, levels.Length);
    }


    public void UpdateVisual()
    {
        if (currentLevel < levels.Length && levels[currentLevel] != null)
        {
            if (spriteRenderer != null && levels[currentLevel].icon != null)
                spriteRenderer.sprite = levels[currentLevel].icon;
            // 可在此更新价格UI（如果用了Canvas）
        }
    }

    public bool CanPurchase()
    {
        return currentLevel < levels.Length;
    }

    public int GetPrice()
    {
        return currentLevel < levels.Length ? levels[currentLevel].price : -1;
    }

    public void Purchase()
    {
        if (!CanPurchase()) return;

        int price = levels[currentLevel].price;
        if (GameController.TotalCoins >= price)
        {
            GameController.AddCoins(-price);
            currentLevel++;
            ApplyEffect();

            UpdateVisual();

            // 👇 关键：通知商店系统“已购买”，触发关闭
            ShopSystem.Instance?.OnItemPurchased();

            // 可选：禁用自身碰撞防止重复触发
            GetComponent<Collider2D>().enabled = false;

            // // 如果已满级，可隐藏或变灰（可选）
            // if (!CanPurchase())
            // {
            //     gameObject.SetActive(false); // 或变灰
            // }
        }
    }

    // ShopItem.cs
    public int GetNextPrice()
    {
        if (currentLevel < levels.Length)
            return levels[currentLevel].price;
        else
            return -1; // 表示已满级（商店不应显示，但安全处理）
    }

    /// <summary>
    /// 核心：应用效果 + 同步到 GameController
    /// </summary>
    public void ApplyEffect()
    {
        // 1. 同步等级到 GameController（持久化）
        if (GameController.Instance != null)
        {
            switch (type)
            {
                case UpgradeType.Boots:
                    GameController.Instance.bootsUpgradeLevel = currentLevel;
                    Debug.Log("购买了靴子");
                    break;
                case UpgradeType.Pistol:
                    GameController.Instance.pistolUpgradeLevel = currentLevel;
                    Debug.Log("购买了手枪");
                    break;
                case UpgradeType.AmmoBag:
                    GameController.Instance.ammoBagUpgradeLevel = currentLevel;
                    Debug.Log("购买了子弹袋");
                    break;
            }
        }

        // 2. 通知 PlayerController 重新计算属性（可选，或由 Player 自己读取）
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.RecalculateStatsFromUpgrades();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(" OnTrigger: 触碰对象 = " + other.name); // 👈 新增

        if (other.CompareTag("Player") && CanPurchase())
        {
            Debug.Log("✅ 满足购买条件，尝试购买"); // 👈 新增
            Purchase();
        }
        else
        {
            Debug.Log("❌ 不满足条件：Tag=" + other.tag + ", 可购买=" + CanPurchase());
        }
    }
}