// CoinPickup.cs
using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [Header("金币属性")]
    public int coinValue = 1; // 1 或 5

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检测是否是玩家（建议给玩家加 Tag "Player"）
        if (other.CompareTag("Player"))
        {
            // 增加金币
            GameController.AddCoins(coinValue);

            // 播放音效（可选）
            // AudioManager.Play("CoinPickup");

            // 销毁金币
            Destroy(gameObject);
        }
    }
}