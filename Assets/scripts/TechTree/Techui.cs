using UnityEngine;

public class Techui : MonoBehaviour
{
    public TechTreeManager manager;

    void Start()
    {
        foreach (var techButton in FindObjectsOfType<TechButton>(true)) // ✅ include inactive
        {
            if (techButton.gameObject.activeSelf)
                techButton.Init(manager);
        }
    }
}
