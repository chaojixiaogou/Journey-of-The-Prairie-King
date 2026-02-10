// CoinsUIManager.cs
using UnityEngine;
using TMPro;

public class CoinsUIManager : MonoBehaviour
{
    [Header("References")]
    public TMP_Text coinsCountText;

    private void OnEnable()
    {
        GameController.OnCoinsChanged += UpdateCoinsDisplay;
        UpdateCoinsDisplay(); // 初始化
    }

    private void OnDisable()
    {
        GameController.OnCoinsChanged -= UpdateCoinsDisplay;
    }

    private void UpdateCoinsDisplay()
    {
        int coins = GameController.TotalCoins;
        coinsCountText.text = "x" + coins;
    }
}