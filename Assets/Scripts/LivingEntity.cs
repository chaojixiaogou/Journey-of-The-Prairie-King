// Assets/Scripts/LivingEntity.cs
using UnityEngine;

public abstract class LivingEntity : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int currentHealth;

    public System.Action onDeath; // 死亡事件

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        onDeath?.Invoke(); // 触发死亡事件
        Destroy(gameObject);
    }
}