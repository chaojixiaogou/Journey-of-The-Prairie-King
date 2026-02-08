// Assets/Scripts/PlayerController.cs
using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    // 移动
    public float moveSpeed = 5f;
    
    // 射击
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;
    
    // 血量 & 重生
    public int maxHealth = 100;
    private int currentHealth;
    public float respawnDelay = 2f;
    private Vector3 spawnPosition;
    private bool isDead = false;

    private Vector2 shootDirection = Vector2.up;
    private float lastFireTime;

    void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (isDead) return;
    
        // === 移动：仅 WASD ===
        float moveH = 0f;
        float moveV = 0f;
        if (Input.GetKey(KeyCode.A)) moveH -= 1;
        if (Input.GetKey(KeyCode.D)) moveH += 1;
        if (Input.GetKey(KeyCode.W)) moveV += 1;
        if (Input.GetKey(KeyCode.S)) moveV -= 1;
        Vector2 moveInput = new Vector2(moveH, moveV).normalized;
    
        // === 射击：仅方向键 ===
        Vector2 shootInput = Vector2.zero;
        if (Input.GetKey(KeyCode.UpArrow))    shootInput += Vector2.up;
        if (Input.GetKey(KeyCode.DownArrow))  shootInput += Vector2.down;
        if (Input.GetKey(KeyCode.LeftArrow))  shootInput += Vector2.left;
        if (Input.GetKey(KeyCode.RightArrow)) shootInput += Vector2.right;
    
        if (shootInput != Vector2.zero)
        {
            shootDirection = shootInput.normalized;
            if (Time.time >= lastFireTime + fireRate)
            {
                Shoot();
                lastFireTime = Time.time;
            }
        }
    
        // 执行移动
        transform.Translate(moveInput * moveSpeed * Time.deltaTime);
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            bullet.GetComponent<Bullet>().SetDirection(shootDirection);
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
        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        transform.position = spawnPosition;
        currentHealth = maxHealth;
        isDead = false;
        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Collider2D>().enabled = true;
    }
}