#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;

namespace KingHip.PrefabGenerator {

  public class PrefabProcessor {
    private readonly List<BaseAssetProcessor> processors = new List<BaseAssetProcessor>();
    private readonly Dictionary<Object, Object> assetCache = new Dictionary<Object, Object>();
    private readonly HashSet<string> processedPrefabPaths = new HashSet<string>();
    private readonly string assetPrefix;

    public PrefabProcessor(PrefabQualitySettingConfig setting) {
      assetPrefix = setting.SettingName;
      processors.Add(new SpriteProcessor(assetPrefix, assetCache, setting.ImageScaleFactor));
      processors.Add(new TextureProcessor(assetPrefix, assetCache, setting.ImageScaleFactor));
      processors.Add(new AudioProcessor(assetPrefix, assetCache, setting.AudioSampleRate));
      processors.Add(new ScriptableObjectProcessor(assetPrefix, assetCache, processors));
    }

    public void ProcessPrefabHierarchy(GameObject rootPrefab) {
      assetCache.Clear();
      processedPrefabPaths.Clear();

      var nestedPrefabs = CollectNestedPrefabs(rootPrefab);
      foreach (var prefab in nestedPrefabs)
        CreateLowResPrefab(prefab);

      CreateLowResPrefab(rootPrefab);

      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
    }
    protected string GetTargetPath(Object source) {
      string sourcePath = AssetDatabase.GetAssetPath(source);
      string directory = Path.GetDirectoryName(sourcePath);
      string fileName = Path.GetFileName(sourcePath);

      if (directory.StartsWith("Assets")) {
        directory = directory.Remove(0, directory.Length == "Assets".Length? "Assets".Length: "Assets".Length + 1);
      }
      string targetPath = Path.Combine("Assets", assetPrefix, directory, $"{assetPrefix}_{fileName}");
      string dirPath = Path.GetDirectoryName(targetPath);
      if (!Directory.Exists(dirPath)) {
        Directory.CreateDirectory(dirPath);
      }


      return targetPath;
    }

    private GameObject CreateLowResPrefab(GameObject sourcePrefabAsset) {
      string sourcePath = AssetDatabase.GetAssetPath(sourcePrefabAsset);
      if (processedPrefabPaths.Contains(sourcePath))
        return assetCache[sourcePrefabAsset] as GameObject;

      string targetPath = GetTargetPath(sourcePrefabAsset);

      if (File.Exists(targetPath))
        AssetDatabase.DeleteAsset(targetPath);

      AssetDatabase.CopyAsset(sourcePath, targetPath);
      GameObject lowResPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
      GameObject prefabContents = PrefabUtility.LoadPrefabContents(targetPath);

      try {
        ProcessComponents(prefabContents);
        ReplaceNestedPrefabs(prefabContents);
        PrefabUtility.SaveAsPrefabAsset(prefabContents, targetPath);
      } finally {
        PrefabUtility.UnloadPrefabContents(prefabContents);
      }

      assetCache[sourcePrefabAsset] = lowResPrefab;
      processedPrefabPaths.Add(sourcePath);
      return lowResPrefab;
    }
    public bool TryGetProcessedAsset(Object originalAsset, out GameObject processedAsset) {
      processedAsset = null;
      if (assetCache.TryGetValue(originalAsset, out Object cached)) {
        processedAsset = cached as GameObject;
        return processedAsset != null;
      }
      return false;
    }
    private void ProcessComponents(GameObject target) {
      var components = target.GetComponentsInChildren<Component>(true);
      foreach (var component in components) {
        if (component == null) continue;
        ProcessSerializedObject(new SerializedObject(component));
      }
    }

    private void ProcessSerializedObject(SerializedObject serializedObject) {
      SerializedProperty iterator = serializedObject.GetIterator();
      bool modified = false;

      while (iterator.NextVisible(true)) {
        if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null) {
          Object processedAsset = ProcessAsset(iterator.objectReferenceValue);
          if (processedAsset != null && processedAsset != iterator.objectReferenceValue) {
            iterator.objectReferenceValue = processedAsset;
            modified = true;
          }
        }
      }

