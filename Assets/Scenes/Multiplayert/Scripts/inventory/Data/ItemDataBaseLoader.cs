using UnityEngine;
using System.Collections.Generic;

public class ItemDataBaseLoader : MonoBehaviour
{
    [Header("Assign all item assets here")]
    public List<ItemData> allItems;

    private void Awake()
    {
        GameData.LoadItems(allItems);
        Debug.Log($"[GameData] Loaded {allItems.Count} items.");
    }
}
