// GameOverManager.cs
// 适用于 TextMeshPro (TMP) UI
using UnityEngine;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text restartText;
    public TMP_Text quitText;

    private int selectedOption = 0; // 0 = Restart, 1 = Quit

    private readonly string SELECTED_PREFIX = "> ";
    private readonly string UNSELECTED_PREFIX = "  ";

    private readonly Color SELECTED_COLOR = Color.white;
    private readonly Color UNSELECTED_COLOR = new Color(0.5f, 0.5f, 0.5f); // #808080

    void Update()
    {
        // 仅当此 Canvas 处于激活状态时才处理输入
        if (!gameObject.activeInHierarchy)
            return;

        // 上/下选择
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedOption = 0;
            UpdateSelectionDisplay();
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedOption = 1;
            UpdateSelectionDisplay();
        }

        // 确认选择
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ConfirmSelection();
        }
    }

    void UpdateSelectionDisplay()
    {
        // 更新 Restart 选项
        if (restartText != null)
        {
            restartText.text = (selectedOption == 0 ? SELECTED_PREFIX : UNSELECTED_PREFIX) + "Restart";
            restartText.color = (selectedOption == 0) ? SELECTED_COLOR : UNSELECTED_COLOR;
        }

        // 更新 Quit 选项
        if (quitText != null)
        {
            quitText.text = (selectedOption == 1 ? SELECTED_PREFIX : UNSELECTED_PREFIX) + "Quit";
            quitText.color = (selectedOption == 1) ? SELECTED_COLOR : UNSELECTED_COLOR;
        }
    }

    void ConfirmSelection()
    {
        if (selectedOption == 0)
        {
            // 重新开始：加载当前场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
        else
        {
            // 退出游戏
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}