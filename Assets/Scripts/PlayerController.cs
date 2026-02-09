using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    // === 移动 ===
    public float moveSpeed = 5f;

    // === 射击 ===
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;

    // === 射击方向对应的精灵 ===
    public Sprite upSprite;
    public Sprite downSprite;
    public Sprite leftSprite;
    public Sprite rightSprite;

    // === 血量 & 重生 ===
    public int maxHealth = 100;
    private int currentHealth;
    public float respawnDelay = 2f;
    private Vector3 spawnPosition;
    private bool isDead = false;

    // === 内部状态 ===
    private Vector2 shootDirection = Vector2.right;
    private float lastFireTime;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 初始化默认朝右
        if (spriteRenderer != null && rightSprite != null)
            spriteRenderer.sprite = rightSprite; 
    }

    void Update()
    {
        if (isDead) return; // 防止死亡后操作（注意变量名修正）

        // ===== 移动：仅 WASD =====
        float moveH = 0f;
        float moveV = 0f;
        if (Input.GetKey(KeyCode.A)) moveH -= 1;
        if (Input.GetKey(KeyCode.D)) moveH += 1;
        if (Input.GetKey(KeyCode.W)) moveV += 1;
        if (Input.GetKey(KeyCode.S)) moveV -= 1;
        Vector2 moveInput = new Vector2(moveH, moveV).normalized;

        // ===== 射击输入：仅方向键 =====
        Vector2 shootInput = Vector2.zero;
        if (Input.GetKey(KeyCode.UpArrow))    shootInput += Vector2.up;
        if (Input.GetKey(KeyCode.DownArrow))  shootInput += Vector2.down;
        if (Input.GetKey(KeyCode.LeftArrow))  shootInput += Vector2.left;
        if (Input.GetKey(KeyCode.RightArrow)) shootInput += Vector2.right;

        // 更新射击方向和精灵
        if (shootInput != Vector2.zero)
        {
            shootDirection = shootInput.normalized;
            UpdatePlayerSprite(); // 切换图片

            if (Time.time >= lastFireTime + fireRate)
            {
                Shoot();
                lastFireTime = Time.time;
            }
        }

        // 执行移动
        transform.Translate(moveInput * moveSpeed * Time.deltaTime);
    }

    void UpdatePlayerSprite()
    {
        if (spriteRenderer == null) return;

        // 八向简化为四向（优先轴向）
        if (Mathf.Abs(shootDirection.x) > Mathf.Abs(shootDirection.y))
        {
            // 左右为主
            if (shootDirection.x > 0)
                spriteRenderer.sprite = rightSprite;
            else
                spriteRenderer.sprite = leftSprite;
        }
        else
        {
            // 上下为主
            if (shootDirection.y > 0)
                spriteRenderer.sprite = upSprite;
            else
                spriteRenderer.sprite = downSprite;
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

    // 被敌人调用
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
        isDead = true;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        transform.position = spawnPosition;
        currentHealth = maxHealth;
        isDead = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = rightSprite; 
        }

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
    }
}