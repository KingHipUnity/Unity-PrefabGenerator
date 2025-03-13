#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace KingHip.PrefabGenerator {
  public class ScriptableObjectProcessor : BaseAssetProcessor {
    private readonly List<BaseAssetProcessor> processors;

    public ScriptableObjectProcessor(string prefix, Dictionary<Object, Object> cache, List<BaseAssetProcessor> assetProcessors)
        : base(prefix, cache) {
      processors = assetProcessors;
    }

    public override bool CanProcess(Object asset) => false && asset is ScriptableObject;
    protected override Object ProcessAsset(Object source) {
      string targetPath = GetTargetPath(source);
      EnsureTargetPathClear(targetPath);

      var lowResScriptableObject = ScriptableObject.Instantiate(source as ScriptableObject);
      if (lowResScriptableObject == null) {
        return source;
      }
      AssetDatabase.CreateAsset(lowResScriptableObject, targetPath);

      SerializedObject serializedObject = new SerializedObject(lowResScriptableObject);
      ProcessSerializedObject(serializedObject);

      if (lowResScriptableObject != null) {
        EditorUtility.SetDirty(lowResScriptableObject);
      }

      AssetDatabase.SaveAssets();

      return lowResScriptableObject;
    }

    private void ProcessSerializedObject(SerializedObject serializedObject) {
      SerializedProperty iterator = serializedObject.GetIterator();
      bool modified = false;

      while (iterator.NextVisible(true)) {
        if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null) {
          Object processedAsset = ProcessReference(iterator.objectReferenceValue);
          if (processedAsset != null && processedAsset != iterator.objectReferenceValue) {
            iterator.objectReferenceValue = processedAsset;
            modified = true;
          }
        }
      }

      if (modified)
        serializedObject.ApplyModifiedProperties();
    }

    private Object ProcessReference(Object asset) {
      foreach (var processor in processors) {
        if (processor.CanProcess(asset))
          return processor.Process(asset);
      }
      return asset;
    }
  }
}
#endif