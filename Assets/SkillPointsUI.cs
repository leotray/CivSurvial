using TMPro;
using UnityEngine;

/// <summary>
/// Attach this to the TextMeshProUGUI element in your skill tree prefab
/// that should display the current skill points.
/// </summary>
public class SkillPointsUI : MonoBehaviour
{
    public TextMeshProUGUI pointsText;

    private void Reset()
    {
        if (pointsText == null)
            pointsText = GetComponent<TextMeshProUGUI>();
    }

    public void SetPoints(int points)
    {
        if (pointsText != null)
            pointsText.text = $"Skill Points: {points}";
    }
}
