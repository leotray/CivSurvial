using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using FishNet;

public class CityManagementUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text cityNameText;
    [SerializeField] private Button createFactionButton;
    [SerializeField] private Button joinFactionButton;
    [SerializeField] private TMP_Dropdown factionDropdown;
    [SerializeField] private TMP_InputField _factionName;

    private int currentCityId;
    private string currentCityName;

    private void Awake()
    {
        panel.SetActive(false);
    }

    public void Open(int cityId, string cityName)
    {
        panel.SetActive(true);
        currentCityId = cityId;
        currentCityName = cityName;
        cityNameText.text = cityName;

        // Populate the faction dropdown list
        PopulateFactionList();

        // Button listeners
        createFactionButton.onClick.RemoveAllListeners();
        createFactionButton.onClick.AddListener(OnCreateFactionPressed);

        joinFactionButton.onClick.RemoveAllListeners();
        joinFactionButton.onClick.AddListener(OnJoinFactionClicked);

        // Unlock cursor for interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"[CityManagementUI] Opened for city '{cityName}' (ID {cityId})");
    }

    public void Close()
    {
        panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void PopulateFactionList()
    {
        var factionNames = FactionManager.Instance.GetAllFactionNames();
        factionDropdown.ClearOptions();
        factionDropdown.AddOptions(factionNames);
    }

    private void OnCreateFactionPressed()
    {
        Debug.Log($"[CityManagementUI] Creating new faction with capital '{currentCityName}'");

        // Generate a default name (later can be changed to input field)
        string factionName = _factionName.text;

        // Tell server to create faction
        FactionManager.Instance.CreateFactionServer(factionName, currentCityId);

        Close();
    }

    private void OnJoinFactionClicked()
    {
        int selectedIndex = factionDropdown.value;
        int factionId = FactionManager.Instance.GetFactionIdByIndex(selectedIndex);
        var conn = InstanceFinder.ClientManager.Connection;

        Debug.Log($"[CityManagementUI] Requesting to join faction {factionId} for city {currentCityId}");

        FactionManager.Instance.RequestJoinFactionServerRpc(currentCityId, factionId, conn);
        Close();
    }
}
