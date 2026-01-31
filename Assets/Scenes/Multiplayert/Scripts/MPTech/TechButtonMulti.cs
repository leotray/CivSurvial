// TechButtonMulti.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TechButtonMulti : MonoBehaviour
{
    public TextMeshProUGUI label;
    public Button button;
    public Image icon;

    private MPtech tech;
    private int factionId;
    private bool isLeader; // <-- add this field
    /// <summary>
    /// Initialize the button for a tech. If tech is null we create a disabled placeholder.
    /// </summary>
    public void Init(MPtech t, bool isUnlocked, bool isLeader, int currentPoints, int factionId)
    {
        this.tech = t;
        this.factionId = factionId;
        this.isLeader = isLeader;
        button.onClick.RemoveAllListeners();

        if (t == null)
        {
            label.text = "UNKNOWN TECH";
            if (icon != null) icon.enabled = false;
            button.interactable = false;
            return;
        }

        label.text = $"{t.techName}\nCost: {t.cost}";
        if (t.icon != null && icon != null)
        {
            icon.enabled = true;
            icon.sprite = t.icon;
        }

        if (isUnlocked)
        {
            label.text += "\n(UNLOCKED)";
            button.interactable = false;
        }
        else
        {
            // Check if prerequisites are unlocked
            bool prereqsMet = true;
            if (t.prerequisites != null && t.prerequisites.Count > 0)
            {
                foreach (var prereq in t.prerequisites)
                {
                    if (prereq == null) continue;
                    if (!FactionManager.Instance.GetFactionUnlocked(factionId).Contains(prereq.techId))
                    {
                        prereqsMet = false;
                        break;
                    }
                }
            }

            // Button can only be pressed if leader, points >= cost, and prerequisites met
            bool canBuy = isLeader && currentPoints >= t.cost && prereqsMet;
            button.interactable = canBuy;
            if (canBuy)
                button.onClick.AddListener(OnClick);
        }

    }

    public void OnClick()
    {
        if (!isLeader) return;

        int factionId = FactionManager.Instance.GetMyFactionId();
        if (factionId >= 0)
        {
            FactionManager.Instance.UnlockTechServerRpc(factionId, tech.techId);
        }
    }

}
