# Cyclops Vehicle Upgrade Console

This mod adds a buildable vehicle upgrade console that you can place manually with the Habitat Builder, including inside mobile vehicles like the Cyclops.

## What it does

- Registers a custom buildable based on the vanilla moonpool vehicle upgrade console.
- Adds it to the Habitat Builder Interior Modules category.
- Enables build placement inside bases and submarines (Cyclops support).
- Uses the Seamoth upgrades crafting tree once built.

## Notes

- Recipe and placement flags are in `Plugin.cs`.
- The console is no longer auto-spawned on Cyclops start.
- I could not compile the project in this environment because the .NET SDK is not installed here.

## Build

If your Subnautica install is not in the default Steam path, pass the path when building:

```powershell
dotnet build .\CyclopsMoonpoolWorkbenchMod.csproj /p:SubnauticaInstallDir="D:\\Games\\Subnautica"
```

The built DLL is copied automatically to `BepInEx\plugins` after a successful build.
