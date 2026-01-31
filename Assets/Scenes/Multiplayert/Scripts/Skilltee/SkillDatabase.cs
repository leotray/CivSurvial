using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Skills/SkillDatabase")]
public class SkillDatabase : ScriptableObject
{
    public Skill[] skills;

    public Skill GetById(string id) => skills.FirstOrDefault(s => s.skillId == id);
    public Skill[] GetSkillsForRole(string roleId) =>
        skills.Where(s => s.roleId == roleId).ToArray();
}
