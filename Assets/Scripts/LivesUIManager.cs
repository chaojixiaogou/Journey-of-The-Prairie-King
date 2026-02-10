// LivesUIManager.cs
using UnityEngine;
using TMPro;

/// <summary>
/// 显示玩家生命值 UI：❤️ x3
/// 要求 PlayerController 实现静态事件 OnLivesChanged 和单例 Instance
/// </summary>
public class LivesUIManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text livesCountText; // 拖入你的 TMP Text 组件

    private void Start()
    {
        // 初始化显示（例如游戏开始时显示 x3）
        UpdateLivesDisplay();
        
        // 订阅静态事件（注意：用类名 PlayerController，不是 Instance）
        PlayerController.OnLivesChanged += UpdateLivesDisplay;
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏
        PlayerController.OnLivesChanged -= UpdateLivesDisplay;
    }

    /// <summary>
    /// 更新 UI 显示为 "x{当前生命数}"
    /// </summary>
    private void UpdateLivesDisplay()
    {
        // 通过单例 Instance 获取实例成员 currentLives
        int currentLives = PlayerController.Instance != null 
            ? PlayerController.Instance.currentLives 
            : 0;

        // 设置文本（如 "x3"）
        if (livesCountText != null)
        {
            livesCountText.text = "x" + currentLives;
        }
    }
}