using System.Collections.Generic;

[System.Serializable]
public class WarData
{
    public int AttackerFactionId;
    public int DefenderFactionId;

    // track capture progress or city ownership changes later
    public Dictionary<int, int> CityDamage = new(); // cityId -> damage dealt by attacker

    public WarData(int attackerId, int defenderId)
    {
        AttackerFactionId = attackerId;
        DefenderFactionId = defenderId;
    }

    public bool Involves(int a, int b)
    {
        return (AttackerFactionId == a && DefenderFactionId == b) ||
               (AttackerFactionId == b && DefenderFactionId == a);
    }
}
