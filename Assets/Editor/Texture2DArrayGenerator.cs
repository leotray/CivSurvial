using UnityEngine;
using UnityEditor;

public class Texture2DArrayGenerator
{
    [MenuItem("Assets/Create/Texture2DArray From Selection")]
    public static void CreateTexture2DArray()
    {
        Texture2D[] textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        if (textures.Length == 0)
        {
            Debug.LogWarning("No textures selected!");
            return;
        }

        int width = textures[0].width;
        int height = textures[0].height;
        TextureFormat format = textures[0].format;

        Texture2DArray textureArray = new Texture2DArray(width, height, textures.Length, format, true, false);

        for (int i = 0; i < textures.Length; i++)
        {
            Graphics.CopyTexture(textures[i], 0, 0, textureArray, i, 0);
        }

        textureArray.wrapMode = TextureWrapMode.Repeat;
        textureArray.filterMode = FilterMode.Bilinear;

        string path = EditorUtility.SaveFilePanelInProject("Save Texture2DArray", "NewTextureArray", "asset", "Save texture array asset");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(textureArray, path);
        AssetDatabase.SaveAssets();

        Debug.Log("✅ Texture2DArray created at: " + path);
    }
}