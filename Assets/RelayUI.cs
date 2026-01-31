using UnityEngine;
using UnityEngine.UI;

public class RelayUI : MonoBehaviour
{
    [Tooltip("Drag your GameNetworkManager here")]
    public GameNetworkManager networkManager;

    [Tooltip("Input field where the player pastes the join code")]
    public InputField joinCodeInput;

    [Tooltip("Optional: Text element to show the host's join code after creating a relay")]
    public Text hostCodeText;

    public void OnHostClicked()
    {
        if (networkManager == null)
        {
            Debug.LogError("[RelayUI] networkManager is not assigned in the inspector.");
            return;
        }
        Debug.Log("[RelayUI] Host button clicked.");
        networkManager.StartHost();
    }

    public void OnJoinClicked()
    {
        if (networkManager == null)
        {
            Debug.LogError("[RelayUI] networkManager is not assigned in the inspector.");
            return;
        }
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[RelayUI] Join code is empty.");
            return;
        }
        Debug.Log($"[RelayUI] Join button clicked. code={code}");
        networkManager.StartClient(code);
    }

    // Called by GameNetworkManager to display join code on the HUD (optional)
    public void ShowJoinCode(string code)
    {
        if (hostCodeText != null) hostCodeText.text = $"Join code: {code}";
    }
}

