using BepInEx;
using BepInEx.Logging;
using Nautilus.Handlers;
using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Utility;
using UnityEngine;

namespace CyclopsMoonpoolWorkbenchMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.snmodding.nautilus")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.copilot.cyclopsmoonpoolworkbench";
        internal const string PluginName = "Cyclops Vehicle Upgrade Console";
        internal const string PluginVersion = "1.0.0";
        internal const string ConsoleClassId = "CyclopsVehicleUpgradesConsole";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            RegisterVehicleUpgradeConsoleBuildable();
            Log.LogInfo("Cyclops vehicle upgrades console buildable registered.");
        }

        private static void RegisterVehicleUpgradeConsoleBuildable()
        {
            PrefabInfo info = PrefabInfo
                .WithTechType(ConsoleClassId, "Compact Vehicle Upgrade Console",
                    "Buildable vehicle upgrade console for bases and mobile vehicles like Cyclops.")
                .WithIcon(CreateConsoleIcon());

            CustomPrefab consolePrefab = new CustomPrefab(info);
            FabricatorTemplate template = new FabricatorTemplate(info, CraftTree.Type.SeamothUpgrades)
            {
                FabricatorModel = FabricatorTemplate.Model.MoonPool,
                ConstructableFlags = ConstructableFlags.Inside |
                                     ConstructableFlags.Wall |
                                     ConstructableFlags.Submarine |
                                     ConstructableFlags.AllowedOnConstructable
            };

            consolePrefab.SetGameObject(template);
            consolePrefab.SetRecipe(new RecipeData
            {
                craftAmount = 1,
                Ingredients =
                {
                    new Ingredient(TechType.Titanium, 3),
                    new Ingredient(TechType.ComputerChip, 1),
                    new Ingredient(TechType.CopperWire, 1)
                }
            });
            consolePrefab.SetUnlock(TechType.BaseUpgradeConsole);
            consolePrefab.SetPdaGroupCategory(TechGroup.InteriorModules, TechCategory.InteriorModule);
            consolePrefab.Register();
        }

        private static Sprite CreateConsoleIcon()
        {
            Sprite fabricatorIcon = SpriteManager.Get(TechType.Fabricator);
            Sprite moduleIcon = SpriteManager.Get(TechType.BaseUpgradeConsole);

            Texture2D baseTexture = ExtractSpriteTexture(fabricatorIcon);
            Texture2D overlayTexture = ExtractSpriteTexture(moduleIcon);
            if (baseTexture == null || overlayTexture == null)
                return fabricatorIcon;

            int overlayWidth = Mathf.Max(16, Mathf.RoundToInt(baseTexture.width * 0.42f));
            int overlayHeight = Mathf.Max(16, Mathf.RoundToInt(baseTexture.height * 0.42f));
            int startX = Mathf.Max(0, baseTexture.width - overlayWidth - 4);
            int startY = 4;

            for (int y = 0; y < overlayHeight; y++)
            {
                for (int x = 0; x < overlayWidth; x++)
                {
                    float u = x / (float)(overlayWidth - 1);
                    float v = y / (float)(overlayHeight - 1);
                    Color overlay = overlayTexture.GetPixelBilinear(u, v);
                    if (overlay.a <= 0.01f)
                        continue;

                    int destX = startX + x;
                    int destY = startY + y;
                    if (destX < 0 || destX >= baseTexture.width || destY < 0 || destY >= baseTexture.height)
                        continue;

                    Color under = baseTexture.GetPixel(destX, destY);
                    Color mixed = Color.Lerp(under, overlay, overlay.a);
                    mixed.a = Mathf.Max(under.a, overlay.a);
                    baseTexture.SetPixel(destX, destY, mixed);
                }
            }

            baseTexture.Apply();
            return Sprite.Create(baseTexture, new Rect(0, 0, baseTexture.width, baseTexture.height), new Vector2(0.5f, 0.5f));
        }

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return null;

            try
            {
                Rect rect = sprite.textureRect;
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] pixels = sprite.texture.GetPixels(
                    Mathf.RoundToInt(rect.x),
                    Mathf.RoundToInt(rect.y),
                    width,
                    height);

                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
            catch
            {
                return null;
            }
        }
    }
}