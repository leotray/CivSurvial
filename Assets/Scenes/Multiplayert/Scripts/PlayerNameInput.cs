using UnityEngine;
using UnityEngine.UI;

namespace CivSurvival
{
    public class PlayerNameInput : MonoBehaviour
    {
        public InputField nameInputField;
        public static string PlayerName { get; private set; } = "Player";

        private void Awake()
        {
            // Load saved name if exists
            if (PlayerPrefs.HasKey("PlayerName"))
            {
                PlayerName = PlayerPrefs.GetString("PlayerName");
                nameInputField.text = PlayerName;
            }

            nameInputField.onValueChanged.AddListener(OnNameChanged);
        }

        private void OnNameChanged(string newName)
        {
            PlayerName = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;
            PlayerPrefs.SetString("PlayerName", PlayerName);
        }
    }
}
