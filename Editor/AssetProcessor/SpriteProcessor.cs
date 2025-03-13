#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingHip.PrefabGenerator {
  public class SpriteProcessor : BaseAssetProcessor {
    private readonly float scaleFactor;
    public SpriteProcessor(string prefix, Dictionary<Object, Object> cache, float scale)
        : base(prefix, cache) {
      scaleFactor = scale;
    }

    public override bool CanProcess(Object asset) => asset is Sprite;

    protected override Object ProcessAsset(Object source) {
      var sprite = (source as Sprite);
      float maxSize = sprite.texture.width > sprite.texture.height ? sprite.texture.width : sprite.texture.height;
      string targetPath = GetTargetPath(source);
      EnsureTargetPathClear(targetPath);

      AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), targetPath);

      var importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
      if (importer != null) {
        foreach (BuildTarget platform in relevantPlatforms) {
          TextureImporterPlatformSettings importerSettings = importer.GetPlatformTextureSettings(platform.ToString());
          if (importerSettings != null && importerSettings.overridden) {
            importerSettings.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.RoundToInt(maxSize * scaleFactor));
            importerSettings.textureCompression = TextureImporterCompression.Compressed;
            // Apply modified settings
            importer.SetPlatformTextureSettings(importerSettings);
          }
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.RoundToInt(maxSize * scaleFactor));
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
      }

      return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
    }
  }

}
#endif