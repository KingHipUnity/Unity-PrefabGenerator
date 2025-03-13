#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingHip.PrefabGenerator {
  public class AudioProcessor : BaseAssetProcessor {
    private readonly int sampleRate;

    public AudioProcessor(string prefix, Dictionary<Object, Object> cache, int rate)
        : base(prefix, cache) {
      sampleRate = rate;
    }

    public override bool CanProcess(Object asset) => asset is AudioClip;

    protected override Object ProcessAsset(Object source) {
      string targetPath = GetTargetPath(source);
      EnsureTargetPathClear(targetPath);

      AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), targetPath);

      var importer = AssetImporter.GetAtPath(targetPath) as AudioImporter;
      if (importer != null) {
        var settings = importer.defaultSampleSettings;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = (uint) sampleRate;
        settings.quality = 0.5f;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;

        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
      }

      return AssetDatabase.LoadAssetAtPath<AudioClip>(targetPath);
    }
  }

}
#endif