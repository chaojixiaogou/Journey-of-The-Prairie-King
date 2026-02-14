// Assets/Editor/SpriteCombinerEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class SpriteCombinerEditor : EditorWindow
{
    [MenuItem("Tools/Combine Sprites to PNG")]
    public static void CombineAndSave()
    {
        // 假设你选中了多个 Sprite
        Object[] selected = Selection.GetFiltered(typeof(Sprite), SelectionMode.Assets);
        if (selected.Length == 0) return;

        Sprite[] sprites = new Sprite[selected.Length];
        for (int i = 0; i < selected.Length; i++) sprites[i] = selected[i] as Sprite;

        // 简单横向拼接（假设高度一致）
        int totalWidth = 0;
        int maxHeight = 0;
        foreach (var s in sprites)
        {
            totalWidth += (int)s.textureRect.width;
            maxHeight = Mathf.Max(maxHeight, (int)s.textureRect.height);
        }

        Texture2D output = new Texture2D(totalWidth, maxHeight, TextureFormat.RGBA32, false);
        output.SetPixels(0, 0, totalWidth, maxHeight, new Color[totalWidth * maxHeight]);

        int offsetX = 0;
        foreach (var s in sprites)
        {
            Color[] pixels = s.texture.GetPixels((int)s.textureRect.x, (int)s.textureRect.y, (int)s.textureRect.width, (int)s.textureRect.height);
            output.SetPixels(offsetX, 0, (int)s.textureRect.width, (int)s.textureRect.height, pixels);
            offsetX += (int)s.textureRect.width;
        }
        output.Apply();

        byte[] pngData = output.EncodeToPNG();
        string path = EditorUtility.SaveFilePanel("Save Combined Sprite", "", "combined.png", "png");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, pngData);
            Debug.Log("Saved to: " + path);
        }

        Object.DestroyImmediate(output);
    }
}