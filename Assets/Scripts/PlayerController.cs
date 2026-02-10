using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // === 移动参数 ===
    public float moveSpeed = 5f;
    public LayerMask obstacleLayer; // 用于碰撞检测的层（如 Obstacle）

    [Header("Collision Detection")]
    public Vector2 colliderSize = new Vector2(0.35f, 0.35f); // 碰撞检测半径（建议略小于角色）
    public float skinWidth = 0.03f; // 安全边距，防止卡墙

    // === 射击参数 ===
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;
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

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // ✅ 设置出生点为屏幕中心（不是初始位置！）
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

        Respawn(); // 初始化生命和状态

        // 初始化默认朝右
        if (spriteRenderer != null && rightSprite != null)
            spriteRenderer.sprite = rightSprite;

        // 初始化死亡动画专用渲染器
        SetupDeathEffectRenderer();
    }

    void Update()
    {
        // 关键：动画播放或 Game Over 时完全禁用逻辑
        if (isDead || isPlayingDeathAnim)
        {
            // 确保主精灵隐藏（安全兜底）
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            return;
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

            if (Time.time >= lastFireTime + fireRate)
            {
                Shoot();
                lastFireTime = Time.time;
            }
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
    }

    void MoveCharacter(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        Vector2 newPosition = (Vector2)transform.position + direction * moveSpeed * Time.deltaTime;

        if (!IsPositionBlocked(newPosition))
        {
            transform.position = newPosition;
        }
        else
        {
            // 尝试滑墙（优先保持一个轴的移动）
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
        if (bulletPrefab != null && firePoint != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            Bullet bulletComp = bullet.GetComponent<Bullet>();
            if (bulletComp != null)
            {
                bulletComp.SetDirection(shootDirection);
            }
        }
    }

    public void TakeDamage(int damage = 1)
    {
        if (isDead || isPlayingDeathAnim) return;

        currentLives -= damage;

        // 👇 触发生命值变化事件
        OnLivesChanged?.Invoke();

        if (currentLives <= 0)
        {
            StartCoroutine(PlayGameOverAnimation());
        }
        else
        {
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
            Destroy(enemy.gameObject);
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

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(cam.transform.position.z)));
                center.z = 0;
                transform.position = center;
            }

            isPlayingDeathAnim = false;
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
        currentLives = maxLives;
        isInvincible = false;
        isDead = false;
        rb.simulated = true;
        transform.position = spawnPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = rightSprite;
        }

        // 👇 触发 UI 更新
        OnLivesChanged?.Invoke();
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