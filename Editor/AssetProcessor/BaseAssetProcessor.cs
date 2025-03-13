#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KingHip.PrefabGenerator {
  public abstract class BaseAssetProcessor {
    protected readonly string assetPrefix;
    protected readonly Dictionary<Object, Object> assetCache;

    protected BuildTarget[] relevantPlatforms;
    protected BaseAssetProcessor(string prefix, Dictionary<Object, Object> cache) {
      assetPrefix = prefix;
      assetCache = cache;
      relevantPlatforms = new BuildTarget[]
      {
                BuildTarget.Android,
                BuildTarget.iOS,
                BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneOSX,
                BuildTarget.WebGL
      };
    }
    public const string BuiltinResources = "Resources/unity_builtin_extra";
    public const string BuiltinExtraResources = "Library/unity default resources";

    public bool IsBuiltInAsset(Object source) {
      string assetPath = AssetDatabase.GetAssetPath(source);
      return assetPath.Equals(BuiltinResources) || assetPath.Equals(BuiltinExtraResources);
    }

    public virtual bool CanProcess(Object asset) => false;

    public Object Process(Object source) {
      //Skip Built In Assets
      if (IsBuiltInAsset(source)) {
        return source;
      }
      if (assetCache.ContainsKey(source))
        return assetCache[source];

      var processed = ProcessAsset(source);
      if (processed != null && processed != source)
        assetCache.Add(source, processed);

      return processed ?? source;
    }

    protected abstract Object ProcessAsset(Object source);

    protected string GetTargetPath(Object source) {
      string sourcePath = AssetDatabase.GetAssetPath(source);
      string directory = Path.GetDirectoryName(sourcePath);
      string fileName = Path.GetFileName(sourcePath);

      // Set the new target path inside the "LowRes" folder
      if (directory.StartsWith("Assets")) {
        directory = directory.Remove(0, "Assets".Length + 1);
      }
      string targetPath = Path.Combine("Assets", assetPrefix, directory, $"{assetPrefix}_{fileName}");
      string dirPath = Path.GetDirectoryName(targetPath);
      if (!Directory.Exists(dirPath)) {
        Directory.CreateDirectory(dirPath);
      }


      return targetPath;
    }

    protected void EnsureTargetPathClear(string targetPath) {
      if (File.Exists(targetPath))
        AssetDatabase.DeleteAsset(targetPath);
    }
  }
}
#endif