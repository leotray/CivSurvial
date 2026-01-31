using UnityEngine;
using UnityEngine.UI;

public class RoleSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rolePanel;       // The full panel you want to show/hide
    [SerializeField] private GameObject buttonPrefab;    // Button prefab
    [SerializeField] private Transform buttonContainer;  // Where buttons will spawn

    private PlayerRole playerRole;

    private void Awake()
    {
        if (rolePanel != null)
            rolePanel.SetActive(false); // Make sure panel starts hidden
    }

    /// <summary>
    /// Opens the role panel and populates it with role buttons.
    /// Called from PlayerRole.OnStartClient for the local player only.
    /// </summary>
    public void Open(RoleDatabase db, PlayerRole roleComp)
    {
        if (rolePanel == null || buttonPrefab == null || buttonContainer == null)
        {
            Debug.LogWarning("[RoleSelectionUI] Missing UI references.");
            return;
        }

        Debug.Log("[RoleSelectionUI] opening the UI");
        rolePanel.SetActive(true);
        playerRole = roleComp;

        // Clear old buttons
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        // Create buttons for each role
        for (int i = 0; i < db.roles.Length; i++)
        {
            int index = i;
            RoleData role = db.roles[i];

            GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
            Button btn = btnObj.GetComponent<Button>();

            // Set button text (role name + optional description)
            Text btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = role.roleName;

            // Assign click event
            btn.onClick.AddListener(() =>
            {
                playerRole.SelectRoleServerRpc(index); // Tell server which role we picked
                Close();
            });
        }
    }

    /// <summary>
    /// Closes the role selection panel
    /// </summary>
    public void Close()
    {
        if (rolePanel != null)
            rolePanel.SetActive(false);
    }
}
