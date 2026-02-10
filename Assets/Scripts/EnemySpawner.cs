using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 支持多种敌人类型，按权重随机生成
/// </summary>
[System.Serializable]
public class EnemySpawnOption
{
    public GameObject prefab;
    [Range(0, 100)]
    public int weight = 10; // 权重（总和建议 ≤ 100，但不强制）
}

public class EnemySpawner : MonoBehaviour
{
    [Header("敌人生成配置")]
    public List<EnemySpawnOption> enemyTypes = new List<EnemySpawnOption>();
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
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    void GenerateSpawnPoints()
    {
        spawnPoints.Clear();
        int[] offsets = { -2, -1, 0, 1 };

        // 上边 (y = +7)
        foreach (int x in offsets) spawnPoints.Add(new Vector3(x, 7, 0));
        // 下边 (y = -8)
        foreach (int x in offsets) spawnPoints.Add(new Vector3(x, -8, 0));
        // 左边 (x = -8)
        foreach (int y in offsets) spawnPoints.Add(new Vector3(-8, y, 0));
        // 右边 (x = +7)
        foreach (int y in offsets) spawnPoints.Add(new Vector3(7, y, 0));

        Debug.Log($"[EnemySpawner] 已生成 {spawnPoints.Count} 个中心对齐的生成点");
    }

    System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            if (spawnPoints.Count == 0 || enemyTypes == null || enemyTypes.Count == 0)
            {
                Debug.LogError("[EnemySpawner] 未设置敌人 Prefab！");
                yield break;
            }

            if (totalEnemiesToSpawn > 0 && spawnedCount >= totalEnemiesToSpawn)
            {
                Debug.Log("[EnemySpawner] 达到最大生成数量");
                yield break;
            }

            // === 按权重随机选择敌人类型 ===
            GameObject selectedPrefab = SelectEnemyByWeight();
            if (selectedPrefab == null)
            {
                yield return new WaitForSeconds(spawnInterval);
                continue;
            }

            Vector3 point = spawnPoints[Random.Range(0, spawnPoints.Count)];
            Instantiate(selectedPrefab, point, Quaternion.identity);
            spawnedCount++;

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    GameObject SelectEnemyByWeight()
    {
        int totalWeight = 0;
        foreach (var option in enemyTypes)
        {
            if (option.prefab != null)
                totalWeight += option.weight;
        }

        if (totalWeight <= 0) return null;

        int random = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var option in enemyTypes)
        {
            if (option.prefab == null) continue;
            cumulative += option.weight;
            if (random < cumulative)
                return option.prefab;
        }

        // 兜底（理论上不会走到这里）
        return enemyTypes[0].prefab;
    }

    public void Pause() => isPaused = true;
    public void Resume() => isPaused = false;
}