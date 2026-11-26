#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Сам настраивает всё, что лежит в Assets/Items (предметы подруги), прямо при импорте:
//  • картинки из Items/Sprites → Sprite (2D and UI), крупно (до 4096), без размытия-мипмапов;
//  • текстуры моделей → до 4096 (чтобы текст на журнале читался);
//  • модели → без камер, света и анимаций;
//  • бумажные материалы (Note_...) → Cutout, чтобы края бумаги были рваными, а не прямоугольником.
// Нажимать ничего не нужно. (Это замена NotesMaterialFix подруги — его копировать НЕ надо.)
public class ItemsImport : AssetPostprocessor
{
    const string Root = "Assets/Items/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        if (assetPath.Contains("/Sprites/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
        else
        {
            ti.maxTextureSize = 4096;
            if (assetPath.Contains("Lock_digits")) ti.wrapMode = TextureWrapMode.Clamp;
        }
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Root)) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialSearch = ModelImporterMaterialSearch.Everywhere;
    }

    void OnPostprocessMaterial(Material m)
    {
        if (!assetPath.StartsWith(Root)) return;
        if (m.shader == null || m.shader.name != "Standard") m.shader = Shader.Find("Standard");
        m.SetFloat("_Glossiness", 0.1f);
        if (!m.name.StartsWith("Note_")) return;
        m.SetFloat("_Mode", 1f);                 // Cutout
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Glossiness", 0.05f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.EnableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
    }
}
#endif
