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
    public int maxHealth = 100;
    private int currentHealth;
    public float respawnDelay = 2f;
    private Vector3 spawnPosition;
    private bool isDead = false;

    // === 内部引用 ===
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 shootDirection = Vector2.right; // 默认朝右

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 强制设为 Kinematic（由脚本控制移动）
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        currentHealth = maxHealth;
        spawnPosition = transform.position;

        // 初始化默认朝右
        if (spriteRenderer != null && rightSprite != null)
            spriteRenderer.sprite = rightSprite;
    }

    void Update()
    {
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

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        rb.simulated = false; // 禁用物理
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        transform.position = spawnPosition;
        currentHealth = maxHealth;
        isDead = false;
        rb.simulated = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = rightSprite; // 重生默认朝右
        }
    }

    // ===== 调试可视化 =====
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(colliderSize.x, colliderSize.y));
    }
}