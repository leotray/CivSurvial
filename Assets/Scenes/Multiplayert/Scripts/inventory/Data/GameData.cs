using System.Collections.Generic;
using UnityEngine;

public static class GameData
{
    private static Dictionary<string, ItemData> itemDatabase = new();

    // Load items at game start
    public static void LoadItems(List<ItemData> items)
    {
        itemDatabase.Clear();
        foreach (var item in items)
        {
            itemDatabase[item.guid] = item;
        }
    }

    public static ItemData LookupItem(string guid)
    {
        itemDatabase.TryGetValue(guid, out var item);
        return item;
    }
}
