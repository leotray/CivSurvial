using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConstructionProgressUI : MonoBehaviour
{
    public static ConstructionProgressUI Instance;

    [Header("UI References")]
    [SerializeField] private GameObject progressPanel; // The whole panel (we enable/disable)
    [SerializeField] private Slider progressSlider;    // The slider itself
    [SerializeField] private TMP_Text progressText;    // Optional % text

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (progressPanel != null)
            progressPanel.SetActive(false);
    }

    public void ShowProgress(float percent)
    {
        if (progressPanel == null || progressSlider == null) return;

        progressPanel.SetActive(true);
        progressSlider.value = percent;
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(percent * 100f)}%";
    }

    public void HideProgress()
    {
        if (progressPanel != null)
            progressPanel.SetActive(false);
    }
}
