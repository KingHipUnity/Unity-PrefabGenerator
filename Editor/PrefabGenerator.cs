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
  public class SceneProcessor {
    private readonly PrefabProcessor prefabProcessor;
    private readonly Dictionary<GameObject, TransformData> transformCache;
    public class RectTransformData {
      public Vector3 LocalPosition { get; set; }

      public Vector3 LocalScale { get; set; }

      public Vector2 SizeDelta { get; set; }

      public Vector2 AnchorMin { get; set; }

      public Vector2 AnchorMax { get; set; }

      public Vector2 AnchoredPosition { get; set; }

      public Vector2 Pivot { get; set; }
    }

    private struct TransformData {
      public Transform Parent;
      public Vector3 LocalPosition;
      public Quaternion LocalRotation;
      public Vector3 LocalScale;
      public int SiblingIndex;
      public PropertyModification[] Modifications;
      public RectTransformData rectTransform;
      public static TransformData Capture(GameObject obj) {
        TransformData data = new TransformData {
          Parent = obj.transform.parent,
          LocalPosition = obj.transform.localPosition,
          LocalRotation = obj.transform.localRotation,
          LocalScale = obj.transform.localScale,
          SiblingIndex = obj.transform.GetSiblingIndex(),
          Modifications = PrefabUtility.GetPropertyModifications(obj)
        };
        if (obj.TryGetComponent(out RectTransform rectTransform)) {
          data.rectTransform = new RectTransformData() {
            AnchoredPosition = rectTransform.anchoredPosition,
            AnchorMin = rectTransform.anchorMin,
            AnchorMax = rectTransform.anchorMax,
            LocalPosition = rectTransform.localPosition,
            LocalScale = rectTransform.localScale,
            Pivot = rectTransform.pivot,
            SizeDelta = rectTransform.sizeDelta
          };
        }
        return data;
      }

      public void Apply(GameObject obj) {
        if (Parent != null) {
          obj.transform.SetParent(Parent);
        }
        obj.transform.SetSiblingIndex(SiblingIndex);
        obj.transform.localPosition = LocalPosition;
        obj.transform.localRotation = LocalRotation;
        obj.transform.localScale = LocalScale;
        if (rectTransform != null) {
          if (obj.TryGetComponent(out RectTransform rectTrans)) {
            rectTrans.localScale = rectTransform.LocalScale;
            rectTrans.pivot = rectTransform.Pivot;
            rectTrans.sizeDelta = rectTransform.SizeDelta;
            rectTrans.anchorMin = rectTransform.AnchorMin;
            rectTrans.anchorMax = rectTransform.AnchorMax;
            rectTrans.anchoredPosition = rectTransform.AnchoredPosition;
            rectTrans.localPosition = rectTransform.LocalPosition;
          }
        }
        if (Modifications != null) {
          PrefabUtility.SetPropertyModifications(obj, Modifications);
        }
      }
    }

    public SceneProcessor(PrefabProcessor processor) {
      prefabProcessor = processor;
      transformCache = new Dictionary<GameObject, TransformData>();
    }

    public void ProcessScene(SceneAsset sourceScene, string lowResPrefix) {
      string scenePath = AssetDatabase.GetAssetPath(sourceScene);
      string targetPath = GetTargetScenePath(scenePath, lowResPrefix);

      // Create and open low-res scene
      var scene = CreateLowResScene(scenePath, targetPath);

      try {
        ProcessSceneContents(scene);
        EditorSceneManager.SaveScene(scene);
      } finally {
        transformCache.Clear();
      }
    }

    private string GetTargetScenePath(string sourcePath, string prefix) {
      var dir = System.IO.Path.GetDirectoryName(sourcePath);
      var fileName = System.IO.Path.GetFileName(sourcePath);
      return $"{dir}/{prefix}{fileName}";
    }

    private UnityEngine.SceneManagement.Scene CreateLowResScene(string sourcePath, string targetPath) {
      var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
      EditorSceneManager.SaveScene(scene, targetPath);
      return EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
    }

    private void ProcessSceneContents(UnityEngine.SceneManagement.Scene scene) {
      var prefabRoots = CollectPrefabRoots(scene);

      // Cache transform data
      foreach (var root in prefabRoots) {
        CacheHierarchyTransforms(root);
      }

      // Process prefabs
      foreach (var root in prefabRoots) {
        ProcessPrefabRoot(root);
        // break;
      }
    }

    private List<GameObject> CollectPrefabRoots(UnityEngine.SceneManagement.Scene scene) {
      var roots = new List<GameObject>();
      foreach (var rootObj in scene.GetRootGameObjects()) {
        CollectPrefabRootsRecursive(rootObj, roots);
      }
      return roots;
    }

    private void CollectPrefabRootsRecursive(GameObject obj, List<GameObject> roots) {
      if (PrefabUtility.IsAnyPrefabInstanceRoot(obj)) {
        roots.Add(obj);
      }

      foreach (Transform child in obj.transform) {
        CollectPrefabRootsRecursive(child.gameObject, roots);
      }
    }

    private void CacheHierarchyTransforms(GameObject obj) {
      transformCache[obj] = TransformData.Capture(obj);

      var children = new List<Transform>();
      foreach (Transform child in obj.transform) {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) {
          children.Add(child);
        }
      }

      foreach (var child in children) {
        CacheHierarchyTransforms(child.gameObject);
      }
    }

    private void ProcessPrefabRoot(GameObject obj) {
      if (obj == null) {
        Debug.LogError("Error in conversion: Value cannot be null. Obj Null");
        return;
      }
      Debug.Log("Process Prefab:" + obj.name);
      var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(obj);
      if (sourcePrefab == null) return;

      // Process the prefab asset
      prefabProcessor.ProcessPrefabHierarchy(sourcePrefab);

      if (prefabProcessor.TryGetProcessedAsset(sourcePrefab, out GameObject lowResPrefab)) {
        // Break prefab instance
        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Create new instance


        // Apply cached transform data
        if (transformCache.TryGetValue(obj, out TransformData data)) {
          var newInstance = (GameObject) PrefabUtility.InstantiatePrefab(lowResPrefab, data.Parent);
          data.Apply(newInstance);
          transformCache.Remove(obj);
          transformCache.Add(newInstance, data);
        }


        GameObject.DestroyImmediate(obj);
      }
    }
  }
  public class PrefabGenerator : EditorWindow {
    private enum ConversionType {
      SinglePrefab,
      Scene,
      Folder
    }

    private ConversionType conversionType;
    private GameObject sourcePrefab;
    private SceneAsset sourceScene;
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
        case ConversionType.Scene:
        sourceScene = (SceneAsset) EditorGUILayout.ObjectField("Source Scene", sourceScene, typeof(SceneAsset), false);
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
          case ConversionType.Scene:
          ProcessScene(processor);
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
        case ConversionType.Scene:
        if (sourceScene == null) {
          EditorUtility.DisplayDialog("Error", "Please select a scene", "OK");
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
    private void ProcessScene(PrefabProcessor prefabProcessor) {
      var sceneProcessor = new SceneProcessor(prefabProcessor);
      sceneProcessor.ProcessScene(sourceScene, qualityConfig.SettingName);
    }
    private void CollectAllPrefabs(GameObject obj, HashSet<GameObject> prefabs) {
      if (PrefabUtility.IsAnyPrefabInstanceRoot(obj)) {
        prefabs.Add(obj);
      }

      foreach (Transform child in obj.transform) {
        CollectAllPrefabs(child.gameObject, prefabs);
      }
    }
    private void ProcessSceneObjectHierarchy(GameObject obj, PrefabProcessor processor) {
      if (obj == null) return;

      bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(obj);
      var children = new List<Transform>();

      // Store children references
      foreach (Transform child in obj.transform) {
        children.Add(child);
      }

      // Process children first
      foreach (var child in children) {
        ProcessSceneObjectHierarchy(child.gameObject, processor);
      }

      // Process current object if it's a prefab
      if (isPrefab) {
        ReplaceWithLowResPrefab(obj, processor);
      }
    }
    private void ReplaceWithLowResPrefab(GameObject obj, PrefabProcessor processor) {
      var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(obj);
      if (sourcePrefab == null) return;

      if (processor.TryGetProcessedAsset(sourcePrefab, out GameObject lowResPrefab)) {
        Transform parent = obj.transform.parent;
        int siblingIndex = obj.transform.GetSiblingIndex();
        Vector3 localPos = obj.transform.localPosition;
        Quaternion localRot = obj.transform.localRotation;
        Vector3 localScale = obj.transform.localScale;

        // Instantiate new prefab in same scene
        GameObject newInstance = (GameObject) PrefabUtility.InstantiatePrefab(lowResPrefab, obj.scene);

        // Set parent before setting transform values
        newInstance.transform.SetParent(parent, false);
        newInstance.transform.SetSiblingIndex(siblingIndex);
        newInstance.transform.localPosition = localPos;
        newInstance.transform.localRotation = localRot;
        newInstance.transform.localScale = localScale;

        // Copy modifications
        var modifications = PrefabUtility.GetPropertyModifications(obj);
        if (modifications != null) {
          PrefabUtility.SetPropertyModifications(newInstance, modifications);
        }

        DestroyImmediate(obj);
      }
    }
    private void CollectAllPrefabsInHierarchy(GameObject obj, HashSet<GameObject> prefabs) {
      var prefabInstance = PrefabUtility.GetNearestPrefabInstanceRoot(obj);
      if (prefabInstance != null) {
        prefabs.Add(prefabInstance);
      }

      foreach (Transform child in obj.transform) {
        CollectAllPrefabsInHierarchy(child.gameObject, prefabs);
      }
    }

    private void ProcessSceneObject(GameObject obj, PrefabProcessor processor) {
      var prefabInstance = PrefabUtility.GetNearestPrefabInstanceRoot(obj);
      if (prefabInstance == obj) {
        var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(obj);
        if (sourcePrefab != null && processor.TryGetProcessedAsset(sourcePrefab, out GameObject lowResPrefab)) {
          var newInstance = (GameObject) PrefabUtility.InstantiatePrefab(lowResPrefab);
          CopyTransformValues(obj, newInstance);

          // Apply prefab overrides from original to new instance
          var modifications = PrefabUtility.GetPropertyModifications(obj);
          if (modifications != null) {
            PrefabUtility.SetPropertyModifications(newInstance, modifications);
          }

          newInstance.transform.SetParent(obj.transform.parent, false);
          DestroyImmediate(obj);
        }
      } else if (prefabInstance == null) {
        ProcessComponents(obj, processor);
        foreach (Transform child in obj.transform) {
          ProcessSceneObject(child.gameObject, processor);
        }
      }
    }
    private void CopyTransformValues(GameObject source, GameObject target) {
      target.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
      target.transform.localScale = source.transform.localScale;
      target.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
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