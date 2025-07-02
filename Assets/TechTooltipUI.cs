using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class TechTooltipUI : MonoBehaviour
{
    public Text titleText;
    public Text descriptionText;
    public Text recipeListText;
    public GameObject background;

    public RectTransform panelRoot; // Assign this to the whole panel for toggling

    private static TechTooltipUI instance;

    void Awake()
    {
        instance = this;
        Hide(); // Hide on start
    }

    public static void ShowTooltip(Tech tech)
    {
        if (instance == null) return;

        instance.panelRoot.gameObject.SetActive(true);
        instance.background.gameObject.SetActive(true);
        instance.titleText.text = tech.techName;
        instance.descriptionText.text = tech.description;

        StringBuilder sb = new StringBuilder();
        foreach (var recipe in tech.unlockedRecipes)
        {
            if (recipe != null && recipe.result != null)
                sb.AppendLine("• " + recipe.result.itemName);
        }

        instance.recipeListText.text = sb.Length > 0 ? sb.ToString() : "No recipes unlocked.";
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.panelRoot.gameObject.SetActive(false);
            instance.background.gameObject.SetActive(false);
        }
            
    }
}
