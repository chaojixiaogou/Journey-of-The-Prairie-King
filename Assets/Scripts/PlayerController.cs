using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // === 移动参数 ===
    public float baseMoveSpeed = 5f;
    public float moveSpeed = 5f;
    public LayerMask obstacleLayer; // 用于碰撞检测的层（如 Obstacle）

    [Header("Collision Detection")]
    public Vector2 colliderSize = new Vector2(0.35f, 0.35f); // 碰撞检测半径（建议略小于角色）
    public float skinWidth = 0.03f; // 安全边距，防止卡墙

    // === 射击参数 ===
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float baseFireRate = 0.2f;
    public float fireRate = 0.2f;
    public int basePistolDamage = 25; // 初始伤害
    public  int pistolDamage = 25;
    private float lastFireTime;

    // === 射击方向对应的精灵 ===
    public Sprite upSprite;
    public Sprite downSprite;
    public Sprite leftSprite;
    public Sprite rightSprite;

    // === 血量与重生 ===
    [Header("Lives & Invincibility")]
    public int maxLives = 3;                // 初始命数
    public float invincibleDuration = 2f;   // 无敌时间
    public float blinkInterval = 0.1f;      // 闪烁频率
    public int currentLives;
    private bool isInvincible = false;
    private Vector3 spawnPosition; // 重用这个变量，但改为屏幕中心
    private bool isDead = false;

    // === 道具系统 ===
    private PowerupType? heldPowerup = null; // 可空，表示未持有
    public static System.Action<PowerupType?> OnPowerupChanged; // 用于 UI 更新

    [Header("=== 道具UI ===")]
    public Image heldPowerupIcon; // 拖入 UI Image 组件

    // 拖入 8 个道具图标（按 PowerupType 枚举顺序）
    public Sprite wheelSprite;
    public Sprite machineGunSprite;
    public Sprite nukeSprite;
    public Sprite tombstoneSprite;
    public Sprite coffeeSprite;
    public Sprite shotgunSprite;
    public Sprite smokeGrenadeSprite;
    public Sprite badgeSprite;

    // === 射击增强状态（由道具激活）===
    private bool isWheelActive = false;
    private float wheelEndTime = 0f;

    private bool isShotgunActive = false;
    private float shotgunEndTime = 0f;

    private bool isMachineGunActive = false;
    private float machineGunEndTime = 0f;

    private const float POWERUP_DURATION = 12f; // 所有道具持续时间

    // === 咖啡（移动加速）===
    private bool isCoffeeActive = false;
    private float coffeeEndTime = 0f;

    private const float COFFEE_DURATION = 16f; // 咖啡持续时间
    private const float COFFEE_SPEED_MULTIPLIER = 1.5f; // 移动速度倍率

    // === 警徽（Badge）===
    private bool isBadgeActive = false;
    private float badgeEndTime = 0f;

    private const float BADGE_DURATION = 24f; // 警徽持续时间

    // 当前是否有机枪效果（来自机枪 or 警徽）
    private bool IsMachineGunActiveNow => isMachineGunActive || isBadgeActive;

    // 当前是否有霰弹效果（来自霰弹 or 警徽）
    private bool IsShotgunActiveNow => isShotgunActive || isBadgeActive;

    // 当前是否有咖啡效果（来自咖啡 or 警徽）
    private bool IsCoffeeActiveNow => isCoffeeActive || isBadgeActive;

    // === 核弹死亡动画素材 ===
    [Header("核弹死亡动画")]
    public Sprite[] nukeDeathSprites; // 拖入5张Sprite（按顺序）
    public float nukeDeathFrameDuration = 0.08f; // 每帧持续时间（秒）
    public string nukeEffectSortingLayer = "Default"; // 可选：设置 Sorting Layer（如 "Effects"）

    // === 烟雾弹（Smoke Grenade）===
    private bool isSmokeActive = false;
    private float smokeEndTime = 0f;
    private const float SMOKE_DURATION = 4f;

    // === 烟雾弹残留动画 ===
    [Header("烟雾弹残留动画")]
    public Sprite[] smokeGrenadeResidueSprites; // 拖入你的5张Sprite（按顺序）
    public float smokeResidueFrameDuration = 0.1f; // 每帧持续时间（秒）
    public string smokeEffectSortingLayer = "Effects"; // 可选：设置 Sorting Layer

    // === 墓碑（Tombstone / Zombie Mode）===
    private bool isZombieMode = false;
    private float zombieEndTime = 0f;
    private const float ZOMBIE_DURATION = 8f;

    // 僵尸行走动画素材
    [Header("僵尸行走动画")]
    public Sprite zombieLeftFoot;   // 迈左脚
    public Sprite zombieRightFoot;  // 迈右脚
    public float zombieStepInterval = 0.2f; // 切换频率

    private Coroutine zombieWalkCoroutine;

    [Header("墓碑 - 玩家替换图片")]
    public Sprite tombstonePlayerReplacementSprite; // 拖入你的静态图片
    private GameObject replacementImageObject = null; // 运行时生成的对象引用


    // === 射击缓存（避免频繁 GC）===
    private List<Vector2> tempMainDirections = new List<Vector2>(8);   // 最多8个主方向
    private List<Vector2> tempFinalDirections = new List<Vector2>(24); // 最多24发（8×3）

    // ===== 事件系统 =====
    public static System.Action OnLivesChanged; // 生命值变化时触发
    public static PlayerController Instance;
    
    // === 死亡动画设置 ===
    [Header("Death Animation")]
    public Sprite[] deathAnimationFrames;      // 拖入5张图
    public float deathAnimationFrameDuration = 0.1f; // 每帧时间（秒）

    // === 运行时引用 ===
    private SpriteRenderer deathEffectRenderer;
    private bool isPlayingDeathAnim = false;   // 动画播放期间禁用输入

    // === Game Over UI ===
    [Header("Game Over")]
    public GameObject gameOverCanvas; // 拖入你的 Canvas

    // === 内部引用 ===
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 shootDirection = Vector2.right; // 默认朝右

    private bool isBoss = false;

    // ===== 音效 =====
    public AudioClip shootSound;      // 拖入 Inspector 的射击音效
    [Range(0f, 1f)]
    public float shootVolume = 0.7f;  // 音量（可选）
    
    private AudioSource audioSource;

    // ===== 拾取音效 =====
    public AudioClip pickupCollectibleSound; // 金币、生命
    public AudioClip pickupPowerupSound;     // 道具（Wheel, MachineGun 等）
    [Range(0f, 1f)]
    public float pickupVolume = 0.7f;

    // ===== 使用道具音效 =====
    public AudioClip useGraveSound;      // 墓碑
    public AudioClip useSmokeBombSound;  // 烟雾弹
    [Range(0f, 1f)]
    public float usePowerupVolume = 0.8f;

    public bool hasTriggeredNextLevel = false; // 👈 新增字段


    public bool isGameOver = false;

    

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // PlayerController 不 DontDestroyOnLoad！每关重建
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            // 重新查找出生点和箭头
            FindSpawnPoint();
            GameController.Instance?.SpawnExitArrowIfNeeded();
    
            // 重新初始化玩家状态
            Respawn();
            
            // 初始化组件（安全起见）
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            

            GameController.Instance.ResetLevelState(); // 👈 重置状态
        }
    }

    // 提取出生点查找逻辑
    void FindSpawnPoint()
    {
        GameObject spawnObj = GameObject.FindGameObjectWithTag("PlayerSpawn");
        if (spawnObj != null)
        {
            spawnPosition = spawnObj.transform.position;
            spawnPosition.z = 0;
        }
        else
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                spawnPosition = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(cam.transform.position.z)));
                spawnPosition.z = 0;
            }
            else
            {
                spawnPosition = Vector3.zero;
            }
            Debug.LogWarning("⚠️ 未找到 PlayerSpawn！");
        }

        // ✅ 初始化当前重生点为默认点
        currentRespawnPosition = spawnPosition;
    }

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        FindSpawnPoint(); // 必须调用！
        Respawn();        // 必须调用！

        SetupDeathEffectRenderer();
        UpdateHeldPowerupUI();

        RecalculateStatsFromUpgrades();
    }

    public void RecalculateStatsFromUpgrades()
    {
        var gc = GameController.Instance;
        if (gc == null) return;

        // 靴子：每级 ×1.25（累乘）
        float speedMult = Mathf.Pow(1.25f, gc.bootsUpgradeLevel);
        moveSpeed = baseMoveSpeed * speedMult;

        // 手枪：每级 ×1.25 射速 → 间隔 ÷1.25
        float fireRateMult = Mathf.Pow(1.25f, gc.pistolUpgradeLevel);
        fireRate = baseFireRate / fireRateMult;

        // 子弹袋：x2、x3、x4
        pistolDamage = basePistolDamage * (gc.ammoBagUpgradeLevel + 1);

        Debug.Log($"🔄 重算属性: 移速={moveSpeed:F2}, 射速间隔={fireRate:F2}");
    }

    void Update()
    {
        // 👇 新增：如果游戏还没开始，跳过所有输入处理
        if (!GameController.HasGameStarted)
        {
            return;
        }

        // 关键：动画播放或 Game Over 时完全禁用逻辑
        if (isDead || isPlayingDeathAnim)
        {
            // 确保主精灵隐藏（安全兜底）
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            return;
        }

        // ===== 自动过期道具效果 =====
        float now = Time.time;
        if (isWheelActive && now >= wheelEndTime) isWheelActive = false;
        if (isShotgunActive && now >= shotgunEndTime) isShotgunActive = false;
        if (isMachineGunActive && now >= machineGunEndTime) isMachineGunActive = false;
        if (isCoffeeActive && now >= coffeeEndTime) isCoffeeActive = false;
        if (isBadgeActive && now >= badgeEndTime) isBadgeActive = false;

        // ===== 僵尸模式结束 =====
        if (isZombieMode && Time.time >= zombieEndTime)
        {
            DeactivateZombieMode();
        }

        // 原有的无敌闪烁逻辑（仅在非动画期间生效）
        if (isInvincible)
        {
            float blinkPhase = (Time.time * (1f / blinkInterval)) % 2;
            spriteRenderer.enabled = blinkPhase < 1f;
        }

        if (isDead) return;

        // ===== 处理射击输入（方向键）=====
        Vector2 shootInput = Vector2.zero;
        if (Input.GetKey(KeyCode.UpArrow))    shootInput += Vector2.up;
        if (Input.GetKey(KeyCode.DownArrow))  shootInput += Vector2.down;
        if (Input.GetKey(KeyCode.LeftArrow))  shootInput += Vector2.left;
        if (Input.GetKey(KeyCode.RightArrow)) shootInput += Vector2.right;

        if (shootInput != Vector2.zero)
        {
            shootDirection = shootInput.normalized;
            UpdatePlayerSprite();

            // ✅ 不再在这里判断射速！直接调用 Shoot()
            Shoot(); // 让 Shoot 自己决定是否真的发射
        }

        // ===== 处理移动输入（WASD）=====
        float moveH = 0f, moveV = 0f;
        if (Input.GetKey(KeyCode.A)) moveH -= 1;
        if (Input.GetKey(KeyCode.D)) moveH += 1;
        if (Input.GetKey(KeyCode.W)) moveV += 1;
        if (Input.GetKey(KeyCode.S)) moveV -= 1;

        Vector2 moveInput = new Vector2(moveH, moveV).normalized;

        // 移动在 FixedUpdate 中处理，但输入在 Update 采集
        MoveCharacter(moveInput);

        // ===== 处理道具使用（空格键）=====
        if (Input.GetKeyDown(KeyCode.Space) && heldPowerup.HasValue)
        {
            UseHeldPowerup();
        }

        // 进入下一关检测
        if (GameController.Instance.canDetectIsReachBottom && !hasTriggeredNextLevel && transform.position.y <= GameController.Instance.mapBottomY)
        {
            hasTriggeredNextLevel = true; // 🔒 锁住，防止重复触发
            GameController.Instance.canDetectIsReachBottom = false;
            GameController.Instance.OnPlayerReachBottom();
        }
    }

    void DeactivateZombieMode()
    {
        isZombieMode = false;
        if (zombieWalkCoroutine != null)
        {
            StopCoroutine(zombieWalkCoroutine);
            zombieWalkCoroutine = null;
        }

        // 恢复默认贴图
        if (spriteRenderer != null && rightSprite != null)
            spriteRenderer.sprite = rightSprite;

        // 关闭敌人恐惧模式
        Enemy.SetZombieMode(false, null);

        Debug.Log("🧟 僵尸模式结束");
    }

    void MoveCharacter(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        // ✅ 计算当前有效移动速度
        float effectiveMoveSpeed = moveSpeed;
        if (IsCoffeeActiveNow)
        {
            effectiveMoveSpeed *= COFFEE_SPEED_MULTIPLIER;
        }

        if (isZombieMode)
            effectiveMoveSpeed *= 1.5f; // 僵尸速度 +50%

        Vector2 newPosition = (Vector2)transform.position + direction * effectiveMoveSpeed * Time.deltaTime;

        if (!IsPositionBlocked(newPosition))
        {
            transform.position = newPosition;
        }
        else
        {
            TrySlide(direction, newPosition);
        }
    }

    bool IsPositionBlocked(Vector2 position)
    {
        float radius = Mathf.Max(colliderSize.x, colliderSize.y) - skinWidth;
        Collider2D[] results = Physics2D.OverlapCircleAll(position, radius, obstacleLayer);
        return results.Length > 0;
    }

    void TrySlide(Vector2 direction, Vector2 blockedPosition)
    {
        // 尝试仅 X 轴移动
        Vector2 xOnly = new Vector2(blockedPosition.x, transform.position.y);
        if (!IsPositionBlocked(xOnly))
        {
            transform.position = xOnly;
            return;
        }

        // 尝试仅 Y 轴移动
        Vector2 yOnly = new Vector2(transform.position.x, blockedPosition.y);
        if (!IsPositionBlocked(yOnly))
        {
            transform.position = yOnly;
        }
        // 否则完全停止（不移动）
    }

    void UpdatePlayerSprite()
    {
        if (spriteRenderer == null) return;

        // ✅ 僵尸模式下不更新方向贴图（由行走动画控制）
        if (isZombieMode)
            return;

        if (Mathf.Abs(shootDirection.x) > Mathf.Abs(shootDirection.y))
        {
            // 左右为主
            spriteRenderer.sprite = shootDirection.x > 0 ? rightSprite : leftSprite;
        }
        else
        {
            // 上下为主
            spriteRenderer.sprite = shootDirection.y > 0 ? upSprite : downSprite;
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        // ===== 计算有效射速 =====
        float speedMultiplier = 1.0f;
        if (IsMachineGunActiveNow) speedMultiplier *= 4f;
        if (IsShotgunActiveNow) speedMultiplier *= (2f / 3f);

        float effectiveFireRate = fireRate / speedMultiplier;
        if (Time.time < lastFireTime + effectiveFireRate)
            return;

        lastFireTime = Time.time;

        // 🔊 播放射击音效（✅ 放在这里！确保只在真正发射时播放）
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, shootVolume);
        }

        // ===== 清空并复用列表 =====
        tempMainDirections.Clear();
        tempFinalDirections.Clear();

        // ===== 确定主射击方向 =====
        if (isWheelActive)
        {
            // 固定 8 个方向（上下左右 + 四个对角线）
            tempMainDirections.Add(Vector2.up);
            tempMainDirections.Add(Vector2.down);
            tempMainDirections.Add(Vector2.left);
            tempMainDirections.Add(Vector2.right);
            tempMainDirections.Add(new Vector2(1, 1).normalized);
            tempMainDirections.Add(new Vector2(1, -1).normalized);
            tempMainDirections.Add(new Vector2(-1, 1).normalized);
            tempMainDirections.Add(new Vector2(-1, -1).normalized);
        }
        else
        {
            // 使用玩家当前输入方向
            tempMainDirections.Add(shootDirection);
        }

        // ===== 对每个主方向应用霰弹散射（如激活）=====
        foreach (Vector2 mainDir in tempMainDirections)
        {
            if (IsShotgunActiveNow)
            {
                float baseAngle = Mathf.Atan2(mainDir.y, mainDir.x) * Mathf.Rad2Deg;
                tempFinalDirections.Add(DirFromAngle(baseAngle - 15f)); // 左偏
                tempFinalDirections.Add(mainDir);                       // 中心
                tempFinalDirections.Add(DirFromAngle(baseAngle + 15f)); // 右偏
            }
            else
            {
                tempFinalDirections.Add(mainDir);
            }
        }

        // ===== 实例化所有子弹 =====
        foreach (Vector2 dir in tempFinalDirections)
        {
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            Bullet bulletComp = bullet.GetComponent<Bullet>();
            if (bulletComp != null)
            {
                bulletComp.SetDirection(dir);
                bulletComp.SetDamage(pistolDamage);
            }
        }
    }

    // 辅助方法：角度转单位方向向量
    Vector2 DirFromAngle(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    public void TakeDamage(int damage = 1)
    {
        if (isDead || isPlayingDeathAnim || isInvincible) return;

        currentLives -= damage;

        // ✅ 同步到 GameController
        if (GameController.Instance != null)
            GameController.Instance.persistentLives = currentLives;

        OnLivesChanged?.Invoke();

        // if (false)
        if (currentLives < 0)
        {
            isGameOver = true;
            StartCoroutine(PlayGameOverAnimation());
        }
        else
        {
            // ✅ 新增：只要没死，就增加 20 秒时间
            if (GameController.Instance != null)
            {
                GameController.Instance.AddTime(20f);
            }

            StartCoroutine(PlayDeathAnimationThenTriggerRespawn());
        }
    }

    IEnumerator PlayDeathAnimationThenTriggerRespawn()
    {
        isPlayingDeathAnim = true;

        // ===== 1. 立即清空所有敌人 =====
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            // ✅ 只销毁非 Boss 敌人
            if (enemy.enemyType != EnemyType.Boss)
            {
                Destroy(enemy.gameObject);
            }
        }

        // 2. ✅ 清除所有 Collectible 道具（金币、心、未来道具）
        GameObject[] collectibles = GameObject.FindGameObjectsWithTag("Collectible");
        foreach (GameObject item in collectibles)
        {
            Destroy(item);
        }

        // ===== 2. 立即暂停所有生成器 =====
        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (var spawner in spawners)
        {
            spawner.Pause();
        }

        // ===== 3. 隐藏玩家主精灵 =====
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
            isInvincible = false; // 退出无敌
        }

        // ===== 4. 播放死亡动画 =====
        if (deathAnimationFrames != null && deathAnimationFrames.Length > 0)
        {
            deathEffectRenderer.enabled = true;

            foreach (Sprite frame in deathAnimationFrames)
            {
                deathEffectRenderer.sprite = frame;
                yield return new WaitForSeconds(deathAnimationFrameDuration);
            }

            deathEffectRenderer.enabled = false;
        }

        // ===== 5. 动画结束后，通知 GameController 延迟复活 =====
        System.Action respawnCallback = () =>
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.sprite = rightSprite;
            }

            transform.position = GetScreenCenterWorldPosition();

            isPlayingDeathAnim = false;

            // ✅ 启动 1 秒无敌（带闪烁）
            StartCoroutine(StartShortInvincibility(2f));
        };

        if (GameController.Instance != null)
        {
            GameController.Instance.OnPlayerLoseLife(respawnCallback);
        }
        else
        {
            Debug.LogError("[Player] GameController not found! Falling back.");
            yield return new WaitForSeconds(2f);
            respawnCallback?.Invoke();

            // 手动恢复生成器
            foreach (var spawner in spawners)
            {
                if (spawner != null) spawner.Resume();
            }
        }
    }

    Vector3 GetScreenCenterWorldPosition()
    {
        if(isBoss)
            return currentRespawnPosition;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float distance = Mathf.Abs(cam.transform.position.z);
            Vector3 screenCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
            screenCenter.z = 0;
            return screenCenter;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// 启动指定时长的无敌状态（带闪烁），用于复活或道具
    /// </summary>
    IEnumerator StartShortInvincibility(float duration)
    {
        isInvincible = true;
        
        // 如果当前因烟雾弹也在无敌，没关系，统一由 isInvincible 控制闪烁
        
        yield return new WaitForSeconds(duration);
    
        isInvincible = false;
        
        // 确保结束时可见（防止 blinkInterval 刚好停在隐藏相位）
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    void UseHeldPowerup()
    {
        if (!heldPowerup.HasValue) return;

        PowerupType type = heldPowerup.Value;
        bool isInBossBattle = IsInBossBattle(); // 👈 新增：检测 Boss 战

        // ===== 特殊处理：Boss 战中禁用某些道具 =====
        bool isDisabledInBossBattle = isInBossBattle && (
            type == PowerupType.Nuke ||
            type == PowerupType.SmokeGrenade ||
            type == PowerupType.Tombstone
        );

        if (isDisabledInBossBattle)
        {
            Debug.Log($"⚠️ 道具 {type} 在 Boss 战中被禁用！");
            // 但仍要清除道具（模拟“使用了但无效”）
            heldPowerup = null;
            if (GameController.Instance != null)
                GameController.Instance.persistentHeldPowerup = heldPowerup;
            UpdateHeldPowerupUI();
            OnPowerupChanged?.Invoke(heldPowerup);
            return; // 👈 直接返回，不执行后续效果
        }

        // ===== 原有逻辑继续 =====
        float now = Time.time;
        Debug.Log($"✨ 使用道具: {type}");

        switch (type)
        {
            case PowerupType.Wheel:
                isWheelActive = true;
                wheelEndTime = now + POWERUP_DURATION;
                break;

            case PowerupType.MachineGun:
                isMachineGunActive = true;
                machineGunEndTime = now + POWERUP_DURATION;
                break;

            case PowerupType.Shotgun:
                isShotgunActive = true;
                shotgunEndTime = now + POWERUP_DURATION;
                break;

            case PowerupType.Coffee:
                isCoffeeActive = true;
                coffeeEndTime = now + COFFEE_DURATION;
                break;

            case PowerupType.Badge:
                isBadgeActive = true;
                badgeEndTime = now + BADGE_DURATION;
                Debug.Log("🎖️ 警徽激活！");
                break;

            case PowerupType.Nuke:
                UseNuke();
                break;

            case PowerupType.SmokeGrenade:
                PlayUsePowerupSound(type);
                UseSmokeGrenade();
                break;

            case PowerupType.Tombstone:
                PlayUsePowerupSound(type);
                UseTombstone();
                break;

            default:
                Debug.LogWarning($"道具 {type} 的效果尚未实现");
                break;
        }

        // 清空持有状态（正常流程）
        heldPowerup = null;
        if (GameController.Instance != null)
            GameController.Instance.persistentHeldPowerup = heldPowerup;
        UpdateHeldPowerupUI();
        OnPowerupChanged?.Invoke(heldPowerup);
    }

    /// <summary>
    /// 判断当前场景中是否存在存活的 Boss
    /// </summary>
    bool IsInBossBattle()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && 
                !enemy.IsDead && 
                enemy.enemyType == EnemyType.Boss)
            {
                return true;
            }
        }
        return false;
    }

    private void PlayUsePowerupSound(PowerupType type)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = null;

        switch (type)
        {
            case PowerupType.Tombstone:
                clipToPlay = useGraveSound;
                break;
            case PowerupType.SmokeGrenade:
                clipToPlay = useSmokeBombSound;
                break;
            // 可以在这里加更多道具的使用音效
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, usePowerupVolume);
        }
    }

    IEnumerator StartInvincibility()
    {
        isInvincible = true;
        transform.position = spawnPosition; // 立即传送回中心

        yield return new WaitForSeconds(invincibleDuration);

        isInvincible = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true; // 确保可见
    }

    void GameOver()
    {
        isDead = true; // 标记永久死亡
        if (spriteRenderer != null) spriteRenderer.enabled = true; // 显示最终状态
        rb.simulated = false;

        Debug.Log("💀 GAME OVER - Lives exhausted.");

        // 可选：3秒后重启（或加载 Game Over 场景）
        // StartCoroutine(RestartAfterDelay(3f));
    }

    // 可选辅助方法（按需启用）
    /*
    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }
    */

    void Respawn()
    {
        // ✅ 从 GameController 恢复持久化状态
        if (GameController.Instance != null)
        {
            currentLives = GameController.Instance.persistentLives;
            heldPowerup = GameController.Instance.persistentHeldPowerup;
        }
        else
        {
            currentLives = maxLives; // 安全兜底
            heldPowerup = null;
        }

        isInvincible = false;
        isDead = false;

        // ✅ 关键修复：确保 rb 不为 null
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError("Player 缺少 Rigidbody2D 组件！");
                return; // 安全退出，避免崩溃
            }
        }
        
        rb.simulated = true;
        transform.position = currentRespawnPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = rightSprite;
        }

        // 重生后重置为默认点
        // currentRespawnPosition = spawnPosition;

        OnLivesChanged?.Invoke();
        UpdateHeldPowerupUI();
        OnPowerupChanged?.Invoke(heldPowerup);
    }

    void SetupDeathEffectRenderer()
    {
        GameObject effectObj = new GameObject("PlayerDeathEffect");
        effectObj.transform.SetParent(transform);
        effectObj.transform.localPosition = Vector3.zero;
        deathEffectRenderer = effectObj.AddComponent<SpriteRenderer>();
        
        // 继承主渲染器的排序设置
        if (spriteRenderer != null)
        {
            deathEffectRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            deathEffectRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
        
        deathEffectRenderer.enabled = false; // 默认隐藏
    }

    public void Heal(int amount)
    {
        currentLives += amount; // 直接加，无上限！
        Debug.Log($"❤️ 玩家回复 {amount} 点生命，当前: {currentLives} (无上限)");

        // ✅ 同步到 GameController
        if (GameController.Instance != null)
            GameController.Instance.persistentLives = currentLives;

        // 可选：触发 UI 更新（如果你的 UI 显示当前生命）
        OnLivesChanged?.Invoke();
    }

    /// <summary>
    /// 玩家拾取一个道具（会顶替当前持有的）
    /// </summary>
    public void PickUpPowerup(PowerupType type)
    {
        heldPowerup = type;
        Debug.Log($"📦 拾取道具: {type}");

        // ✅ 同步
        if (GameController.Instance != null)
            GameController.Instance.persistentHeldPowerup = heldPowerup;

        // 🔊 播放道具拾取音效
        if (audioSource != null && pickupPowerupSound != null)
        {
            audioSource.PlayOneShot(pickupPowerupSound, pickupVolume);
        }

        UpdateHeldPowerupUI(); // 👈 新增
        OnPowerupChanged?.Invoke(heldPowerup);
    }

    Sprite GetSpriteForPowerup(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.Wheel: return wheelSprite;
            case PowerupType.MachineGun: return machineGunSprite;
            case PowerupType.Nuke: return nukeSprite;
            case PowerupType.Tombstone: return tombstoneSprite;
            case PowerupType.Coffee: return coffeeSprite;
            case PowerupType.Shotgun: return shotgunSprite;
            case PowerupType.SmokeGrenade: return smokeGrenadeSprite;
            case PowerupType.Badge: return badgeSprite;
            default: return null;
        }
    }

    void UpdateHeldPowerupUI()
    {
        if (heldPowerupIcon == null) return;

        if (heldPowerup.HasValue)
        {
            Sprite icon = GetSpriteForPowerup(heldPowerup.Value);
            heldPowerupIcon.sprite = icon;
            heldPowerupIcon.enabled = (icon != null); // 如果没配图就隐藏
        }
        else
        {
            heldPowerupIcon.enabled = false; // 无道具时隐藏
        }
    }

    void UseNuke()
    {
        Debug.Log("💣 核弹启动！全屏清敌（无掉落）");

        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            // 检查是否已被销毁（安全）
            if (enemy == null) continue;

            // 🔊 播放该敌人的随机死亡音效
            if(!enemy.IsDead)
                enemy.PlayRandomDeathSound();

            // 播放自定义死亡动画
            if(!enemy.IsDead)
                StartCoroutine(PlayNukeDeathAnimationAt(enemy.transform.position));

            // 直接销毁，不调用 Die() → 不掉 loot，不播原特效
            Destroy(enemy.gameObject);
        }
    }

    /// <summary>
    /// 在指定位置播放核弹死亡动画（5帧序列）
    /// </summary>
    private System.Collections.IEnumerator PlayNukeDeathAnimationAt(Vector3 position)
    {
        // 安全检查
        if (nukeDeathSprites == null || nukeDeathSprites.Length == 0)
        {
            yield break;
        }

        // 创建临时游戏对象
        GameObject animObj = new GameObject("NukeDeathAnim");
        animObj.transform.position = position;

        // 添加 SpriteRenderer
        SpriteRenderer sr = animObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = nukeEffectSortingLayer; // 可选：确保层级正确
        sr.sortingOrder = 10; // 确保在角色/敌人上方

        // 播放每一帧
        foreach (Sprite sprite in nukeDeathSprites)
        {
            sr.sprite = sprite;
            yield return new WaitForSeconds(nukeDeathFrameDuration);
        }

        // 动画结束，销毁对象
        Destroy(animObj);
    }

    void UseSmokeGrenade()
    {
        Debug.Log("💨 使用烟雾弹！");

        // ===== 1. 记录当前（原）位置 =====
        Vector3 originalPosition = transform.position;

        // ===== 2. 随机传送 =====
        Vector3? newPos = FindRandomValidPosition(maxAttempts: 20);
        if (newPos.HasValue)
        {
            transform.position = newPos.Value;
            Debug.Log($"✅ 传送到: {newPos.Value}");
        }
        else
        {
            Debug.LogWarning("⚠️ 未能找到有效传送点，留在原地");
        }

        // ===== 3. 在原位置播放残留动画 =====
        if (smokeGrenadeResidueSprites != null && smokeGrenadeResidueSprites.Length > 0)
        {
            StartCoroutine(PlaySmokeResidueAnimation(originalPosition));
        }

        // ===== 4. 暂停所有敌人 =====
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.Pause();
            }
        }

        // ===== 5. 激活烟雾效果（无敌+闪烁）=====
        isSmokeActive = true;
        smokeEndTime = Time.time + SMOKE_DURATION;
        StartCoroutine(SmokeEffectCoroutine());
    }

    /// <summary>
    /// 寻找地图内一个随机且非障碍物的位置
    /// </summary>
    Vector3? FindRandomValidPosition(int maxAttempts = 10)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        // 获取屏幕世界坐标边界（假设 Orthographic 相机）
        float screenLeft = cam.ViewportToWorldPoint(Vector3.zero).x/24*14;
        float screenRight = cam.ViewportToWorldPoint(Vector3.right).x/24*14;
        float screenBottom = cam.ViewportToWorldPoint(Vector3.zero).y/24*14;
        float screenTop = cam.ViewportToWorldPoint(Vector3.up).y/24*14;

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(screenLeft + 1f, screenRight - 1f);
            float y = Random.Range(screenBottom + 1f, screenTop - 1f);
            Vector2 pos = new Vector2(x, y);

            // 检查是否被障碍物阻挡
            if (!IsPositionBlocked(pos))
            {
                return new Vector3(x, y, transform.position.z);
            }
        }

        return null; // 未找到
    }

    /// <summary>
    /// 在指定位置播放烟雾弹残留动画（5帧序列）
    /// </summary>
    private IEnumerator PlaySmokeResidueAnimation(Vector3 position)
    {
        if (smokeGrenadeResidueSprites == null || smokeGrenadeResidueSprites.Length == 0)
            yield break;

        // 创建临时游戏对象
        GameObject animObj = new GameObject("SmokeGrenadeResidue");
        animObj.transform.position = position;

        SpriteRenderer sr = animObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = smokeEffectSortingLayer;
        sr.sortingOrder = 5; // 确保在地面之上，玩家之下（可调）

        // 播放每一帧
        foreach (Sprite sprite in smokeGrenadeResidueSprites)
        {
            sr.sprite = sprite;
            yield return new WaitForSeconds(smokeResidueFrameDuration);
        }

        // 动画结束，销毁对象
        Destroy(animObj);
    }

    IEnumerator SmokeEffectCoroutine()
    {
        bool wasInvincible = isInvincible;
        isInvincible = true;

        yield return new WaitForSeconds(SMOKE_DURATION);

        // ===== 恢复状态 =====
        isInvincible = wasInvincible;
        isSmokeActive = false;

        // ✅ 关键修复：如果不再无敌，确保 Sprite 显示
        if (!isInvincible && spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        // 恢复敌人
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.Resume();
            }
        }

        Debug.Log("💨 烟雾效果结束");
    }

    void UseTombstone()
    {
        Debug.Log("⚰️ 使用墓碑！进入僵尸模式");

        StartCoroutine(TombstoneTransformationSequence());
    }

    IEnumerator TombstoneTransformationSequence()
    {
        // ===== 1. 暂停游戏 =====
        Time.timeScale = 0f;

        // ===== 2. 创建全屏遮罩（完全变黑）=====
        GameObject overlay = CreateFullscreenOverlay();

        // ===== 3. 隐藏玩家主精灵，显示替换图片 =====
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        ShowPlayerReplacementImage();

        // ===== 4. 等待一段时间（展示效果）=====
        yield return new WaitForSecondsRealtime(1.0f); // 使用 Realtime，因为 timeScale=0

        // ===== 5. 恢复 =====
        Time.timeScale = 1f;
        Destroy(overlay);
        HidePlayerReplacementImage();
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        // ===== 6. 激活僵尸模式 =====
        ActivateZombieMode();
    }

    private GameObject replacementUIImage = null;

    void ShowPlayerReplacementImage()
    {
        if (tombstonePlayerReplacementSprite == null || Camera.main == null) return;

        // 屏幕坐标
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        if (viewportPos.z <= 0) viewportPos = new Vector3(0.5f, 0.5f, 0);
        Vector2 screenPos = new Vector2(viewportPos.x * Screen.width, viewportPos.y * Screen.height);

        // 创建 UI 根
        GameObject uiRoot = new GameObject("TombstoneReplacementUI");
        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        // 创建 Image
        GameObject imageObj = new GameObject("ReplacementImage");
        imageObj.transform.SetParent(uiRoot.transform);

        Image image = imageObj.AddComponent<Image>();
        image.sprite = tombstonePlayerReplacementSprite;
        image.preserveAspect = true; // 保持比例
        image.raycastTarget = false; // 避免阻挡输入（可选）

        RectTransform rect = image.rectTransform;

        // 🔑 关键：重置锚点为“中心点”，这样 sizeDelta 才表示宽高
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // 设置位置（相对于屏幕中心）
        rect.anchoredPosition = screenPos - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 🔑 方法一：使用 SetNativeSize 获取原始尺寸，再缩放
        image.SetNativeSize(); // 这会把 sizeDelta 设为 Sprite 的“UI 像素尺寸”

        // 现在你可以缩放它！比如放大 1.5 倍
        float desiredScale = 0.45f;
        rect.sizeDelta = new Vector2(
            rect.sizeDelta.x * desiredScale,
            rect.sizeDelta.y * desiredScale
        );

        replacementUIImage = uiRoot;
    }

    void HidePlayerReplacementImage()
    {
        if (replacementUIImage != null)
        {
            Destroy(replacementUIImage);
            replacementUIImage = null;
        }
    }

    /// <summary>
    /// 创建全屏完全黑色遮罩（仅 UI 层）
    /// </summary>
    GameObject CreateFullscreenOverlay()
    {
        GameObject go = new GameObject("TombstoneOverlay");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 确保在最上层

        Image image = go.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 1); // 完全黑色且不透明

        // 自适应屏幕
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    


    void ActivateZombieMode()
    {
        isZombieMode = true;
        zombieEndTime = Time.time + ZOMBIE_DURATION;

        // 启动行走动画
        if (zombieWalkCoroutine != null)
            StopCoroutine(zombieWalkCoroutine);
        zombieWalkCoroutine = StartCoroutine(ZombieWalkAnimation());

        // 通知敌人进入“恐惧模式”
        Enemy.SetZombieMode(true, transform);
    }

    IEnumerator ZombieWalkAnimation()
    {
        if (spriteRenderer == null) yield break;

        bool useLeft = true;
        while (isZombieMode)
        {
            spriteRenderer.sprite = useLeft ? zombieLeftFoot : zombieRightFoot;
            useLeft = !useLeft;
            yield return new WaitForSeconds(zombieStepInterval);
        }

        // 恢复默认朝右
        if (spriteRenderer != null && rightSprite != null)
            spriteRenderer.sprite = rightSprite;
    }

    public void OnPickupCollectible(CollectibleType type)
    {
        if (audioSource == null) return;

        switch (type)
        {
            case CollectibleType.Coin:
            case CollectibleType.Heart:
                if (pickupCollectibleSound != null)
                    audioSource.PlayOneShot(pickupCollectibleSound, pickupVolume);
                break;
            default: // Powerup 等
                if (pickupPowerupSound != null)
                    audioSource.PlayOneShot(pickupPowerupSound, pickupVolume);
                break;
        }
    }

    // ===== 新增：支持动态重生点 =====
    private Vector3 currentRespawnPosition; // 当前生效的重生点

    /// <summary>
    /// 设置下一次重生的位置（例如被 Boss 击中后）
    /// </summary>
    public void SetRespawnPosition(Vector2 position)
    {
        currentRespawnPosition = position;
        isBoss = true;
    }

    IEnumerator PlayGameOverAnimation()
    {
        isDead = true; // 标记永久死亡
        isPlayingDeathAnim = true;

        // 1. 清空敌人
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            Destroy(enemy.gameObject);
        }

        // 2. ✅ 清除所有 Collectible 道具（金币、心、未来道具）
        GameObject[] collectibles = GameObject.FindGameObjectsWithTag("Collectible");
        foreach (GameObject item in collectibles)
        {
            Destroy(item);
        }

        // 2. 暂停生成器
        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (var spawner in spawners)
        {
            spawner.Pause();
        }

        // 3. 隐藏玩家主精灵
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
            isInvincible = false;
        }

        // 4. 播放死亡动画
        if (deathAnimationFrames != null && deathAnimationFrames.Length > 0)
        {
            deathEffectRenderer.enabled = true;
            foreach (Sprite frame in deathAnimationFrames)
            {
                deathEffectRenderer.sprite = frame;
                yield return new WaitForSeconds(deathAnimationFrameDuration);
            }
            deathEffectRenderer.enabled = false;
        }

        // 5. 显示 Game Over UI
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[Player] GameOverCanvas not assigned!");
        }

        // 6. 停止物理模拟
        rb.simulated = false;
    }

    // ===== 调试可视化 =====
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(colliderSize.x, colliderSize.y));
    }
}