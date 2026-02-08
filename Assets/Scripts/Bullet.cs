// Assets/Scripts/Bullet.cs
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 25;
    public float speed = 8f;
    private Vector2 direction;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
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
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject); // 击中后消失
            }
        }
    }
}