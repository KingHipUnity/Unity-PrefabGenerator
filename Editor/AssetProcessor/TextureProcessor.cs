#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingHip.PrefabGenerator {
  public class TextureProcessor : BaseAssetProcessor {
    private readonly float scaleFactor;
    public TextureProcessor(string prefix, Dictionary<Object, Object> cache, float scale)
        : base(prefix, cache) {
      scaleFactor = scale;
    }

    public override bool CanProcess(Object asset) => asset is Texture;

    protected override Object ProcessAsset(Object source) {
      var texture = (source as Texture);
      float maxSize = texture.width > texture.height ? texture.width : texture.height;
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
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.RoundToInt(maxSize * scaleFactor));
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
      }

      return AssetDatabase.LoadAssetAtPath<Texture>(targetPath);
    }
  }

}
#endif