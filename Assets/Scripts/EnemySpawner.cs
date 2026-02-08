// Assets/Scripts/EnemySpawner.cs
using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 2f;
    public float spawnDistance = 8f; // 从屏幕外多远处生成

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        Camera cam = Camera.main;
        float h = cam.orthographicSize;           // 屏幕高度的一半
        float w = h * cam.aspect;                 // 屏幕宽度的一半

        Vector3 pos;
        int side = Random.Range(0, 4);

        switch (side)
        {
            case 0: // 上
                pos = new Vector3(Random.Range(-w, w), h + spawnDistance, 0);
                break;
            case 1: // 下
                pos = new Vector3(Random.Range(-w, w), -h - spawnDistance, 0);
                break;
            case 2: // 左
                pos = new Vector3(-w - spawnDistance, Random.Range(-h, h), 0);
                break;
            default: // 右
                pos = new Vector3(w + spawnDistance, Random.Range(-h, h), 0);
                break;
        }

        Instantiate(enemyPrefab, pos, Quaternion.identity);
    }
}