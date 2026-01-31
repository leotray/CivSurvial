using UnityEngine;

[CreateAssetMenu(menuName = "Roles/Role Database", fileName = "RoleDatabase")]
public class RoleDatabase : ScriptableObject
{
    public RoleData[] roles;
}