      if (modified)
        serializedObject.ApplyModifiedProperties();
    }

    public Object ProcessAsset(Object asset) {
      foreach (var processor in processors) {
        if (processor.CanProcess(asset))
          return processor.Process(asset);
      }
      return asset;
    }

    private void ReplaceNestedPrefabs(GameObject prefabInstance) {
      var children = prefabInstance.GetComponentsInChildren<Transform>(true);
      foreach (Transform child in children) {
        if (child == null || child.gameObject == prefabInstance) continue;

        GameObject originalPrefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
        if (originalPrefab != null && assetCache.ContainsKey(originalPrefab)) {
          GameObject lowResNestedPrefab = assetCache[originalPrefab] as GameObject;
          if (lowResNestedPrefab != null) {
            var newInstance = (GameObject) PrefabUtility.InstantiatePrefab(lowResNestedPrefab);
            newInstance.transform.SetParent(child.parent, false);
            newInstance.transform.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
            newInstance.transform.localScale = child.localScale;
            newInstance.transform.SetSiblingIndex(child.GetSiblingIndex());

            Object.DestroyImmediate(child.gameObject);
          }
        }
      }
    }

    private List<GameObject> CollectNestedPrefabs(GameObject prefabAsset) {
      List<GameObject> nestedPrefabs = new List<GameObject>();
      HashSet<string> prefabPaths = new HashSet<string>();

      GameObject tempInstance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefabAsset));
      try {
        CollectNestedPrefabsRecursive(tempInstance, nestedPrefabs, prefabPaths);
      } finally {
        PrefabUtility.UnloadPrefabContents(tempInstance);
      }

      nestedPrefabs.Reverse();
      return nestedPrefabs;
    }

    private void CollectNestedPrefabsRecursive(GameObject instance, List<GameObject> nestedPrefabs, HashSet<string> prefabPaths) {
      var transforms = instance.GetComponentsInChildren<Transform>(true);
      foreach (Transform child in transforms) {
        if (child.gameObject == instance) continue;

        GameObject correspondingPrefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
        if (correspondingPrefab != null) {
          string prefabPath = AssetDatabase.GetAssetPath(correspondingPrefab);
          if (!string.IsNullOrEmpty(prefabPath) && !prefabPaths.Contains(prefabPath)) {
            prefabPaths.Add(prefabPath);
            nestedPrefabs.Add(correspondingPrefab);

            GameObject nestedContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
              CollectNestedPrefabsRecursive(nestedContents, nestedPrefabs, prefabPaths);
            } finally {
              PrefabUtility.UnloadPrefabContents(nestedContents);
            }
          }
        }
      }
    }
  }
  public class PrefabGenerator : EditorWindow {
    private enum ConversionType {
      SinglePrefab,
      Folder
    }

    private ConversionType conversionType;
    private GameObject sourcePrefab;
    private string sourceFolderPath = "";
    private PrefabQualitySettingConfig qualityConfig;

    [MenuItem("KH-Tools/Prefab Generator")]
    public static void ShowWindow() {
      GetWindow<PrefabGenerator>("Prefab Generator");
    }
    int selectedAddrs = 0;
    private void OnGUI() {
      GUILayout.Label("Low Resolution Prefab Generator", EditorStyles.boldLabel);

      conversionType = (ConversionType) EditorGUILayout.EnumPopup("Conversion Type", conversionType);

      switch (conversionType) {
        case ConversionType.SinglePrefab:
        sourcePrefab = (GameObject) EditorGUILayout.ObjectField("Source Prefab", sourcePrefab, typeof(GameObject), false);
        break;
        case ConversionType.Folder:
        EditorGUILayout.BeginHorizontal();
        sourceFolderPath = EditorGUILayout.TextField("Source Folder", sourceFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60))) {
          string path = EditorUtility.OpenFolderPanel("Select Source Folder", "Assets", "");
          if (!string.IsNullOrEmpty(path)) {
            sourceFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
          }
        }
        EditorGUILayout.EndHorizontal();
        break;
      }
      qualityConfig = (PrefabQualitySettingConfig) EditorGUILayout.ObjectField("Quality Setting", qualityConfig, typeof(PrefabQualitySettingConfig), false);
      if (qualityConfig != null && GUILayout.Button("Generate")) {
        ProcessBasedOnType();
      }
    }

    private void ProcessBasedOnType() {
      if (!ValidateInput()) return;

      var processor = new PrefabProcessor(qualityConfig);

      try {
        switch (conversionType) {
          case ConversionType.SinglePrefab:
          ProcessSinglePrefab(processor);
          break;
          case ConversionType.Folder:
          ProcessFolder(processor);
          break;
        }
      } catch (System.Exception e) {
        Debug.LogError($"Error in conversion: {e.Message}\n{e.StackTrace}");
        EditorUtility.DisplayDialog("Error", $"Conversion failed: {e.Message}", "OK");
      }
    }

    private bool ValidateInput() {
      switch (conversionType) {
        case ConversionType.SinglePrefab:
        if (sourcePrefab == null) {
          EditorUtility.DisplayDialog("Error", "Please select a prefab", "OK");
          return false;
        }
        break;

        case ConversionType.Folder:
        if (string.IsNullOrEmpty(sourceFolderPath) || !AssetDatabase.IsValidFolder(sourceFolderPath)) {
          EditorUtility.DisplayDialog("Error", "Please select a valid folder", "OK");
          return false;
        }
        break;
      }
      return true;
    }

    private void ProcessSinglePrefab(PrefabProcessor processor) {
      EditorUtility.DisplayProgressBar("Processing Prefab", "Creating low resolution version...", 0.5f);
      processor.ProcessPrefabHierarchy(sourcePrefab);
      EditorUtility.ClearProgressBar();
    }

    private void CollectAllPrefabs(GameObject obj, HashSet<GameObject> prefabs) {
      if (PrefabUtility.IsAnyPrefabInstanceRoot(obj)) {
        prefabs.Add(obj);
      }

      foreach (Transform child in obj.transform) {
        CollectAllPrefabs(child.gameObject, prefabs);
      }
    }

    private void ProcessFolder(PrefabProcessor processor) {
      var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { sourceFolderPath });
      float progress = 0;
      float step = 1f / prefabGuids.Length;

      foreach (var guid in prefabGuids) {
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        EditorUtility.DisplayProgressBar("Processing Folder", $"Processing {Path.GetFileName(assetPath)}...", progress);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        processor.ProcessPrefabHierarchy(prefab);

        progress += step;
      }

      EditorUtility.ClearProgressBar();
    }

    private void ProcessComponents(GameObject obj, PrefabProcessor processor) {
      var components = obj.GetComponentsInChildren<Component>(true);
      foreach (var component in components) {
        if (component == null) continue;

        SerializedObject serializedComponent = new SerializedObject(component);
        ProcessSerializedObject(serializedComponent, processor);
      }
    }

    private void ProcessSerializedObject(SerializedObject serializedObject, PrefabProcessor processor) {
      SerializedProperty iterator = serializedObject.GetIterator();
      bool modified = false;

      while (iterator.NextVisible(true)) {
        if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null) {
          // Process the reference using the processor
          Object processedAsset = processor.ProcessAsset(iterator.objectReferenceValue);
          if (processedAsset != null && processedAsset != iterator.objectReferenceValue) {
            iterator.objectReferenceValue = processedAsset;
            modified = true;
          }
        }
      }

      if (modified) {
        serializedObject.ApplyModifiedProperties();
      }
    }
  }
}
#endif