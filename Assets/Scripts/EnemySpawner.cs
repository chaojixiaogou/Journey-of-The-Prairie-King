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

    [Header("初始延迟")]
    public float initialDelay = 2f; // 默认延迟 2 秒，可在 Inspector 调整

    private const string ENEMY_FLY_NAME = "Enemy_Fly";
    private const string ENEMY_GHOST_NAME = "Enemy_Ghost";

    void Start()
    {
        GenerateSpawnPoints();

        // 👇 关键：如果游戏还没开始，先暂停自己
        if (!GameController.HasGameStarted)
        {
            isPaused = true;
            Debug.Log("[EnemySpawner] 游戏尚未开始，暂停生成");
        }
        
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    void GenerateSpawnPoints()
    {
        spawnPoints.Clear();
        float[] offsets = { -1.5f, -0.5f, 0.5f, 1.5f };

        // 上边 (y = +7)
        foreach (float x in offsets) spawnPoints.Add(new Vector3(x, 7.5f, 0));
        // 下边 (y = -8)
        foreach (float x in offsets) spawnPoints.Add(new Vector3(x, -7.5f, 0));
        // 左边 (x = -8)
        foreach (float y in offsets) spawnPoints.Add(new Vector3(-7.5f, y, 0));
        // 右边 (x = +7)
        foreach (float y in offsets) spawnPoints.Add(new Vector3(7.5f, y, 0));

        Debug.Log($"[EnemySpawner] 已生成 {spawnPoints.Count} 个中心对齐的生成点");
    }

    // ===== 新增：在地图四条边上随机选一个点 =====
    Vector3 GetRandomBoundaryPosition()
    {
        // 地图边界（与你当前 spawnPoints 一致）
        float topY = 7.5f;
        float bottomY = -7.5f;
        float leftX = -7.5f;
        float rightX = 7.5f;

        // 随机选择四条边之一
        int edge = Random.Range(0, 4);

        switch (edge)
        {
            case 0: // 上边 (y = topY)
                return new Vector3(Random.Range(leftX, rightX), topY, 0);
            case 1: // 下边 (y = bottomY)
                return new Vector3(Random.Range(leftX, rightX), bottomY, 0);
            case 2: // 左边 (x = leftX)
                return new Vector3(leftX, Random.Range(bottomY, topY), 0);
            case 3: // 右边 (x = rightX)
                return new Vector3(rightX, Random.Range(bottomY, topY), 0);
            default:
                return Vector3.zero;
        }
    }

    System.Collections.IEnumerator SpawnLoop()
    {
        // ✅ 新增：初始延迟
        if (initialDelay > 0)
        {
            Debug.Log($"[EnemySpawner] 等待 {initialDelay} 秒后开始生成敌人...");
            yield return new WaitForSeconds(initialDelay);
        }
        
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

            Vector3 spawnPosition;

            // 👇 新增：判断是否为 Fly 或 Ghost
            string prefabName = selectedPrefab.name;
            if (prefabName == ENEMY_FLY_NAME || prefabName == ENEMY_GHOST_NAME)
            {
                spawnPosition = GetRandomBoundaryPosition(); // 连续边界
            }
            else
            {
                spawnPosition = spawnPoints[Random.Range(0, spawnPoints.Count)]; // 原有离散点
            }

            Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
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

    public void StopSpawning()
    {
        StopAllCoroutines(); // 或设置 isSpawning = false
    }
}