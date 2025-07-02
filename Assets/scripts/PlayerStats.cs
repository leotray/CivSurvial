using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int currentHealth;
    public HealthBar healthBar;

    [Header("Hunger Settings")]
    public int maxHunger = 100;
    public int currentHunger;
    public HealthBar hungerBar;

    public float hungerDecreaseInterval = 5f;   // Time between hunger loss
    public int hungerDecreaseAmount = 1;        // Amount of hunger lost
    private float hungerTimer;

    public float starvationInterval = 2f;       // Time between starvation damage
    public int starvationDamage = 2;            // Damage from starvation
    private float starvationTimer;

    [Header("Healing from Food")]
    public int minHungerToAllowHealing = 50;    // Only heal if hunger >= this

    [Header("Combat")]
    public bool isBlocking = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentHunger = maxHunger;
    }

    private void Start()
    {
        healthBar.SetMaxHealth(maxHealth);
        healthBar.SetHealth(currentHealth);

        hungerBar.SetMaxHealth(maxHunger);
        hungerBar.SetHealth(currentHunger);
    }

    private void Update()
    {
        HandleHunger();
        HandleStarvation();
    }

    private void HandleHunger()
    {
        hungerTimer += Time.deltaTime;

        if (hungerTimer >= hungerDecreaseInterval)
        {
            hungerTimer = 0f;
            ModifyHunger(-hungerDecreaseAmount);
        }
    }

    private void HandleStarvation()
    {
        if (currentHunger > 0) return;

        starvationTimer += Time.deltaTime;

        if (starvationTimer >= starvationInterval)
        {
            starvationTimer = 0f;
            TakeDamage(starvationDamage, transform.position); // Starvation damage
        }
    }

    public void TakeDamage(int amount, Vector3 attackerPosition)
    {
        if (isBlocking)
        {
            Vector3 toAttacker = attackerPosition - transform.position;
            toAttacker.y = 0;

            float angle = Vector3.Angle(transform.forward, toAttacker);

            if (angle <= 60f)
            {
                Debug.Log("Blocked attack from angle: " + angle);
                return;
            }
            else
            {
                Debug.Log("Hit from behind/side, not blocked. Angle: " + angle);
            }
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        healthBar.SetHealth(currentHealth);
        Debug.Log("Player took damage! Current Health: " + currentHealth);

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        healthBar.SetHealth(currentHealth);
        Debug.Log("Player healed! Current Health: " + currentHealth);
    }

    public void ModifyHunger(int amount)
    {
        if (amount > 0 && currentHunger >= maxHunger)
        {
            Debug.Log("Hunger is full, skipping hunger increase.");
            return;
        }

        currentHunger += amount;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
        hungerBar.SetHealth(currentHunger);

        Debug.Log($"Hunger changed by {amount}, now at: {currentHunger}");
    }

    public void TryHealFromFood(int healAmount)
    {
        Heal(healAmount); // Always heal regardless of hunger

        if (currentHunger >= maxHunger)
        {
            Debug.Log("Hunger is already full, skipping hunger gain.");
            return;
        }

        ModifyHunger(healAmount);
    }

    [SerializeField] private GameObject deathScreenCanvas;  // Drag UI Canvas here

    private void Die()
    {
        Debug.Log("Player died!");
        Time.timeScale = 0f; // Pause the game
        deathScreenCanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }

}
