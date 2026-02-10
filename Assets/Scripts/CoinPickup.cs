using UnityEngine;
using System.Collections;

public class CoinPickup : MonoBehaviour
{
    [Header("金币属性")]
    public int coinValue = 1; // 1 或 5

    [Header("自动消失设置")]
    public float totalLifetime = 10f;      // 总存活时间：10秒
    public float blinkDuration = 2f;       // 闪烁持续时间：最后2秒
    public float blinkInterval = 0.1f;     // 闪烁间隔（越小越快）

    private SpriteRenderer spriteRenderer;
    private Coroutine destroyCoroutine;


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning("[CoinPickup] 未找到 SpriteRenderer，无法闪烁！", this);
        }

        // 启动自动销毁协程
        destroyCoroutine = StartCoroutine(AutoDestroyWithBlink());
    }

    IEnumerator AutoDestroyWithBlink()
    {
        // 先正常显示 (totalLifetime - blinkDuration) 秒
        yield return new WaitForSeconds(totalLifetime - blinkDuration);

        // 开始闪烁
        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            while (elapsed < blinkDuration)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
                elapsed += blinkInterval;
                yield return new WaitForSeconds(blinkInterval);
            }
            // 确保最后一帧可见（可选）
            spriteRenderer.enabled = true;
        }

        // 时间到，销毁
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameController.AddCoins(coinValue);
            // AudioManager.Play("CoinPickup"); // 可选

            // ✅ 通知玩家“我被拾取了”，由玩家决定播什么音效
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OnPickupCollectible(CollectibleType.Coin); // 👈 新增方法
            }

            // 立即销毁，取消闪烁
            if (destroyCoroutine != null)
                StopCoroutine(destroyCoroutine);
            Destroy(gameObject);
        }
    }
}