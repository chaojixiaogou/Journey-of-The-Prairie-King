// PowerupItem.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class PowerupItem : MonoBehaviour
{
    [Header("道具类型")]
    public PowerupType type;

    [Header("生命周期")]
    public float totalLifetime = 10f;      // 存活10秒
    public float blinkDuration = 2f;       // 最后2秒闪烁
    public float blinkInterval = 0.1f;     // 闪烁间隔

    private SpriteRenderer spriteRenderer;
    private Coroutine destroyCoroutine;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        destroyCoroutine = StartCoroutine(AutoDestroyWithBlink());
        
        // 设置 Tag（确保能被 Player 清除）
        gameObject.tag = "Collectible";
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
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.PickUpPowerup(type); // 传递类型给玩家
            }

            // 销毁自己
            if (destroyCoroutine != null)
                StopCoroutine(destroyCoroutine);
            Destroy(gameObject);
        }
    }
}