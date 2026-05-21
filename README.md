# Cyclops Vehicle Upgrade Console

This mod places the vanilla vehicle upgrade console inside the Cyclops automatically, so you do not need to craft or place it manually.

## What it does

- Hooks Cyclops startup with Harmony.
- Loads the vanilla vehicle upgrade console prefab.
- Anchors it to the Cyclops vehicle docking bay when that bay is present.
- Watches the Cyclops bay and logs which vehicle is docked there.
- Uses a marker component so each Cyclops only gets one copy.

## Notes

- The local offset and rotation are in `Plugin.cs`.
- If the console appears slightly off from the docking-bay panel, adjust `DefaultLocalOffset` and `DefaultLocalEuler`.
- I could not compile the project in this environment because the .NET SDK is not installed here.

## Build

If your Subnautica install is not in the default Steam path, pass the path when building:

```powershell
dotnet build .\CyclopsMoonpoolWorkbenchMod.csproj /p:SubnauticaInstallDir="D:\\Games\\Subnautica"
```

The built DLL is copied automatically to `BepInEx\plugins` after a successful build.
