# Prefab Generator

## Overview
Prefab Generator is a Unity Editor tool designed to create optimized, low-resolution versions of prefabs and scenes. It processes assets such as sprites, textures, audio, and scriptable objects to generate performance-friendly prefabs, making it useful for mobile and WebGL platforms.

## Features
- Generates low-resolution prefabs from existing ones
- Processes entire scenes to replace high-resolution assets with optimized versions
- Supports batch processing for folders
- Customizable quality settings via `PrefabQualitySettingConfig`
- Automated asset caching to avoid redundant processing

## Installation
### Install via Unity Package Manager (UPM)
You can install Prefab Generator using Unity's Package Manager by adding the following Git URL:
```
https://github.com/KingHipUnity/Unity-PrefabGenerator.git
```
#### Steps:
1. Open Unity and go to `Window > Package Manager`.
2. Click the `+` button and select `Add package from git URL`.
3. Paste the Git URL: `https://github.com/KingHipUnity/Unity-PrefabGenerator.git` and click `Add`.

### Manual Installation
1. Copy the `PrefabGenerator` folder into your Unity project's `Assets` directory.
2. Ensure the project is running in Unity Editor mode (this tool is for Editor use only).
3. Open Unity and navigate to `KH-Tools > Prefab Generator` to use the tool.

## Usage
### 1. Open the Prefab Generator
Go to `Window > KH-Tools > Prefab Generator` in the Unity Editor.

### 2. Select Conversion Type
Choose one of the following:
- **Single Prefab:** Process a single prefab to generate a low-resolution version.
- **Scene:** Process a scene to replace prefabs with their optimized versions.
- **Folder:** Process all prefabs within a specified folder.

### 3. Configure Settings
Assign a `PrefabQualitySettingConfig` asset to specify optimization settings:
- **Image Scale Factor:** Adjusts texture resolution.
- **Audio Sample Rate:** Sets audio compression quality.
- **Setting Name:** Prefix used for generated assets.

### 4. Run the Process
Click `Generate` to start processing based on the selected conversion type.

## Scriptable Objects
### PrefabQualitySettingConfig
Located in `KingHip.PrefabGenerator`, this scriptable object defines asset optimization parameters:
```csharp
[CreateAssetMenu(menuName = "PrefabQualitySettingConfig/SettingData", fileName ="SettingDataSO")]
public class PrefabQualitySettingConfig : ScriptableObject {
    public string SettingName;
    public float ImageScaleFactor = 1f;
    public int AudioSampleRate = 24000;
}
```

## How It Works
1. **PrefabProcessor**
   - Iterates through the prefab hierarchy.
   - Optimizes referenced assets using asset processors.
   - Saves optimized prefabs in a specified folder.

2. **SceneProcessor**
   - Scans the scene for prefabs.
   - Replaces high-resolution prefabs with optimized versions.
   - Saves changes as a new scene file.

3. **BaseAssetProcessor**
   - Handles asset optimization (textures, audio, etc.).
   - Ensures built-in Unity assets are not modified.
   - Stores processed assets to prevent redundant work.

## Requirements
- Unity 2020.3 or later
- Editor mode (not runtime compatible)

## License
This project is licensed under the MIT License. Feel free to modify and use it in your projects.

## Contact
For support or contributions, submit an issue in the repository.

