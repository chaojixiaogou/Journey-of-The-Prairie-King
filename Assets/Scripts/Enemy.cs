using UnityEngine;

public class Enemy : MonoBehaviour
{
    // === 血量 ===
    public int maxHealth = 50;
    private int currentHealth;
    public int damageToPlayer = 10;

    // === 移动 ===
    public float speed = 2f;

    // === 行走动画 ===
    public Sprite walkLeft;   // 迈左脚
    public Sprite walkRight;  // 迈右脚
    public float walkCycleTime = 0.3f; // 切换间隔（秒）

    // === 内部状态 ===
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private float walkTimer = 0f;
    private bool isWalking = false;

    void Start()
    {
        currentHealth = maxHealth;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 初始化默认显示（比如右脚）
        if (spriteRenderer != null && walkRight != null)
        {
            spriteRenderer.sprite = walkRight;
        }
    }

    void Update()
    {
        if (player != null)
        {
            // 计算方向
            Vector2 dir = (player.position - transform.position).normalized;
            
            // 移动
            transform.Translate(dir * speed * Time.deltaTime);

            // 标记正在移动
            isWalking = true;
        }
        else
        {
            isWalking = false;
        }

        // 行走动画
        if (isWalking)
        {
            walkTimer += Time.deltaTime;
            if (walkTimer >= walkCycleTime)
            {
                // 切换 Sprite
                if (spriteRenderer.sprite == walkLeft)
                    spriteRenderer.sprite = walkRight;
                else
                    spriteRenderer.sprite = walkLeft;

                walkTimer = 0f;
            }
        }
    }

    // 被子弹调用
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

    // 碰到玩家时造成伤害
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
}