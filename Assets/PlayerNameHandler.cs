using UnityEngine;
using UnityEngine.UI;



    public class PlayerNameHandler : MonoBehaviour
    {
        // Static variable to hold the player's chosen name (default to empty string).
        public static string PlayerName = "";

        [SerializeField] private InputField nameInputField;

        private void Awake()
        {
            // If an InputField is assigned, register a listener to update the PlayerName when editing ends.
            if (nameInputField != null)
            {
                nameInputField.onEndEdit.AddListener(SetPlayerName);
                Debug.Log($"[PlayerNameHandler] Awake: Listening to end-edit on '{nameInputField.name}'.");
            }
            else
            {
                Debug.LogWarning($"[PlayerNameHandler] Awake: nameInputField not assigned in {gameObject.name}.");
            }
        }

        private void Start()
        {
            // Set initial value if input field has text
            if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            {
                SetPlayerName(nameInputField.text);
            }
        }

        // Called when the player finishes editing the name input field.
        public void SetPlayerName(string name)
        {
            Debug.Log($"[PlayerNameHandler] SetPlayerName called with '{name}'.");
            if (!string.IsNullOrWhiteSpace(name))
            {
                PlayerName = name.Trim();
                Debug.Log($"[PlayerNameHandler] PlayerName set to '{PlayerName}'.");
            }
            else
            {
                Debug.Log($"[PlayerNameHandler] Input was blank or whitespace. Keeping PlayerName as '{PlayerName}'.");
            }
        }

        // Optional: Method to get current player name
        public string GetCurrentPlayerName()
        {
            return PlayerName;
        }

        // Optional: Method to reset player name
        public void ResetPlayerName()
        {
            PlayerName = "";
            if (nameInputField != null)
            {
                nameInputField.text = "";
            }
            Debug.Log("[PlayerNameHandler] Player name reset.");
        }
    }

