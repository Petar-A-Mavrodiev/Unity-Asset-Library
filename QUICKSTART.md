# Unity Edu Kit - Quick Start

## Installation
1. Window → Package Manager → **+** → "Add package from git URL"
2. Paste: `https://github.com/Petar-A-Mavrodiev/Unity-Asset-Library.git`

## Required Setup

### Input System
Edit → Project Settings → Player → **Active Input Handling** → select **Both**
Unity will restart.

### DOTween
Install from Asset Store: [DOTween (Free)](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676)
After import: Tools → Demigiant → DOTween Utility Panel → **Setup DOTween**

## Customizing Scripts

Package scripts are **read-only**. To modify:

1. Copy `.cs` file from `Packages/com.metanoetics.unity-asset-library/` to `Assets/`
2. Rename the file and class (e.g., `DestroyAction` → `MyDestroyAction`)
3. Remove or change the `namespace Metanoetics` wrapper
4. Edit as needed
