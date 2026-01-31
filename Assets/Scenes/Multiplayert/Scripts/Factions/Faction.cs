// Faction.cs
using System;
using System.Collections.Generic;
using Unity;
using UnityEngine;

// Faction.cs
[Serializable]
public class Faction
{
    public string FactionName;
    public ulong OwnerClientId;
    public int CapitalCityId;

    public List<ulong> Members;
    public List<ulong> Invited;

    public List<string> UnlockedTechIds;
    public List<string> KnownRecipes;
    public int TechPoints;

    // ✅ Add claimed chunks here
    public List<Vector2Int> ClaimedChunks;

    public Faction()
    {
        FactionName = string.Empty;
        OwnerClientId = 0;
        Members = new List<ulong>();
        Invited = new List<ulong>();
        UnlockedTechIds = new List<string>();
        KnownRecipes = new List<string>();
        TechPoints = 0;
        ClaimedChunks = new List<Vector2Int>();
    }

    public Faction(string factionName, ulong ownerClientId, int initialTechPoints = 0)
    {
        FactionName = factionName;
        OwnerClientId = ownerClientId;

        Members = new List<ulong> { ownerClientId };
        Invited = new List<ulong>();
        UnlockedTechIds = new List<string>();
        KnownRecipes = new List<string>();
        TechPoints = initialTechPoints;
        ClaimedChunks = new List<Vector2Int>();
    }
}

