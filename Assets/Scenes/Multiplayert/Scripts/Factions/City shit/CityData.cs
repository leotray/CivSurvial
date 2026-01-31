using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CityData
{
    public int CityId;
    public string CityName;
    public int OwningFactionId;   // link back to faction
    public Vector3 Position;
    public ulong LeaderId;
    public string LeaderName;

    public bool CityHallBuilt;
    public List<ulong> Citizens;   // player IDs in THIS city only
    public List<Vector2Int> ClaimedChunks;

    public CityData(int cityId, string cityName, int owningFactionId, Vector3 pos)
    {
        CityId = cityId;
        CityName = cityName;
        OwningFactionId = owningFactionId;
        Position = pos;

        CityHallBuilt = false;
        Citizens = new List<ulong>();
        ClaimedChunks = new List<Vector2Int>();
    }
}
