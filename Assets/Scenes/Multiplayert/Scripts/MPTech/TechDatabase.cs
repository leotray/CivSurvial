// TechDatabase.cs
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "TechDatabase", menuName = "Tech/TechDatabase")]
public class TechDatabase : ScriptableObject
{
    public MPtech[] techs;

    public MPtech GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return techs?.FirstOrDefault(t => t != null && t.techId == id);
    }

    public string[] GetAllTechIds()
    {
        if (techs == null) return new string[0];
        return techs.Where(t => t != null && !string.IsNullOrEmpty(t.techId)).Select(t => t.techId).ToArray();
    }
}
