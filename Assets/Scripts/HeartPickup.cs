// HeartPickup.cs
using UnityEngine;
using System.Collections;

public class HeartPickup : MonoBehaviour
{
    [Header("自动消失设置")]
    public float totalLifetime = 10f;      // 存活10秒
    public float blinkDuration = 2f;       // 最后2秒闪烁
    public float blinkInterval = 0.1f;     // 闪烁速度

    private SpriteRenderer spriteRenderer;
    private Coroutine destroyCoroutine;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        destroyCoroutine = StartCoroutine(AutoDestroyWithBlink());
    }

    IEnumerator AutoDestroyWithBlink()
    {
        yield return new WaitForSeconds(totalLifetime - blinkDuration);

        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            while (elapsed < blinkDuration)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
                elapsed += blinkInterval;
                yield return new WaitForSeconds(blinkInterval);
            }
            spriteRenderer.enabled = true;
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // ===== 回血逻辑 =====
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.Heal(1); // 恢复1点生命
            }

            // 销毁道具
            if (destroyCoroutine != null)
                StopCoroutine(destroyCoroutine);
            Destroy(gameObject);
        }
    }
}