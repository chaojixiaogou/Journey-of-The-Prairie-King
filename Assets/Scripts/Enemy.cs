// Assets/Scripts/Enemy.cs
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHealth = 50;
    private int currentHealth;
    public float speed = 2f;
    public int damageToPlayer = 10; // 碰到玩家造成伤害

    private Transform player;

    void Start()
    {
        currentHealth = maxHealth;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (player != null)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            transform.Translate(dir * speed * Time.deltaTime);
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

    // 如果敌人碰到玩家（可选）
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("1111");
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