using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    // === 基础属性 ===
    public int maxHealth = 50;
    private int currentHealth;
    public int damageToPlayer = 10;
    public float moveSpeed = 2f;
    public float updatePathInterval = 0.5f; // 每隔多久重新计算路径

    // === 行走动画 ===
    public Sprite walkLeft;
    public Sprite walkRight;
    public float walkCycleTime = 0.3f;

    // === 寻路依赖 ===
    public LayerMask obstacleLayer; // 障碍物层
    public Vector2Int gridSize = new Vector2Int(20, 15); // 地图逻辑尺寸（格子数）
    public float cellSize = 1f; // 每格多少 Unity 单位（应 = Tilemap Cell Size / PPU）

    // === 内部状态 ===
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private List<Vector3> path = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float walkTimer = 0f;
    private bool isWalking = false;

    void Start()
    {
        currentHealth = maxHealth;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // 使用 Kinematic
        rb.simulated = true;

        if (spriteRenderer != null && walkRight != null)
            spriteRenderer.sprite = walkRight;

        if (player != null)
            InvokeRepeating(nameof(UpdatePath), 0f, updatePathInterval);
    }

    void Update()
    {
        if (player == null || path.Count == 0) return;

        // 移动到下一个路径点
        MoveOnPath();
    }

    void UpdatePath()
    {
        if (player == null) return;

        Vector3 startPos = transform.position;
        Vector3 targetPos = player.position;

        path = FindPath(startPos, targetPos);
        currentWaypointIndex = 0;
    }

    List<Vector3> FindPath(Vector3 start, Vector3 target)
    {
        Vector2Int startCell = WorldToCell(start);
        Vector2Int targetCell = WorldToCell(target);
    
        if (!IsInBounds(startCell) || !IsInBounds(targetCell))
            return new List<Vector3>();
    
        // 使用 Dictionary 模拟 openSet 和 closedSet
        var openSet = new Dictionary<Vector2Int, float>(); // cell -> fScore
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();
    
        gScore[startCell] = 0;
        fScore[startCell] = Heuristic(startCell, targetCell);
        openSet[startCell] = fScore[startCell];
    
        while (openSet.Count > 0)
        {
            // 手动找出 fScore 最小的节点（代替 PriorityQueue）
            Vector2Int current = GetLowestFScoreNode(openSet);
            openSet.Remove(current);
    
            if (current == targetCell)
            {
                return ReconstructPath(cameFrom, current);
            }
    
            closedSet.Add(current);
    
            foreach (var neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (IsBlocked(neighbor)) continue;
    
                float tentativeGScore = gScore.GetValueOrDefault(current) + 1;
    
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, targetCell);
                    openSet[neighbor] = fScore[neighbor];
                }
            }
        }
    
        return new List<Vector3>(); // 无路径
    }
    
    // 辅助方法：从 openSet 中找出 fScore 最小的节点
    Vector2Int GetLowestFScoreNode(Dictionary<Vector2Int, float> openSet)
    {
        Vector2Int bestNode = default;
        float bestScore = float.MaxValue;
        foreach (var kvp in openSet)
        {
            if (kvp.Value < bestScore)
            {
                bestScore = kvp.Value;
                bestNode = kvp.Key;
            }
        }
        return bestNode;
    }

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.y / cellSize)
        );
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize, cell.y * cellSize, 0);
    }

    bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= -gridSize.x / 2 && cell.x <= gridSize.x / 2 &&
               cell.y >= -gridSize.y / 2 && cell.y <= gridSize.y / 2;
    }

    bool IsBlocked(Vector2Int cell)
    {
        Vector3 worldPos = CellToWorld(cell);
        Collider2D hit = Physics2D.OverlapCircle(worldPos, cellSize * 0.4f, obstacleLayer);
        return hit != null;
    }

    float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Manhattan 距离（适合网格）
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    List<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        var neighbors = new List<Vector2Int>
        {
            new Vector2Int(cell.x + 1, cell.y),
            new Vector2Int(cell.x - 1, cell.y),
            new Vector2Int(cell.x, cell.y + 1),
            new Vector2Int(cell.x, cell.y - 1)
        };
        return neighbors;
    }

    List<Vector3> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var totalPath = new List<Vector3> { CellToWorld(current) };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, CellToWorld(current));
        }
        return totalPath;
    }

    void MoveOnPath()
    {
        if (currentWaypointIndex >= path.Count) return;

        Vector3 target = path[currentWaypointIndex];
        Vector2 direction = (target - transform.position).normalized;
        Vector2 moveStep = direction * moveSpeed * Time.deltaTime;

        // 手动碰撞检测（防止穿墙）
        Vector2 newPosition = (Vector2)transform.position + moveStep;
        if (!Physics2D.OverlapCircle(newPosition, 0.2f, obstacleLayer))
        {
            transform.position = newPosition;
            isWalking = true;
        }
        else
        {
            isWalking = false;
        }

        // 到达当前路径点
        if (Vector2.Distance(transform.position, target) < 0.1f)
        {
            currentWaypointIndex++;
        }

        // 行走动画
        if (isWalking)
        {
            walkTimer += Time.deltaTime;
            if (walkTimer >= walkCycleTime)
            {
                if (spriteRenderer.sprite == walkLeft)
                    spriteRenderer.sprite = walkRight;
                else
                    spriteRenderer.sprite = walkLeft;
                walkTimer = 0f;
            }
        }
    }

    // === 受伤 & 死亡 ===
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController playerScript = other.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                playerScript.TakeDamage(damageToPlayer);
            }
        }
    }



    // 简易优先队列（Unity 2022+ 支持 System.Collections.Generic.PriorityQueue）
    // 如果报错，可替换为 SortedSet 或使用第三方实现
}