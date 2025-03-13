#if UNITY_EDITOR
using UnityEngine;
namespace KingHip.PrefabGenerator {
  [CreateAssetMenu(menuName = "PrefabQualitySettingConfig/SettingData", fileName ="SettingDataSO")]
  public class PrefabQualitySettingConfig : ScriptableObject {
    public string SettingName;
    public float ImageScaleFactor = 1f;
    public int AudioSampleRate = 24000;
  }
}
#endif