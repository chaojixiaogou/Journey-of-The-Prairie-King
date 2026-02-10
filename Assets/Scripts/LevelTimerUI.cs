// LevelTimerUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelTimerUI : MonoBehaviour
{
    [Header("UI References")]
    public Image clockIcon;          // 你的时钟图片（Image）
    public Slider timeSlider;        // 进度条（Slider）
    public TMP_Text timeText;        // 可选：显示 "45s" 文字（TMP）

    private void OnEnable()
    {
        // 订阅事件
        GameController.OnLevelTimeUpdated += UpdateTimerUI;
        GameController.OnLevelTimeFinished += OnTimerFinished;
    }

    private void OnDisable()
    {
        // 取消订阅
        GameController.OnLevelTimeUpdated -= UpdateTimerUI;
        GameController.OnLevelTimeFinished -= OnTimerFinished;
    }

    private void UpdateTimerUI(float currentTime, float totalTime)
    {
        float ratio = Mathf.Clamp01(currentTime / totalTime);
        
        // 更新进度条
        timeSlider.value = ratio;
    
        // 可选：更新文字
        if (timeText != null)
        {
            timeText.text = Mathf.CeilToInt(currentTime).ToString();
        }
    
        // 获取 Slider 的 Fill Image
        Image fillImage = timeSlider.fillRect?.GetComponent<Image>();
        if (fillImage != null)
        {
            // 使用 HSV 渐变：Hue 从 0（红）到 120（绿）
            float hue = ratio * 120f; // 时间越多，越绿
            Color fillColor = Color.HSVToRGB(hue / 360f, 1f, 1f); // S=1, V=1
            fillImage.color = fillColor;
        }
    }

    private void OnTimerFinished()
    {
        // 倒计时结束，可以隐藏 UI 或显示“通关”动画
        Debug.Log("⏰ 倒计时 UI：时间到！");
    }
}