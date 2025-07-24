using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // ✅ This is what you're missing!
using System.Collections.Generic;


public class RespawnButtonHandler : MonoBehaviour
{
    public PlayerStats playerStats;
    public GameObject deathScreenCanvas;
    public Transform spawnPoint;


    public void OnRespawnClicked()
    {
        // Temporarily disable movement
        var movement = playerStats.GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        // Reset stats
        playerStats.currentHealth = playerStats.maxHealth;
        playerStats.currentHunger = playerStats.maxHunger;
        playerStats.healthBar.SetHealth(playerStats.maxHealth);
        playerStats.hungerBar.SetHealth(playerStats.maxHunger);

        // Move the **entire player GameObject**
        GameObject playerObject = playerStats.gameObject; // or .transform.root.gameObject if needed
        playerObject.transform.position = spawnPoint.position;
        playerObject.transform.rotation = spawnPoint.rotation;

        Debug.Log("Spawned at position of spawn point: " + spawnPoint.position);

        // Re-enable movement after frame delay
        StartCoroutine(ReenableMovement(movement));

        // Hide death screen and resume game
        deathScreenCanvas.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator ReenableMovement(PlayerMovement movement)
    {
        yield return null; // Wait 1 frame
        if (movement != null) movement.enabled = true;
    }

}
