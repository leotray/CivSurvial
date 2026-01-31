using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class URPOutline : MonoBehaviour
{
    public Material outlineMaterial;
    public float outlineWidth = 0.02f;

    private GameObject outlineObject;

    void Start()
    {
        if (outlineMaterial == null)
        {
            Debug.LogError("Outline material not assigned!");
            return;
        }

        // Create outline object
        outlineObject = new GameObject("OutlineMesh");
        outlineObject.transform.SetParent(transform, false);

        // Copy mesh
        MeshFilter originalMF = GetComponent<MeshFilter>();
        MeshRenderer originalMR = GetComponent<MeshRenderer>();

        MeshFilter outlineMF = outlineObject.AddComponent<MeshFilter>();
        MeshRenderer outlineMR = outlineObject.AddComponent<MeshRenderer>();

        outlineMF.sharedMesh = originalMF.sharedMesh;
        outlineMR.material = new Material(outlineMaterial); // Instance

        // Set properties
        outlineMR.material.SetFloat("_OutlineWidth", outlineWidth);

        // Scale slightly
        outlineObject.transform.localScale = Vector3.one;

        // Optional: layer or tag to manage render separately
    }
}
