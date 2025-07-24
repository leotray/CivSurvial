using UnityEngine;

public class MountableHorse : MonoBehaviour
{
    public Transform mountPoint;
    public bool isMounted = false;
    public bool isTamed = false; // ✅ Add this to control taming state
}
