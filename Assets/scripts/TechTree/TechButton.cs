using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TechButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Tech tech;
    public Button button;
    public Text label;
    public GameObject[] toRevealOnUnlock;

    private TechTreeManager manager;

    public void Init(TechTreeManager managerRef)
    {
        manager = managerRef;
        label.text = $"{tech.techName}\nCost: {tech.cost}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
        button.interactable = !manager.IsTechUnlocked(tech);
    }

    public void OnClick()
    {
        if (manager.TryPurchaseTech(tech))
        {
            button.interactable = false;

            foreach (GameObject go in toRevealOnUnlock)
            {
                go.SetActive(true);
                TechButton tb = go.GetComponent<TechButton>();
                if (tb != null)
                    tb.Init(manager);
            }

            CraftingUI ui = FindObjectOfType<CraftingUI>();
            if (ui != null)
                ui.RefreshUI();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TechTooltipUI.ShowTooltip(tech);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TechTooltipUI.Hide();
    }
}
