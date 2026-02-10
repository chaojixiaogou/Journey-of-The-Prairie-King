using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 适用于 16x16 地图，且原点 (0,0) 在地图中心的情况
/// 敌人生成在四周边缘的 1-based 第7~10格（即每边中间4格）
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("敌人设置")]
    public GameObject enemyPrefab;
    public int totalEnemiesToSpawn = -1; // -1 = 无限生成
    public float spawnInterval = 2f;

    private List<Vector3> spawnPoints = new List<Vector3>();
    private int spawnedCount = 0;

    [Header("运行时控制")]
    private bool isPaused = false;
    private Coroutine spawnCoroutine;

    void Start()
    {
        GenerateSpawnPoints();
        spawnCoroutine = StartCoroutine(SpawnLoop()); // 保存协程引用
    }

    void GenerateSpawnPoints()
    {
        spawnPoints.Clear();

        // 中间四格对应的偏移量（相对于边中心）
        int[] offsets = { -2, -1, 0, 1 }; // 这就是 1-based 第7~10格在中心坐标系下的值

        // 上边 (y = +7)
        foreach (int x in offsets)
            spawnPoints.Add(new Vector3(x, 7, 0));

        // 下边 (y = -8)
        foreach (int x in offsets)
            spawnPoints.Add(new Vector3(x, -8, 0));

        // 左边 (x = -8)
        foreach (int y in offsets)
            spawnPoints.Add(new Vector3(-8, y, 0));

        // 右边 (x = +7)
        foreach (int y in offsets)
            spawnPoints.Add(new Vector3(7, y, 0));

        Debug.Log($"[EnemySpawner] 已生成 {spawnPoints.Count} 个中心对齐的生成点");
    }

    System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (isPaused)
            {
                yield return null; // 暂停期间不退出协程，只等待
                continue;
            }

            if (enemyPrefab == null || spawnPoints.Count == 0)
                yield break;

            if (totalEnemiesToSpawn > 0 && spawnedCount >= totalEnemiesToSpawn)
            {
                Debug.Log("[EnemySpawner] 达到最大生成数量");
                yield break;
            }

            Vector3 point = spawnPoints[Random.Range(0, spawnPoints.Count)];
            Instantiate(enemyPrefab, point, Quaternion.identity);
            spawnedCount++;

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void Pause()
    {
        isPaused = true;
    }
    
    public void Resume()
    {
        isPaused = false;
    }
}