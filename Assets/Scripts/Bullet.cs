// Assets/Scripts/Bullet.cs
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 25;
    public float speed = 8f;
    private Vector2 direction;

    // 👇 新增：标记是否来自 Boss
    public bool isFromBoss = false;

    private static readonly float MAP_LEFT = -8f;
    private static readonly float MAP_RIGHT = 8f;
    private static readonly float MAP_TOP = 8f;
    private static readonly float MAP_BOTTOM = -8f;

    public LayerMask bulletWallLayer;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    // 👇 新增：允许外部设置伤害（关键！）
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    void Update()
    {
        Vector3 direction_detect = direction;
        float distance = speed * Time.deltaTime;

        // 射线检测前方是否有 BulletWall
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            direction_detect,
            distance,
            bulletWallLayer
        );

        if (hit.collider != null)
        {
            // 撞到实心墙，销毁子弹
            Destroy(gameObject);
            return;
        }

        transform.Translate(direction * speed * Time.deltaTime);
        // ✅ 检查是否飞出地图
        if (transform.position.x < MAP_LEFT ||
            transform.position.x > MAP_RIGHT ||
            transform.position.y < MAP_BOTTOM ||
            transform.position.y > MAP_TOP)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Destroy(gameObject, 3f); // 自动销毁
    }

    // 子弹只负责“触发”，不处理销毁逻辑
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            
            Enemy enemy = other.GetComponent<Enemy>();
            if(!isFromBoss){
                if (enemy != null)
                {
                    int enemyCurrentHealth = enemy.GetCurrentHealth();
                    enemy.TakeDamage(damage);
                    if(damage - enemyCurrentHealth > 0)
                    {
                        damage -= enemyCurrentHealth;
                    }
                    else
                    {
                    Destroy(gameObject); // 击中后消失
                    }
                }
            }
        }
        // 👇 新增：打到玩家
        else if (other.CompareTag("Player") && isFromBoss)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(1);

                // ✅ 如果是 Boss 子弹，设置特殊重生点
                if (isFromBoss)
                {
                    // 地图中心偏上：比如 (0, 6)
                    player.SetRespawnPosition(new Vector2(0f, 5f));
                }

                Destroy(gameObject); // 子弹消失
            }
        }
    }
}