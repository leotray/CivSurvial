using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using FishNet.Connection;

public class PlayerStatsNetworked : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    private int _currentHealth;
    public HealthBar healthBar;

    [Header("Hunger Settings")]
    public int maxHunger = 100;
    public int currentHunger;
    public HealthBar hungerBar;
    public float hungerDecreaseInterval = 5f;
    public int hungerDecreaseAmount = 1;
    public float hungerTimer;
    public float starvationInterval = 2f;
    public int starvationDamage = 2;
    public float starvationTimer;

    [Header("Stamina Settings")]
    public int maxStamina = 100;
    public int currentStamina;
    public HealthBar staminaBar;
    public float sprintDrainRate = 20f;
    public float moveRegenRate = 7f;
    public float idleRegenRate = 15f;
    public int attackCost = 20;

    [Header("Role Multipliers")]
    public float staminaRegenMultiplier = 1f; // NEW: applied to both idle and move regen

    // Movement flags
    public bool isSprinting;
    public bool isMoving;

    [Header("Combat")]
    public bool isBlocking = false;

    [SerializeField] private GameObject deathScreenCanvas;

    // Sync helpers
    private bool _lastSentIsSprinting = false;
    private bool _lastSentIsMoving = false;
    private float _sendStateAccumulator = 0f;
    private const float SEND_STATE_INTERVAL = 0.1f;

    // Precision mirrors
    private float _staminaFloat;
    private float _hungerFloat;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner)
        {
            if (healthBar != null) healthBar.SetMaxHealth(maxHealth);
            if (hungerBar != null) hungerBar.SetMaxHealth(maxHunger);
            if (staminaBar != null) staminaBar.SetMaxHealth(maxStamina);

            if (healthBar != null) healthBar.SetHealth(_currentHealth);
            if (hungerBar != null) hungerBar.SetHealth(currentHunger);
            if (staminaBar != null) staminaBar.SetHealth(currentStamina);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetHealth(maxHealth);
        currentHunger = maxHunger;
        currentStamina = maxStamina;
        _staminaFloat = currentStamina;
        _hungerFloat = currentHunger;
        ObserversSetHunger(currentHunger);
        ObserversSetStamina(currentStamina);
    }

    private void Update()
    {
        if (IsOwner)
        {
            _sendStateAccumulator += Time.deltaTime;
            bool changed = (isSprinting != _lastSentIsSprinting) || (isMoving != _lastSentIsMoving);
            if (changed || _sendStateAccumulator >= SEND_STATE_INTERVAL)
            {
                ServerReportMovementState(isSprinting, isMoving, Time.deltaTime);
                _lastSentIsSprinting = isSprinting;
                _lastSentIsMoving = isMoving;
                _sendStateAccumulator = 0f;
            }

            if (healthBar != null) healthBar.SetHealth(_currentHealth);
            if (hungerBar != null) hungerBar.SetHealth(currentHunger);
            if (staminaBar != null) staminaBar.SetHealth(currentStamina);
        }

        if (IsServer)
        {
            HandleHunger();
            HandleStarvation();
            ServerStaminaFallbackTick(Time.deltaTime);
        }
    }

    // ---------------- HEALTH ----------------
    [Server]
    private void SetHealth(int value)
    {
        value = Mathf.Clamp(value, 0, maxHealth);
        _currentHealth = value;
        ObserversSetHealth(_currentHealth);
        if (_currentHealth <= 0) Die();
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversSetHealth(int value)
    {
        _currentHealth = value;
        if (IsOwner && healthBar != null) healthBar.SetHealth(_currentHealth);
    }

    [Server]
    public void TakeDamage(int amount, Vector3 attackerPosition, bool canStun)
    {
        if (isBlocking)
        {
            Vector3 toAttacker = attackerPosition - transform.position;
            toAttacker.y = 0;
            float angle = Vector3.Angle(transform.forward, toAttacker);
            if (angle <= 60f) return;
        }
        SetHealth(_currentHealth - amount);
    }

    [Server]
    public void Heal(int amount) => SetHealth(_currentHealth + amount);

    // ---------------- HUNGER ----------------
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
            TakeDamage(starvationDamage, transform.position, false);
        }
    }

    [Server]
    public void ModifyHunger(int amount)
    {
        _hungerFloat += amount;
        _hungerFloat = Mathf.Clamp(_hungerFloat, 0f, maxHunger);
        currentHunger = Mathf.RoundToInt(_hungerFloat);
        ObserversSetHunger(currentHunger);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversSetHunger(int value)
    {
        currentHunger = value;
        if (IsOwner && hungerBar != null) hungerBar.SetHealth(currentHunger);
    }

    // ---------------- STAMINA ----------------
    [Server]
    private void ServerStaminaFallbackTick(float delta)
    {
        ApplyStaminaTick(delta, isSprinting, isMoving);
    }

    [ServerRpc(RequireOwnership = true)]
    private void ServerReportMovementState(bool ownerIsSprinting, bool ownerIsMoving, float ownerDelta)
    {
        isSprinting = ownerIsSprinting;
        isMoving = ownerIsMoving;
        float deltaForTick = Mathf.Clamp(ownerDelta, 0.001f, 0.5f);
        ApplyStaminaTick(deltaForTick, isSprinting, isMoving);
        ObserversSetStamina(currentStamina);
    }

    [Server]
    private void ApplyStaminaTick(float deltaTime, bool serverIsSprinting, bool serverIsMoving)
    {
        if (_staminaFloat <= 0f) _staminaFloat = 0f;

        if (serverIsSprinting && _staminaFloat > 0f)
        {
            _staminaFloat -= sprintDrainRate * deltaTime;
        }
        else
        {
            if (serverIsMoving)
                _staminaFloat += moveRegenRate * staminaRegenMultiplier * deltaTime;
            else
                _staminaFloat += idleRegenRate * staminaRegenMultiplier * deltaTime;
        }

        _staminaFloat = Mathf.Clamp(_staminaFloat, 0f, maxStamina);
        int newStam = Mathf.RoundToInt(_staminaFloat);
        if (newStam != currentStamina)
        {
            currentStamina = newStam;
            ObserversSetStamina(currentStamina);
        }
    }

    [Server]
    public void ModifyStamina(int amount)
    {
        _staminaFloat += amount;
        _staminaFloat = Mathf.Clamp(_staminaFloat, 0f, maxStamina);
        currentStamina = Mathf.RoundToInt(_staminaFloat);
        ObserversSetStamina(currentStamina);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversSetStamina(int value)
    {
        currentStamina = value;
        if (IsOwner && staminaBar != null) staminaBar.SetHealth(currentStamina);
    }

    public bool HasStaminaForAttack() => currentStamina >= attackCost;

    public void UseStaminaForAttack()
    {
        if (IsServer) ModifyStamina(-attackCost);
        else ServerSpendStamina(attackCost);
    }
    public void UseStaminaForAttack(int customCost)
    {
        if (IsServer) ModifyStamina(-customCost);
        else ServerSpendStamina(customCost);
    }


    [ServerRpc(RequireOwnership = true)]
    private void ServerSpendStamina(int amount) => ModifyStamina(-amount);

    // ---------------- DEATH ----------------
    [Server]
    private void Die() => Debug.Log("[PlayerStatsNetworked] Player died!");
}
