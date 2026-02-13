using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public enum EnemyType
{
    Normal,      // 普通敌人（A* 寻路）
    Ghost,        // 幽灵（穿墙，直线追击）
    Sentry,       // 新增：哨兵（移动到随机点后静止+强化）
    Boss // 👈 新增 Boss 类型
    // 未来可加：Zombie, Boss, Kamikaze...
}

public enum BossType
{
    Cowboy, // 未来可加：Alien, Tank, etc.
    Demon
}

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    // === 可配置参数 ===
    public int maxHealth = 50;
    public float moveSpeed = 2f;
    public float pathUpdateInterval = 1.0f;
    public LayerMask obstacleLayer;

    [Header("=== 敌人类型 ===")]
    public EnemyType enemyType = EnemyType.Normal; // 默认普通敌人

    // === 行走动画资源（仅需这两张图）===
    public Sprite walkLeft;   // 迈左脚帧
    public Sprite walkRight;  // 迈右脚帧

    // === 受击反馈 ===
    public Sprite hitSprite;               // 拖入受击图片
    public float hitFlashDuration = 0.1f;  // 受击图片显示时间（秒）
    private float hitTimer = 0f;
    private bool isShowingHit = false;

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

    [Header("=== 哨兵模式配置 ===")]
    public Sprite[] sentryActivateFrames;     // 4张激活动画帧
    public float sentryFrameInterval = 0.1f; // 动画播放速度
    public Sprite sentryHitSprite;            // 拖入哨兵专用受击图
    public Sprite sentryIdleSprite;
    public bool isSentryActivated = false;    // 是否已激活（只触发一次）
    private bool sentryPathComputed = false; // 是否已计算路径

    private Vector3? sentryTargetPosition = null; // 哨兵的目标点（Nullable）

    [Header("=== Boss 配置 ===")]
    public BossType bossType = BossType.Cowboy;

    // Cowboy 专用
    public Vector2 bossSpawnPosition = new Vector2(0f, -6f); // 掩体后位置（地图中心偏下）

    // 动画资源
    public Sprite[] bossMovingFrames;   // [0]=左脚, [1]=右脚（移动时循环）
    public Sprite[] bossIdleFrames;     // [0]=静止A, [1]=静止B（静止时循环）
    public float bossAnimInterval = 0.2f;

    // 射击相关
    public GameObject bulletPrefab;     // 拖入子弹 Prefab
    public float shootInterval = 0.8f; // 射击间隔
    private float lastShootTime = -999f;

    // 行为状态
    private enum CowboyState
    {
        AtCover,                // 在掩体后（初始/结束状态）
        MovingToEdge,           // 正在移动到地图边缘（左或右）
        MovingAcrossMap,        // 从一端横穿到另一端
        PausingAtSide,          // 停在掩体侧边（-2 或 +2）
        ReturningToCover,       // 返回掩体中心
        PeekShooting            // 闪身射击模式（技能3）
    }
    private CowboyState cowboyState = CowboyState.AtCover;
    private float stateTimer = 0f;
    private bool isShooting = false;
    private Vector2 targetPosition = Vector2.zero;

    // 掩体位置（固定）
    private static readonly Vector2 COVER_LEFT = new Vector2(-2f, -6f);
    private static readonly Vector2 COVER_RIGHT = new Vector2(2f, -6f);
    private static readonly Vector2 COVER_CENTER = new Vector2(0f, -6f);

    // 地图边界
    private const float MAP_HALF_WIDTH = 8f; // 地图 -7.5 ~ +7.5

    // ===== Boss 技能路径控制 =====
    private Vector2[] bossSkillPath;          // 当前技能的路径点序列
    private int bossSkillPathIndex = 0;       // 👈 专用索引，不与 currentPathIndex 冲突
    private bool isExecutingBossSkill = false;
    private int peekShootCount = 0;

    // ===== Demon 专用配置 =====
    public GameObject[] demonEnemyPrefabs; // 拖入普通敌人 Prefab
    public Sprite[] idleAnimFrames;   // [0] 和 [1]：静止/移动时用
    public Sprite[] castingAnimFrames; // [0] 和 [1]：施法时用（技能2/3）
    private Vector2 demonSpawnPosition; // 出生点（用于返回）

    // 内部状态
    private enum DemonState
    {
        InitialDelay,      // 初始2秒
        Skill1_MovingToEdge,
        Skill1_Shooting,
        Skill1_Returning,
        Skill2_Spawning,
        Skill3_Shooting,
        ChoosingNextSkill
    }

    private DemonState demonState = DemonState.InitialDelay;
    private int skillPhase = 0; // 0=初始, 1=已放技能1, 2=已放技能2, >=3=随机
    private Vector2 targetEdgePosition;
    private int wavesSpawned = 0;

    private float shootCooldown = 0f; // 射击冷却

    [Header("=== Demon 召唤特效 ===")]
    public Sprite[] demonSummonEffectFrames; // 拖入 4~6 张召唤动画
    public float summonEffectInterval = 0.12f;

    // === 方向缓存（用于移动，非动画）===
    private Vector2 lastMovementDirection = Vector2.right;

    // === 死亡动画 ===
    public Sprite[] deathFrames;        // 拖入6张图
    public float deathFrameInterval = 0.1f;   // 每帧间隔（秒）
    public float finalFrameHoldTime = 1.0f;   // 最后一帧停留时间

    // ===== 僵尸模式支持 =====
    private static bool isZombieModeActive = false;
    private static Transform zombiePlayerTransform = null;

    // === 互斥道具掉落（每次最多掉一种）===
    public GameObject coin1Prefab;
    public GameObject coin5Prefab;
    public GameObject heartPrefab;

    [Header("=== 掉落总概率 ===")]
    public float totalDropChance = 0.8f; // 80% 概率掉落任意道具

    [Header("=== 道具类型权重（仅在掉落时生效）===")]
    public int coinWeight = 70;   // 金币权重（包括普通+稀有）
    public int heartWeight = 30;  // 生命道具权重

    [Tooltip("当掉落金币时，有此概率是5金币")]
    public float rareCoinChance = 0.1f;

    // === 新增：8种道具 Prefab ===
    public GameObject[] powerupPrefabs; // 按 PowerupType 顺序排列！

    [Header("=== 道具掉落权重 ===")]
    public int[] powerupWeights; // 与 powerupPrefabs 一一对应

    // === 防卡死 ===
    private Vector2 lastPosition;
    private float stuckTime = 0f;

    // === 行走动画控制 ===
    private float walkAnimTimer = 0f;
    private bool isOnLeftFoot = true;
    private const float WALK_ANIM_INTERVAL = 0.25f; // 每0.25秒切换一次脚
    private bool isMovingThisFrame = false;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

    // ===== 音效 =====
    public AudioClip[] deathSounds; // 拖入多个音效
    [Range(0f, 1f)]
    public float deathVolume = 0.6f;

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

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
        

        // ===== 根据类型初始化 =====
    if (enemyType == EnemyType.Boss && bossType == BossType.Cowboy)
    {
        // 强制设置出生位置（覆盖场景中的位置）
        transform.position = bossSpawnPosition;
        lastPosition = transform.position;

        // 初始化 Boss 状态
        cowboyState = CowboyState.AtCover;
        stateTimer = 0f;
        isShooting = false;

        // 设置初始贴图
        if (bossIdleFrames != null && bossIdleFrames.Length >= 2)
            spriteRenderer.sprite = bossIdleFrames[0];
    }
    else if (enemyType == EnemyType.Boss && bossType == BossType.Demon)
    {
        transform.position = demonSpawnPosition;
        lastPosition = transform.position;

        // 设置初始贴图
        if (idleAnimFrames != null && idleAnimFrames.Length >= 2)
            spriteRenderer.sprite = idleAnimFrames[0];
    }
    else if (enemyType == EnemyType.Sentry)
    {
        EnsureNotInsideObstacle();
        FindRandomValidSentryPosition();
        if (sentryTargetPosition.HasValue)
        {
            currentPath = FindPath(transform.position, sentryTargetPosition.Value);
            currentPathIndex = 0;
            sentryPathComputed = true;
        }
    }
    else if (enemyType != EnemyType.Ghost)
    {
        EnsureNotInsideObstacle();
    }
        lastPosition = transform.position;

        // 初始化第一帧动画
        if (spriteRenderer != null && walkRight != null)
            spriteRenderer.sprite = walkRight;
    }

    void FindRandomValidSentryPosition()
    {
        int attempts = 0;
        const int maxAttempts = 100;
        const float minDistance = 3f; // 至少离当前点 3 格（可调）

        Vector2 startPos = transform.position;

        while (attempts < maxAttempts)
        {
            float x = Random.Range(-7.5f, 7.5f);
            float y = Random.Range(-7.5f, 7.5f);
            Vector3 candidate = new Vector3(x, y, 0);

            // 检查距离
            if (Vector2.Distance(startPos, candidate) < minDistance)
                continue;

            // 检查是否无障碍
            if (Physics2D.OverlapCircle(candidate, 0.25f, obstacleLayer) == null)
            {
                sentryTargetPosition = candidate;
                Debug.Log($"[Sentry] 找到目标点: {candidate}");
                return;
            }
            attempts++;
        }

        // 如果失败，尝试放宽距离限制
        attempts = 0;
        while (attempts < maxAttempts)
        {
            float x = Random.Range(-7.5f, 7.5f);
            float y = Random.Range(-7.5f, 7.5f);
            Vector3 candidate = new Vector3(x, y, 0);

            if (Physics2D.OverlapCircle(candidate, 0.25f, obstacleLayer) == null)
            {
                sentryTargetPosition = candidate;
                Debug.LogWarning("[Sentry] 使用近距离目标点（理想点未找到）");
                return;
            }
            attempts++;
        }

        // 彻底失败：停在原地并激活
        Debug.LogError("[Sentry] 无法找到有效目标点！原地激活。");
        sentryTargetPosition = transform.position;
    }

    void EnsureNotInsideObstacle()
    {
        // 幽灵不需要避障
        if (enemyType == EnemyType.Ghost)
            return;

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
        if (isPaused || isDead || player == null) return;

        isMovingThisFrame = false;

        // ===== 僵尸模式优先处理 =====
        if (isZombieModeActive && zombiePlayerTransform != null)
        {
            HandleZombieMode();
            UpdateAnimation();
            return;
        }

        // ===== 按敌人类型执行不同 AI =====
        switch (enemyType)
        {
            case EnemyType.Normal:
                RunNormalAI();
                break;
            case EnemyType.Ghost:
                RunGhostAI();
                break;
            case EnemyType.Sentry:
                RunSentryAI();
                break;
            case EnemyType.Boss:
                RunBossAI();
                return;
            default:
                RunNormalAI(); // 安全兜底
                break;
        }

        UpdateAnimation();
    }

    void RunNormalAI()
    {
        // === 卡死检测 ===
        if (Vector2.Distance(transform.position, lastPosition) < 0.05f)
            stuckTime += Time.deltaTime;
        else
            stuckTime = 0f;
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
            FollowPath();
        else
            MoveDirectlyTowardsPlayer();
    }

    void RunGhostAI()
    {
        // 幽灵：无视障碍，直接朝玩家移动
        Vector2 direction = (player.position - transform.position).normalized;

        // 更新方向缓存（用于动画）
        if (direction.magnitude > 0.1f)
            lastMovementDirection = direction;

        // 直接移动（不检测障碍）
        transform.position += (Vector3)direction * moveSpeed * Time.deltaTime;
        isMovingThisFrame = true;
    }

    void RunSentryAI()
    {
        if (isSentryActivated) return;
        if (!sentryTargetPosition.HasValue || !sentryPathComputed)
        {
            isSentryActivated = true;
            ActivateSentryMode();
            return;
        }

        // 使用专用路径跟随
        if (currentPath != null && currentPath.Count > 0)
        {
            FollowSentryPath(); // 👈 不再调用通用 FollowPath
        }
        else
        {
            // 路径为空？直接走向目标（也不检测障碍）
            MoveToPathPoint(sentryTargetPosition.Value);
        }

        // 到达判断
        if (Vector2.Distance(transform.position, sentryTargetPosition.Value) < 0.4f)
        {
            isSentryActivated = true;
            ActivateSentryMode();
        }
    }

    void ActivateSentryMode()
    {
        // 血量翻倍（基于原始 maxHealth）
        currentHealth = maxHealth * 2;
        maxHealth = currentHealth; // 可选：也更新 maxHealth

        // 播放激活动画
        if (sentryActivateFrames != null && sentryActivateFrames.Length >= 4)
        {
            StartCoroutine(PlaySentryActivationAnimation());
        }
        else
        {
            // 如果没给动画，直接切到默认状态（比如最后一帧）
            Debug.LogWarning("未设置哨兵激活动画！");
            isMovingThisFrame = false;
        }
    }

    IEnumerator PlaySentryActivationAnimation()
    {
        // 播放前3帧
        for (int i = 0; i < sentryActivateFrames.Length - 1; i++)
        {
            spriteRenderer.sprite = sentryActivateFrames[i];
            yield return new WaitForSeconds(sentryFrameInterval);
        }

        // 设置最后一帧并永久保持
        spriteRenderer.sprite = sentryActivateFrames[sentryActivateFrames.Length - 1];
        spriteRenderer.sprite = sentryIdleSprite;

        // 确保行走动画不再覆盖它
        isMovingThisFrame = false;
        // （后续 UpdateAnimation 不会改 sprite）
    }

    void MoveDirectlyTo(Vector3 target)
    {
        Vector2 direction = (target - transform.position).normalized;
        if (direction.magnitude < 0.1f) return;

        lastMovementDirection = direction;
        Vector2 desiredPos = (Vector2)transform.position + direction * moveSpeed * Time.deltaTime;

        // 👇 关键：只移动，不尝试滑动！
        if (Physics2D.OverlapCircle(desiredPos, 0.25f, obstacleLayer) == null)
        {
            transform.position = desiredPos;
            isMovingThisFrame = true;
        }
        // 否则：不动（等待下次路径更新）
    }

    /// <summary>
    /// 哨兵专用：沿路径点移动，不检测障碍，不滑动
    /// </summary>
    void MoveToPathPoint(Vector3 target)
    {
        Vector2 direction = (target - transform.position).normalized;
        if (direction.magnitude < 0.1f) return;

        lastMovementDirection = direction;
        Vector2 desiredPos = (Vector2)transform.position + direction * moveSpeed * Time.deltaTime;

        // === 轻量碰撞检测 ===
        // 使用较小的半径（比如 0.24f 而不是 0.25f）留出容差
        const float radius = 0.24f;

        if (Physics2D.OverlapCircle(desiredPos, radius, obstacleLayer) == null)
        {
            // 安全：直接移动
            transform.position = desiredPos;
            isMovingThisFrame = true;
        }
        else
        {
            // ⚠️ 碰撞了！可能是动态物体（如玩家、子弹）临时阻挡
            // 哨兵应：短暂停顿 or 微调方向（但不滑动！）

            // 简单策略：尝试沿路径方向“挤一格”（小步试探）
            Vector2 smallStep = (Vector2)transform.position + direction * 0.1f;
            if (Physics2D.OverlapCircle(smallStep, radius, obstacleLayer) == null)
            {
                transform.position = smallStep;
                isMovingThisFrame = true;
            }
            // 否则：这一帧不动（等待障碍离开）
            // （不会左右滑动，不会穿墙）
        }
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

    void FollowSentryPath()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            // 路径走完，靠近最终目标
            MoveToPathPoint(sentryTargetPosition.Value);
            return;
        }

        Vector3 target = currentPath[currentPathIndex];
        if (Vector2.Distance(transform.position, target) < 0.4f)
        {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                MoveToPathPoint(sentryTargetPosition.Value);
                return;
            }
            target = currentPath[currentPathIndex];
        }

        MoveToPathPoint(target);
    }

    void RunBossAI()
    {
        if (bossType == BossType.Cowboy)
        {
            if (bossType != BossType.Cowboy) return;

            stateTimer += Time.deltaTime;

            switch (cowboyState)
            {
                case CowboyState.AtCover:
                    UpdateBossAnimation(false);
                    if (stateTimer > 1f)
                    {
                        ChooseRandomAction();
                    }
                    break;

                case CowboyState.MovingToEdge:
                case CowboyState.MovingAcrossMap:
                    MoveTowards(bossSkillPath[bossSkillPathIndex]);
                    UpdateBossAnimation(true);
                    if (isShooting && Time.time - lastShootTime > shootInterval)
                    {
                        ShootUpward();
                    }
                    if (Vector2.Distance(transform.position, bossSkillPath[bossSkillPathIndex]) < 0.4f)
                    {
                        bossSkillPathIndex++;
                        if (bossSkillPathIndex < bossSkillPath.Length)
                        {
                            // 还有下一个点：继续横穿
                            cowboyState = CowboyState.MovingAcrossMap;
                        }
                        else
                        {
                            // 路径走完：进入暂停状态
                            EnterPauseState();
                        }
                    }
                    break;

                case CowboyState.PausingAtSide:
                    UpdateBossAnimation(false);
                    if (!isShooting && stateTimer > 1.5f)
                    {
                        ReturnToCover();
                    }
                    break;

                case CowboyState.ReturningToCover:
                    MoveTowards(COVER_CENTER);
                    UpdateBossAnimation(true);
                    if (Vector2.Distance(transform.position, COVER_CENTER) < 0.4f)
                    {
                        cowboyState = CowboyState.AtCover;
                        stateTimer = 0f;
                        isShooting = false;
                    }
                    break;

                case CowboyState.PeekShooting:
                    // 由协程控制，这里不做逻辑
                    UpdateBossAnimation(false);
                    break;
            }
        }
        if (bossType == BossType.Demon)
        {
            RunDemonAI();
            return;
        }
    }

    void RunDemonAI()
    {
        if (player == null) return;

        stateTimer += Time.deltaTime;

        switch (demonState)
        {
            case DemonState.InitialDelay:
                UpdateDemonAnimation(false); // 使用 idle 动画
                if (stateTimer >= 1.5f)
                {
                    skillPhase = 1;
                    demonState = DemonState.ChoosingNextSkill;
                    stateTimer = 0f;
                }
                break;

            case DemonState.ChoosingNextSkill:
                UpdateDemonAnimation(false);
                if (stateTimer >= 0.1f) // 立即选
                {
                    if (skillPhase == 1)
                    {
                        StartSkill1();
                        skillPhase = 2;
                    }
                    else if (skillPhase == 2)
                    {
                        StartSkill2();
                        skillPhase = 3;
                    }
                    else
                    {
                        // 随机选择 1, 2, 3
                        int r = Random.Range(1, 4);
                        if (r == 1) StartSkill1();
                        else if (r == 2) StartSkill2();
                        else StartSkill3();
                    }
                    stateTimer = 0f;
                }
                break;

            case DemonState.Skill1_MovingToEdge:
                UpdateDemonAnimation(true); // 使用 idle 动画（移动状态）
                MoveTowards(targetEdgePosition);
                TryShootAtPlayer(); // 👈 新增：边走边射

                if (Vector2.Distance(transform.position, targetEdgePosition) < 0.4f)
                {
                    demonState = DemonState.Skill1_Shooting;
                    stateTimer = 0f;
                    // 注意：此时进入纯射击阶段，由协程控制总时长
                }
                break;

            case DemonState.Skill1_Shooting:
                UpdateDemonAnimation(false);
                TryShootAtPlayer();
                break;

            case DemonState.Skill1_Returning:
                UpdateDemonAnimation(true);
                MoveTowards(demonSpawnPosition);
                TryShootAtPlayer(); // 👈 新增：边走边射

                if (Vector2.Distance(transform.position, demonSpawnPosition) < 0.4f)
                {
                    demonState = DemonState.ChoosingNextSkill;
                    stateTimer = 0f;
                }
                break;

            case DemonState.Skill2_Spawning:
                UpdateDemonAnimation(true); // 施法动画
                // 由协程控制，这里不处理
                break;

            case DemonState.Skill3_Shooting:
                UpdateDemonAnimation(true); // 施法动画
                // 由协程控制
                break;
        }
    }

    void StartSkill1()
    {
        // 四个边界中点
        Vector2 top = new Vector2(0f, 7f);
        Vector2 bottom = new Vector2(0f, -7f);
        Vector2 left = new Vector2(-7f, 0f);
        Vector2 right = new Vector2(7f, 0f);
        Vector2[] edges = { top, bottom, left, right };

        // 找离玩家最远的点
        Vector2 playerPos = player.position;
        targetEdgePosition = edges[0];
        float maxDist = 0f;
        foreach (var edge in edges)
        {
            float d = Vector2.Distance(playerPos, edge);
            if (d > maxDist)
            {
                maxDist = d;
                targetEdgePosition = edge;
            }
        }

        demonState = DemonState.Skill1_MovingToEdge;
        stateTimer = 0f;

        // 启动射击协程（在到达后开始）
        StartCoroutine(Skill1_ShootAfterArrival());
    }

    IEnumerator Skill1_ShootAfterArrival()
    {
        // 等待进入 Shooting 状态
        while (demonState != DemonState.Skill1_Shooting)
            yield return null;

        float shootDuration = Random.Range(5f, 8f);
        float elapsed = 0f;
        while (elapsed < shootDuration && demonState == DemonState.Skill1_Shooting)
        {
            // 实际射击由 TryShootAtPlayer() 在 Update 中处理
            // 这里只需维持状态
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 时间到，开始返回
        demonState = DemonState.Skill1_Returning;
        stateTimer = 0f;
    }

    void ShootAtPlayer()
    {
        if (bulletPrefab == null || player == null) return;
        Vector2 dir = (player.position - transform.position).normalized;
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.isFromBoss = true;
            b.SetDirection(dir);
        }
    }

    void StartSkill2()
    {
        demonState = DemonState.Skill2_Spawning;
        wavesSpawned = 0;
        StartCoroutine(Skill2_SpawnWaves());
    }

    IEnumerator Skill2_SpawnWaves()
    {
        for (int wave = 0; wave < 3; wave++)
        {
            if (demonEnemyPrefabs == null || demonEnemyPrefabs.Length == 0)
            {
                yield return new WaitForSeconds(2f);
                continue;
            }

            int randomIndex = Random.Range(0, demonEnemyPrefabs.Length);
            GameObject selectedPrefab = demonEnemyPrefabs[randomIndex];

            Vector2[] offsets = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

            // 启动4个并行动画+生成
            List<IEnumerator> spawnRoutines = new List<IEnumerator>();
            foreach (Vector2 offset in offsets)
            {
                Vector3 spawnPos = transform.position + (Vector3)offset;
                spawnRoutines.Add(PlayDeathEffectThenSpawn(spawnPos, selectedPrefab));
            }

            // 并行执行所有4个动画
            while (spawnRoutines.Count > 0)
            {
                for (int i = spawnRoutines.Count - 1; i >= 0; i--)
                {
                    if (!spawnRoutines[i].MoveNext())
                    {
                        spawnRoutines.RemoveAt(i);
                    }
                }
                yield return null;
            }

            // 本波结束，等待1秒再下一波
            yield return new WaitForSeconds(2f);
        }

        demonState = DemonState.ChoosingNextSkill;
        stateTimer = 0f;
    }

    /// <summary>
    /// 在指定位置播放死亡动画，结束后生成指定敌人
    /// </summary>
    IEnumerator PlayDeathEffectThenSpawn(Vector3 position, GameObject enemyToSpawn)
    {
        Sprite[] effectFrames = demonSummonEffectFrames ?? deathFrames;
        float interval = demonSummonEffectFrames != null ? summonEffectInterval : deathFrameInterval;

        if (effectFrames == null || effectFrames.Length == 0)
        {
            if (enemyToSpawn != null)
                Instantiate(enemyToSpawn, position, Quaternion.identity);
            yield break;
        }

        GameObject effectObj = new GameObject("DemonSummonEffect");
        effectObj.transform.position = position;
        SpriteRenderer sr = effectObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Effects"; // 👈 建议新建 Effects 层
        sr.sortingOrder = 9999;           // 👈 确保在最上层

        foreach (var frame in effectFrames)
        {
            sr.sprite = frame;
            yield return new WaitForSeconds(interval);
        }

        if (enemyToSpawn != null)
            Instantiate(enemyToSpawn, position, Quaternion.identity);

        Destroy(effectObj);
    }

    void StartSkill3()
    {
        demonState = DemonState.Skill3_Shooting;
        StartCoroutine(Skill3_FireEightDirections());
    }

    IEnumerator Skill3_FireEightDirections()
    {
        float elapsed = 0f;
        while (elapsed < 3f)
        {
            // 8 个方向（45度间隔）
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (bulletPrefab != null)
                {
                    GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                    Bullet b = bullet.GetComponent<Bullet>();
                    if (b != null)
                    {
                        b.isFromBoss = true;
                        b.SetDirection(dir);
                    }
                }
            }
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }
        demonState = DemonState.ChoosingNextSkill;
        stateTimer = 0f;
    }

    void TryShootAtPlayer()
    {
        if (player == null || bulletPrefab == null) return;

        shootCooldown -= Time.deltaTime;
        if (shootCooldown <= 0f)
        {
            ShootAtPlayer(); // 你已有的方法
            shootCooldown = shootInterval;
        }
    }

    private float demonAnimTimer = 0f;
    private bool demonUseCastingAnim = false;
    private int demonAnimIndex = 0;

    void UpdateDemonAnimation(bool isCasting)
    {
        demonUseCastingAnim = isCasting;

        // 如果正在显示受击效果
        if (isShowingHit)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f)
            {
                isShowingHit = false;
            }
            return;
        }

        demonAnimTimer += Time.deltaTime;
        float interval = 0.25f;
        Sprite[] frames = demonUseCastingAnim ? castingAnimFrames : idleAnimFrames;

        if (frames == null || frames.Length < 2) return;

        if (demonAnimTimer >= interval)
        {
            demonAnimTimer = 0f;
            demonAnimIndex = (demonAnimIndex + 1) % 2;
            spriteRenderer.sprite = frames[demonAnimIndex];
        }
    }

    void ChooseRandomAction()
    {
        int choice = Random.Range(0, 3);
        stateTimer = 0f;

        switch (choice)
        {
            case 0: // 技能1：右 → 左 → 停左 → 回
                bossSkillPath = new Vector2[]
                {
                    new Vector2(MAP_HALF_WIDTH, -6f),   // 右边缘
                    new Vector2(-MAP_HALF_WIDTH, -6f),  // 左边缘
                    new Vector2(-3f, -6f)               // 掩体左侧
                };
                // isMovingAlongPath = true;
                bossSkillPathIndex = 0;
                cowboyState = CowboyState.MovingToEdge;
                isShooting = true;
                break;

            case 1: // 技能2：左 → 右 → 停右 → 回
                bossSkillPath = new Vector2[]
                {
                    new Vector2(-MAP_HALF_WIDTH, -6f),  // 左边缘
                    new Vector2(MAP_HALF_WIDTH, -6f),   // 右边缘
                    new Vector2(3f, -6f)                // 掩体右侧
                };
                // isMovingAlongPath = true;
                bossSkillPathIndex = 0;
                cowboyState = CowboyState.MovingToEdge;
                isShooting = true;
                break;

            case 2: // 技能3：闪身6枪
                peekShootCount = 0;
                cowboyState = CowboyState.PeekShooting;
                isShooting = false; // 射击由协程控制
                StartCoroutine(DoPeekShootSequence());
                break;
        }
    }

    IEnumerator DoPeekShootSequence()
    {
        for (int i = 0; i < 3; i++)
        {
            // 闪到右侧
            yield return MoveToAndShoot(new Vector2(3f, -6f));
            // 闪到左侧
            yield return MoveToAndShoot(new Vector2(-3f, -6f));
        }

        // 全部完成，返回掩体
        while (Vector2.Distance(transform.position, COVER_CENTER) > 0.4f)
        {
            MoveTowards(COVER_CENTER);
            UpdateBossAnimation(true);
            yield return null;
        }

        cowboyState = CowboyState.AtCover;
        stateTimer = 0f;
    }

    IEnumerator MoveToAndShoot(Vector2 pos)
    {
        // 移动到位置
        while (Vector2.Distance(transform.position, pos) > 0.4f)
        {
            MoveTowards(pos);
            UpdateBossAnimation(true);
            yield return null;
        }

        // 开一枪
        ShootUpward();
        yield return new WaitForSeconds(0.2f); // 枪口停顿

        // 返回掩体
        while (Vector2.Distance(transform.position, COVER_CENTER) > 0.4f)
        {
            MoveTowards(COVER_CENTER);
            UpdateBossAnimation(true);
            yield return null;
        }

        yield return new WaitForSeconds(0.1f); // 掩体停顿
    }

    // IEnumerator MoveToAndShoot(Vector2 pos)
    // {
    //     while (Vector2.Distance(transform.position, pos) > 0.4f)
    //     {
    //         MoveTowards(pos);
    //         UpdateBossAnimation(true);
    //         if (Time.time - lastShootTime > shootInterval)
    //         {
    //             ShootUpward();
    //         }
    //         yield return null;
    //     }
    //     yield return new WaitForSeconds(0.3f); // 短暂停顿
    // }

    void EnterPauseState()
    {
        cowboyState = CowboyState.PausingAtSide;
        stateTimer = 0f;
        isShooting = false;
    }

    void ReturnToCover()
    {
        cowboyState = CowboyState.ReturningToCover;
        stateTimer = 0f;
    isShooting = false;
    }
    void MoveTowards(Vector2 target)
    {
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        if (direction.magnitude < 0.1f) return;

        lastMovementDirection = direction;
        transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
        isMovingThisFrame = true; // 仅用于通用动画，Boss 用自己的
    }

    void ShootUpward()
    {
        if (bulletPrefab == null) return;
        lastShootTime = Time.time;

        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        Bullet bulletComp = bullet.GetComponent<Bullet>();
        if (bulletComp != null)
        {
            bulletComp.isFromBoss = true;
            bulletComp.SetDirection(Vector2.up); // 👈 关键：设置方向！
        }
    }

    private float bossAnimTimer = 0f;
    private int bossAnimIndex = 0;

    void UpdateBossAnimation(bool isMoving)
    {
        // 如果正在显示受击效果
        if (isShowingHit)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f)
            {
                isShowingHit = false;
            }
            return;
        }

        bossAnimTimer += Time.deltaTime;
        Sprite[] frames = isMoving ? bossMovingFrames : bossIdleFrames;

        if (frames == null || frames.Length == 0) return;

        if (bossAnimTimer >= bossAnimInterval)
        {
            bossAnimTimer = 0f;
            bossAnimIndex = (bossAnimIndex + 1) % frames.Length;
        }

        spriteRenderer.sprite = frames[bossAnimIndex];
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

    void UpdateAnimation()
    {
        // 如果正在显示受击效果
        if (isShowingHit)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f)
            {
                isShowingHit = false;
                // 👇 关键：哨兵激活状态下，恢复为常态图
                if (enemyType == EnemyType.Sentry && isSentryActivated)
                {
                    if (sentryIdleSprite != null)
                        spriteRenderer.sprite = sentryIdleSprite;
                    // 否则保持原样（安全兜底）
                }
            }
            return;
        }

        // ===== 哨兵已激活：禁止任何动画覆盖 =====
        if (enemyType == EnemyType.Sentry && isSentryActivated)
        {
            return; // 保持当前 sprite（即激活动画最后一帧）
        }

        // === 原行走动画逻辑 ===
        if (!isMovingThisFrame)
        {
            // 可选：静止时显示默认帧
            return;
        }

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
        if (isDead) return;

        currentHealth -= damage;

        // 👇 根据状态决定是否显示受击效果 & 用哪张图
        if (currentHealth > 0)
        {
            if (enemyType == EnemyType.Sentry && isSentryActivated)
            {
                // 哨兵激活状态：使用 sentryHitSprite
                if (sentryHitSprite != null)
                {
                    ShowHitEffect(sentryHitSprite);
                }
            }
            else
            {
                // 普通状态：使用 hitSprite
                if (hitSprite != null)
                {
                    ShowHitEffect(hitSprite);
                }
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 显示指定的受击图片
    /// </summary>
    void ShowHitEffect(Sprite hitSpriteToUse)
    {
        if (spriteRenderer == null || hitSpriteToUse == null) return;

        spriteRenderer.sprite = hitSpriteToUse;
        isShowingHit = true;
        hitTimer = hitFlashDuration;
    }

    private bool isDead = false;

    // 提供只读访问
    public bool IsDead => isDead;

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 🔊 播放随机死亡音效
        PlayRandomDeathSound();

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // 启动带金币掉落的死亡动画
        StartCoroutine(PlayDeathAnimationAndDropCoin());

        // 👇 新增：如果是 Boss，触发白色闪光 + 地图切换
        if (enemyType == EnemyType.Boss)
        {
            // 确保 LevelManager 已初始化
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.StartWhiteFlashTransition(() =>
                {
                    if(bossType == BossType.Cowboy)
                    {
                        // 在全白瞬间切换地图
                        LevelManager.Instance?.ResetOrChangeTilemap();
                    }
                });
            }
            else
            {
                Debug.LogWarning("[Enemy] LevelManager 未找到，无法执行 Boss 死亡特效！");
            }
        }
    }

    /// <summary>
    /// 播放一个随机的死亡音效（从 deathSounds 中选）
    /// </summary>
    public void PlayRandomDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0)
            return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        AudioSource.PlayClipAtPoint(clip, transform.position, deathVolume);
    }

    IEnumerator PlayDeathAnimationAndDropCoin()
    {
        // === 情况1：有死亡帧 ===
        if (deathFrames != null && deathFrames.Length > 0)
        {
            // 播放前 N-1 帧
            for (int i = 0; i < deathFrames.Length - 1; i++)
            {
                spriteRenderer.sprite = deathFrames[i];
                yield return new WaitForSeconds(deathFrameInterval);
            }

            // 显示最后一帧
            spriteRenderer.sprite = deathFrames[deathFrames.Length - 1];

            // 👇 关键：立即掉落金币（就在最后一帧显示时！）
            TryDropLoot();

            // 继续停留 finalFrameHoldTime 秒（尸体+金币共存）
            yield return new WaitForSeconds(finalFrameHoldTime);
        }
        // === 情况2：无死亡帧（兜底）===
        else
        {
            // 立即掉金币，短暂停留后销毁
            TryDropLoot();
            yield return new WaitForSeconds(finalFrameHoldTime);
        }

        // 销毁敌人本体
        Destroy(gameObject);
    }

    /// <summary>
    /// 敌人死亡时，按互斥规则尝试掉落一种道具（金币 或 心）
    /// </summary>
    void TryDropLoot()
    {
        if (Random.value >= totalDropChance)
            return;

        // 计算总权重：金币 + 心 + 所有道具
        int totalWeight = coinWeight + heartWeight;

        // 累加道具权重
        if (powerupWeights != null)
        {
            foreach (int w in powerupWeights)
                totalWeight += w;
        }

        if (totalWeight <= 0) return;

        int roll = Random.Range(0, totalWeight);

        // 区间1: 金币
        if (roll < coinWeight)
        {
            GameObject coin = (Random.value < rareCoinChance) ? coin5Prefab : coin1Prefab;
            InstantiateSafe(coin);
        }
        // 区间2: 心
        else if (roll < coinWeight + heartWeight)
        {
            InstantiateSafe(heartPrefab);
        }
        // 区间3: 道具
        else
        {
            int remaining = roll - coinWeight - heartWeight;
            int cumulative = 0;

            for (int i = 0; i < powerupWeights?.Length; i++)
            {
                cumulative += powerupWeights[i];
                if (remaining < cumulative && i < powerupPrefabs?.Length)
                {
                    InstantiateSafe(powerupPrefabs[i]);
                    break;
                }
            }
        }
    }

    public static void SetZombieMode(bool active, Transform player = null)
    {
        isZombieModeActive = active;
        zombiePlayerTransform = active ? player : null;
    }

    void HandleZombieMode()
    {
        // 重置移动标记（用于动画）
        isMovingThisFrame = false;

        float distanceToPlayer = Vector2.Distance(transform.position, zombiePlayerTransform.position);

        // ✅ 接触杀：距离 < 0.8 就死亡
        if (distanceToPlayer < 0.8f)
        {
            Die(); // 立即死亡（不触发掉落？按需保留）
            return;
        }

        // ✅ 逃跑：远离玩家
        Vector2 awayDir = (transform.position - zombiePlayerTransform.position).normalized;
        Vector2 desiredPos = (Vector2)transform.position + awayDir * moveSpeed * Time.deltaTime;

        // 尝试移动（简单避障）
        if (Physics2D.OverlapCircle(desiredPos, 0.25f, obstacleLayer) == null)
        {
            transform.position = desiredPos;
            isMovingThisFrame = true;
        }
        else
        {
            // 可选：尝试左右滑动逃跑（简化版）
            TrySlideAway(awayDir);
        }

        // 更新行走动画（如果移动了）
        UpdateAnimation();
    }

    void TrySlideAway(Vector2 awayDirection)
    {
        // 尝试垂直方向滑动
        Vector2 perpRight = new Vector2(-awayDirection.y, awayDirection.x);
        Vector2 perpLeft = new Vector2(awayDirection.y, -awayDirection.x);

        Vector2 testRight = (Vector2)transform.position + perpRight * moveSpeed * Time.deltaTime;
        Vector2 testLeft = (Vector2)transform.position + perpLeft * moveSpeed * Time.deltaTime;

        if (Physics2D.OverlapCircle(testRight, 0.25f, obstacleLayer) == null)
        {
            transform.position = testRight;
            isMovingThisFrame = true;
        }
        else if (Physics2D.OverlapCircle(testLeft, 0.25f, obstacleLayer) == null)
        {
            transform.position = testLeft;
            isMovingThisFrame = true;
        }
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    // 安全实例化辅助方法
    void InstantiateSafe(GameObject prefab)
    {
        if (prefab != null)
        {
            Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ✅ 僵尸模式下，敌人不能伤害玩家
        if (isZombieModeActive)
            return;

        if (other.CompareTag("Player"))
            other.GetComponent<PlayerController>()?.TakeDamage(1);
    }
}