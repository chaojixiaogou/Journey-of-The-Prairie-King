using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    // === 可配置参数 ===
    public int maxHealth = 50;
    public float moveSpeed = 2f;
    public float pathUpdateInterval = 1.0f;
    public LayerMask obstacleLayer;

    // === 行走动画资源（仅需这两张图）===
    public Sprite walkLeft;   // 迈左脚帧
    public Sprite walkRight;  // 迈右脚帧

    // === 内部状态 ===
    private int currentHealth;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private List<Vector3> currentPath = new List<Vector3>();
    private int currentPathIndex = 0;
    private float lastPathUpdateTime = -999f;
    private const int MAX_SEARCH_NODES = 100;

    // === 贴墙滑动 ===
    private bool isSlidingWall = false;
    private Vector2 wallSlideDirection = Vector2.zero;
    private float wallSlideTimer = 0f;
    private const float WALL_SLIDE_DURATION = 0.6f;

    // === 方向缓存（用于移动，非动画）===
    private Vector2 lastMovementDirection = Vector2.right;

    // === 死亡动画 ===
    public Sprite[] deathFrames;        // 拖入6张图
    public float deathFrameInterval = 0.1f;   // 每帧间隔（秒）
    public float finalFrameHoldTime = 1.0f;   // 最后一帧停留时间

    // === 防卡死 ===
    private Vector2 lastPosition;
    private float stuckTime = 0f;

    // === 行走动画控制 ===
    private float walkAnimTimer = 0f;
    private bool isOnLeftFoot = true;
    private const float WALK_ANIM_INTERVAL = 0.25f; // 每0.25秒切换一次脚
    private bool isMovingThisFrame = false;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogError("[Enemy] 找不到 Tag 为 'Player' 的对象！");
            enabled = false;
            return;
        }

        EnsureNotInsideObstacle();
        lastPosition = transform.position;

        // 初始化第一帧动画
        if (spriteRenderer != null && walkRight != null)
            spriteRenderer.sprite = walkRight;
    }

    void EnsureNotInsideObstacle()
    {
        Vector2 pos = transform.position;
        if (Physics2D.OverlapCircle(pos, 0.25f, obstacleLayer) != null)
        {
            Vector2[] offsets = {
                Vector2.zero,
                Vector2.right * 0.3f, Vector2.left * 0.3f,
                Vector2.up * 0.3f, Vector2.down * 0.3f,
                new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0.3f),
                new Vector2(0.3f, -0.3f), new Vector2(-0.3f, -0.3f)
            };

            foreach (var offset in offsets)
            {
                Vector2 testPos = pos + offset;
                if (Physics2D.OverlapCircle(testPos, 0.25f, obstacleLayer) == null)
                {
                    transform.position = testPos;
                    lastPosition = transform.position;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        // 重置移动标记（关键！）
        isMovingThisFrame = false;

        // === 卡死检测 ===
        if (Vector2.Distance(transform.position, lastPosition) < 0.05f)
        {
            stuckTime += Time.deltaTime;
        }
        else
        {
            stuckTime = 0f;
        }
        lastPosition = transform.position;

        // === 动态路径更新 ===
        float updateInterval = stuckTime > 1.0f ? 0.3f : pathUpdateInterval;
        if (Time.time - lastPathUpdateTime > updateInterval)
        {
            currentPath = FindPath(transform.position, player.position);
            currentPathIndex = 0;
            lastPathUpdateTime = Time.time;
        }

        // === 移动逻辑 ===
        if (currentPath != null && currentPath.Count > 0)
        {
            FollowPath();
        }
        else
        {
            MoveDirectlyTowardsPlayer();
        }

        // === 更新行走动画 ===
        UpdateWalkAnimation();
    }

    void FollowPath()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            MoveDirectlyTowardsPlayer();
            return;
        }

        Vector3 target = currentPath[currentPathIndex];
        if (Vector2.Distance(transform.position, target) < 0.4f)
        {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                MoveDirectlyTowardsPlayer();
                return;
            }
            target = currentPath[currentPathIndex];
        }

        Vector2 direction = (target - transform.position).normalized;
        MoveInDirection(direction);
    }

    void MoveDirectlyTowardsPlayer()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        MoveInDirection(direction);
    }

    void MoveInDirection(Vector2 rawDirection)
    {
        if (rawDirection.magnitude > 0.1f)
        {
            lastMovementDirection = rawDirection.normalized;
        }

        Vector2 desiredPos = (Vector2)transform.position + lastMovementDirection * moveSpeed * Time.deltaTime;

        // === 尝试直行 ===
        if (Physics2D.OverlapCircle(desiredPos, 0.25f, obstacleLayer) == null)
        {
            transform.position = desiredPos;
            isMovingThisFrame = true;
            return;
        }

        // === 尝试贴墙滑动 ===
        if (!isSlidingWall)
        {
            Vector2 perpRight = new Vector2(-lastMovementDirection.y, lastMovementDirection.x);
            Vector2 perpLeft = new Vector2(lastMovementDirection.y, -lastMovementDirection.x);

            Vector2 testRight = (Vector2)transform.position + perpRight * moveSpeed * Time.deltaTime;
            Vector2 testLeft = (Vector2)transform.position + perpLeft * moveSpeed * Time.deltaTime;

            if (Physics2D.OverlapCircle(testRight, 0.25f, obstacleLayer) == null)
            {
                wallSlideDirection = perpRight;
                isSlidingWall = true;
                wallSlideTimer = 0f;
            }
            else if (Physics2D.OverlapCircle(testLeft, 0.25f, obstacleLayer) == null)
            {
                wallSlideDirection = perpLeft;
                isSlidingWall = true;
                wallSlideTimer = 0f;
            }
        }

        if (isSlidingWall)
        {
            wallSlideTimer += Time.deltaTime;
            if (wallSlideTimer <= WALL_SLIDE_DURATION)
            {
                Vector2 slidePos = (Vector2)transform.position + wallSlideDirection * moveSpeed * Time.deltaTime;
                if (Physics2D.OverlapCircle(slidePos, 0.25f, obstacleLayer) == null)
                {
                    transform.position = slidePos;
                    isMovingThisFrame = true;
                    return;
                }
            }
            isSlidingWall = false;
        }
    }

    void UpdateWalkAnimation()
    {
        if (!isMovingThisFrame) return;

        walkAnimTimer += Time.deltaTime;
        if (walkAnimTimer >= WALK_ANIM_INTERVAL)
        {
            walkAnimTimer = 0f;
            isOnLeftFoot = !isOnLeftFoot;
        }

        spriteRenderer.sprite = isOnLeftFoot ? walkLeft : walkRight;
    }

    // ===== A* 寻路系统 =====
    List<Vector3> FindPath(Vector3 start, Vector3 target)
    {
        Vector2Int startCell = WorldToCell(start);
        Vector2Int targetCell = WorldToCell(target);

        if (!IsInBounds(startCell) || !IsInBounds(targetCell))
            return null;

        var openSet = new Dictionary<Vector2Int, float>();
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();

        gScore[startCell] = 0;
        fScore[startCell] = Heuristic(startCell, targetCell);
        openSet[startCell] = fScore[startCell];

        int nodesSearched = 0;
        while (openSet.Count > 0)
        {
            if (++nodesSearched > MAX_SEARCH_NODES)
                return null;

            Vector2Int current = GetLowestFScoreNode(openSet);
            openSet.Remove(current);

            if (current == targetCell)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (var neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (IsBlocked(neighbor)) continue;

                float tentativeG = gScore.GetValueOrDefault(current) + 1;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, targetCell);
                    openSet[neighbor] = fScore[neighbor];
                }
            }
        }
        return null;
    }

    Vector2Int WorldToCell(Vector3 worldPos) => 
        new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));

    Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x, cell.y, 0);

    bool IsInBounds(Vector2Int c) => c.x >= -8 && c.x <= 7 && c.y >= -8 && c.y <= 7;

    bool IsBlocked(Vector2Int cell) =>
        Physics2D.OverlapCircle(CellToWorld(cell), 0.4f, obstacleLayer) != null;

    float Heuristic(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    List<Vector2Int> GetNeighbors(Vector2Int cell) => new List<Vector2Int>
    {
        new Vector2Int(cell.x + 1, cell.y),
        new Vector2Int(cell.x - 1, cell.y),
        new Vector2Int(cell.x, cell.y + 1),
        new Vector2Int(cell.x, cell.y - 1)
    };

    Vector2Int GetLowestFScoreNode(Dictionary<Vector2Int, float> openSet)
    {
        Vector2Int best = default;
        float bestScore = float.MaxValue;
        foreach (var kvp in openSet)
            if (kvp.Value < bestScore) { bestScore = kvp.Value; best = kvp.Key; }
        return best;
    }

    List<Vector3> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector3>();
        while (cameFrom.TryGetValue(current, out Vector2Int prev))
        {
            path.Add(CellToWorld(current));
            current = prev;
        }
        path.Add(CellToWorld(current));
        path.Reverse();
        return path;
    }

    // ===== 受伤 & 死亡 =====
    public void TakeDamage(int damage)
    {
        if (isDead) return; // 防止重复死亡

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private bool isDead = false;

    void Die()
    {
        if (isDead) return; // 防止重复调用
        isDead = true;
    
        // 👇 关键修复：立即禁用碰撞体，防止尸体继续触发伤害
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }
    
        // 可选：也禁用子物体的碰撞体（如果有）
        // foreach (Collider2D childCol in GetComponentsInChildren<Collider2D>())
        //     childCol.enabled = false;
    
        enabled = false; // 停止所有 AI 行为
    
        StartCoroutine(PlayDeathAnimation());
    }

    IEnumerator PlayDeathAnimation()
    {
        // 安全检查：确保有死亡帧
        if (deathFrames == null || deathFrames.Length == 0)
        {
            yield return new WaitForSeconds(finalFrameHoldTime);
            Destroy(gameObject);
            yield break;
        }

        // 播放前 N-1 帧
        for (int i = 0; i < deathFrames.Length - 1; i++)
        {
            spriteRenderer.sprite = deathFrames[i];
            yield return new WaitForSeconds(deathFrameInterval);
        }

        // 播放最后一帧
        spriteRenderer.sprite = deathFrames[deathFrames.Length - 1];
        yield return new WaitForSeconds(finalFrameHoldTime);

        // 动画结束，销毁对象
        Destroy(gameObject);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            other.GetComponent<PlayerController>()?.TakeDamage(1);
    }
}