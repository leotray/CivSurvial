using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ElectionUIManager : MonoBehaviour
{
    [SerializeField] private Text winnerText;

    private void Awake()
    {
        if (winnerText != null)
            winnerText.gameObject.SetActive(false);
    }

    public void ShowWinnerAnnouncement(string message)
    {
        if (winnerText == null)
        {
            Debug.LogError("[ElectionUIManager] Winner Text not assigned!");
            return;
        }

        winnerText.text = message;
        winnerText.gameObject.SetActive(true);
        StartCoroutine(HideAfterDelay(5f));
    }

    public void HideWinnerAnnouncement()
    {
        if (winnerText != null)
            winnerText.gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideWinnerAnnouncement();
    }
}
