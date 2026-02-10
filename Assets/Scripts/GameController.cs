// GameController.cs
using UnityEngine;
using System.Collections;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("复活设置")]
    public float respawnDelay = 2f; // 掉命后等待时间（秒）

    private bool isRespawning = false;

    void Awake()
    {
        // 单例模式：确保全局唯一
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 切换场景不销毁（可选）
        }
        else
        {
            Destroy(gameObject); // 避免重复
        }
    }

    /// <summary>
    /// 仅延迟指定时间后执行回调（不清敌！由 PlayerController 负责清敌）
    /// </summary>
    public void OnPlayerLoseLife(System.Action onRespawnCallback)
    {
        if (isRespawning) return;
        StartCoroutine(DelayedRespawn(onRespawnCallback));
    }
    
    IEnumerator DelayedRespawn(System.Action onRespawnCallback)
    {
        isRespawning = true;
    
        // 只等待，不清敌（PlayerController 已处理）
        yield return new WaitForSeconds(respawnDelay);
    
        onRespawnCallback?.Invoke();
    
        // 恢复生成器（PlayerController 会在回调中触发 Resume）
        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (var spawner in spawners)
        {
            spawner.Resume();
        }
    
        isRespawning = false;
    }
}