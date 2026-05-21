using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;
using Nautilus.Assets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Json;
using Nautilus.Options.Attributes;
using UnityEngine;
using UWE;

namespace CyclopsMoonpoolWorkbenchMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.copilot.cyclopsmoonpoolworkbench";
        internal const string PluginName = "Cyclops Vehicle Upgrade Console";
        internal const string PluginVersion = "1.0.0";
        internal const string ConsoleClassId = "CyclopsVehicleUpgradesConsole";
        internal const float DefaultOffsetX = -3f;
        internal const float DefaultOffsetY = -0.22f;
        internal const float DefaultOffsetZ = -3f;
        internal const float DefaultEulerX = 0f;
        internal const float DefaultEulerY = 90f;
        internal const float DefaultEulerZ = 0f;

        internal static ManualLogSource Log;
        internal static PlacementMenuConfig PlacementConfig;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PlacementConfig = OptionsPanelHandler.RegisterModOptions<PlacementMenuConfig>();
            RegisterVehicleUpgradeConsole();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Cyclops vehicle upgrades console mod loaded.");
        }

        internal static Vector3 GetConfiguredOffset()
        {
            PlacementMenuConfig config = PlacementConfig;
            if (config == null)
                return new Vector3(DefaultOffsetX, DefaultOffsetY, DefaultOffsetZ);

            return new Vector3(config.OffsetX, config.OffsetY, config.OffsetZ);
        }

        internal static Vector3 GetConfiguredEuler()
        {
            PlacementMenuConfig config = PlacementConfig;
            if (config == null)
                return new Vector3(DefaultEulerX, DefaultEulerY, DefaultEulerZ);

            return new Vector3(config.RotationX, config.RotationY, config.RotationZ);
        }

        private static void RegisterVehicleUpgradeConsole()
        {
            PrefabInfo info = PrefabInfo.WithTechType(ConsoleClassId, "Cyclops Vehicle Upgrade Console",
                "A vehicle upgrade console integrated into the Cyclops.");

            CustomPrefab consolePrefab = new CustomPrefab(info);
            FabricatorTemplate template = new FabricatorTemplate(info, CraftTree.Type.SeamothUpgrades)
            {
                FabricatorModel = FabricatorTemplate.Model.MoonPool
            };

            consolePrefab.SetGameObject(template);
            consolePrefab.Register();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(SubRoot), "Start")]
        private static class CyclopsStartPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SubRoot __instance)
            {
                if (__instance == null || __instance.gameObject == null)
                    return;

                if (__instance.gameObject.name.IndexOf("cyclops", System.StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                if (__instance.GetComponentInChildren<CyclopsMoonpoolWorkbenchMarker>(true) != null)
                    return;

                var installer = __instance.gameObject.AddComponent<CyclopsMoonpoolWorkbenchInstaller>();
                installer.Begin(__instance);
            }
        }
    }

    internal sealed class CyclopsMoonpoolWorkbenchInstaller : MonoBehaviour
    {
        private static readonly string[] DockingBayComponentNames = new string[] { "VehicleDockingBay", "CyclopsVehicleStorageTerminalManager" };
        private static readonly string[] DockingBayAnchorNames = new string[] { "dockingpoint", "dock", "bay", "upgradeconsole", "vehicleupgradeconsole", "vehicleupgradesconsole" };
        private static readonly string[] WallAnchorNames = new string[] { "wall", "panel", "console", "terminal", "upgrade" };
        private const float DockingBayRetryInterval = 1f;
        private const float DockingBayRetryTimeout = 15f;

        private SubRoot _cyclops;

        public void Begin(SubRoot cyclops)
        {
            _cyclops = cyclops;
            StartCoroutine(InstallRoutine());
        }

        private IEnumerator InstallRoutine()
        {
            yield return null;

            if (_cyclops == null)
                yield break;

            if (FindExistingMarker(_cyclops.transform) != null)
                yield break;

            float timeoutAt = Time.time + DockingBayRetryTimeout;
            MonoBehaviour dockingBay = null;
            while (dockingBay == null && Time.time < timeoutAt)
            {
                dockingBay = FindDockingBayComponent(_cyclops.transform);
                if (dockingBay != null)
                    break;

                yield return new WaitForSeconds(DockingBayRetryInterval);
            }

            Transform targetParent = FindWallAnchor(dockingBay, _cyclops.transform);
            if (targetParent == null)
                targetParent = FindDockingBayAnchor(dockingBay, _cyclops.transform);
            if (targetParent == null)
                targetParent = _cyclops.transform;

            var request = PrefabDatabase.GetPrefabAsync(Plugin.ConsoleClassId);
            yield return request;

            GameObject prefab;
            request.TryGetPrefab(out prefab);
            if (prefab == null)
            {
                Plugin.Log.LogWarning("Could not load the Cyclops vehicle upgrade console prefab.");
                yield break;
            }

            GameObject station = Instantiate(prefab, targetParent, false);
            station.name = "CyclopsVehicleUpgradeConsole";
            station.transform.localPosition = Plugin.GetConfiguredOffset();
            station.transform.localRotation = Quaternion.Euler(Plugin.GetConfiguredEuler());
            station.SetActive(true);

            CyclopsPlacementApplier placementApplier = station.GetComponent<CyclopsPlacementApplier>();
            if (placementApplier == null)
                placementApplier = station.AddComponent<CyclopsPlacementApplier>();

            placementApplier.Begin(station.transform);

            station.AddComponent<CyclopsMoonpoolWorkbenchMarker>();

            if (dockingBay != null)
            {
                CyclopsVehicleDockWatcher watcher = _cyclops.gameObject.GetComponent<CyclopsVehicleDockWatcher>();
                if (watcher == null)
                    watcher = _cyclops.gameObject.AddComponent<CyclopsVehicleDockWatcher>();

                watcher.Begin(dockingBay);
            }

            Plugin.Log.LogInfo("Placed a vehicle upgrade console in Cyclops '" + _cyclops.name + "' under parent '" + targetParent.name + "'.");
        }

        private static CyclopsMoonpoolWorkbenchMarker FindExistingMarker(Transform root)
        {
            return root.GetComponentInChildren<CyclopsMoonpoolWorkbenchMarker>(true);
        }

        private static MonoBehaviour FindDockingBayComponent(Transform root)
        {
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                    continue;

                string componentName = component.GetType().Name;
                for (int j = 0; j < DockingBayComponentNames.Length; j++)
                {
                    if (string.Equals(componentName, DockingBayComponentNames[j], StringComparison.Ordinal))
                        return component;
                }
            }

            return null;
        }

        private static Transform FindDockingBayAnchor(MonoBehaviour dockingBay, Transform root)
        {
            if (dockingBay != null)
            {
                Transform dockingTransform = dockingBay.transform;
                Transform dockingPoint = FindNamedChild(dockingTransform, DockingBayAnchorNames);
                if (dockingPoint != null)
                    return dockingPoint.parent != null ? dockingPoint.parent : dockingTransform;

                return dockingTransform;
            }

            Transform namedAnchor = FindNamedChild(root, DockingBayAnchorNames);
            if (namedAnchor != null)
                return namedAnchor.parent != null ? namedAnchor.parent : namedAnchor;

            return null;
        }

        private static Transform FindWallAnchor(MonoBehaviour dockingBay, Transform root)
        {
            if (dockingBay != null)
            {
                Transform dockingTransform = dockingBay.transform;
                Transform wallAnchor = FindNamedChild(dockingTransform, WallAnchorNames);
                if (wallAnchor != null)
                    return wallAnchor.parent != null ? wallAnchor.parent : wallAnchor;

                Transform parent = dockingTransform.parent;
                if (parent != null)
                {
                    wallAnchor = FindNamedChild(parent, WallAnchorNames);
                    if (wallAnchor != null)
                        return wallAnchor.parent != null ? wallAnchor.parent : wallAnchor;
                }
            }

            Transform namedAnchor = FindNamedChild(root, WallAnchorNames);
            if (namedAnchor != null)
                return namedAnchor.parent != null ? namedAnchor.parent : namedAnchor;

            return null;
        }

        private static Transform FindNamedChild(Transform root, string[] nameTokens)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                    continue;

                string lowered = candidate.name.ToLowerInvariant();
                for (int j = 0; j < nameTokens.Length; j++)
                {
                    if (lowered.Contains(nameTokens[j]))
                        return candidate;
                }
            }

            return null;
        }

        private static Transform FindBestParent(Transform root)
        {
            Transform best = null;
            int bestScore = 0;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                int score = ScoreName(child.name);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = child;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static int ScoreName(string name)
        {
            string lowered = name.ToLowerInvariant();
            int score = 0;

            if (lowered.Contains("fabricator")) score += 120;
            if (lowered.Contains("upgrade")) score += 110;
            if (lowered.Contains("console")) score += 105;
            if (lowered.Contains("moonpool")) score += 100;
            if (lowered.Contains("vehiclebay") || lowered.Contains("vehicle_bay")) score += 90;
            if (lowered.Contains("dock")) score += 80;
            if (lowered.Contains("bay")) score += 60;
            if (lowered.Contains("vehicle")) score += 40;
            if (lowered.Contains("model")) score += 20;
            if (lowered.Contains("submarine")) score += 10;

            return score;
        }
    }

    internal sealed class CyclopsMoonpoolWorkbenchMarker : MonoBehaviour
    {
    }

    [Menu("Cyclops Vehicle Console")]
    internal class PlacementMenuConfig : ConfigFile
    {
        [Slider("Offset X", -8f, 8f, DefaultValue = Plugin.DefaultOffsetX, Step = 0.01f, Format = "{0:F2}")]
        public float OffsetX = Plugin.DefaultOffsetX;

        [Slider("Offset Y", -8f, 8f, DefaultValue = Plugin.DefaultOffsetY, Step = 0.01f, Format = "{0:F2}")]
        public float OffsetY = Plugin.DefaultOffsetY;

        [Slider("Offset Z", -8f, 8f, DefaultValue = Plugin.DefaultOffsetZ, Step = 0.01f, Format = "{0:F2}")]
        public float OffsetZ = Plugin.DefaultOffsetZ;

        [Slider("Rotacion X", -180f, 180f, DefaultValue = Plugin.DefaultEulerX, Step = 1f, Format = "{0:F0}")]
        public float RotationX = Plugin.DefaultEulerX;

        [Slider("Rotacion Y", -180f, 180f, DefaultValue = Plugin.DefaultEulerY, Step = 1f, Format = "{0:F0}")]
        public float RotationY = Plugin.DefaultEulerY;

        [Slider("Rotacion Z", -180f, 180f, DefaultValue = Plugin.DefaultEulerZ, Step = 1f, Format = "{0:F0}")]
        public float RotationZ = Plugin.DefaultEulerZ;
    }

    internal sealed class CyclopsPlacementApplier : MonoBehaviour
    {
        private Transform _station;
        private float _nextApplyTime;

        public void Begin(Transform station)
        {
            _station = station;
            _nextApplyTime = 0f;
        }

        private void Update()
        {
            if (_station == null)
                return;

            if (Time.time < _nextApplyTime)
                return;

            _nextApplyTime = Time.time + 0.1f;
            _station.localPosition = Plugin.GetConfiguredOffset();
            _station.localRotation = Quaternion.Euler(Plugin.GetConfiguredEuler());
        }
    }

    internal sealed class CyclopsVehicleDockWatcher : MonoBehaviour
    {
        private const float PollInterval = 0.5f;
        private MonoBehaviour _dockingBay;
        private float _nextPollTime;
        private string _lastVehicleLabel;

        public void Begin(MonoBehaviour dockingBay)
        {
            _dockingBay = dockingBay;
            _nextPollTime = 0f;
            _lastVehicleLabel = null;
        }

        private void Update()
        {
            if (_dockingBay == null)
                return;

            if (Time.time < _nextPollTime)
                return;

            _nextPollTime = Time.time + PollInterval;

            string vehicleLabel = ReadDockedVehicleLabel(_dockingBay);
            if (vehicleLabel == _lastVehicleLabel)
                return;

            _lastVehicleLabel = vehicleLabel;
            if (string.IsNullOrEmpty(vehicleLabel))
                Plugin.Log.LogInfo("Cyclops docking bay is empty.");
            else
                Plugin.Log.LogInfo("Cyclops docking bay vehicle detected: " + vehicleLabel);
        }

        private static string ReadDockedVehicleLabel(MonoBehaviour dockingBay)
        {
            if (dockingBay == null)
                return null;

            object value = TryInvokeMember(dockingBay, "GetDockedVehicle", "GetVehicle", "GetCurrentVehicle");
            if (value != null)
                return DescribeDockedObject(value);

            value = TryReadMember(dockingBay, "dockedVehicle", "vehicle", "currentVehicle", "occupant", "storedVehicle", "attachedVehicle", "dockVehicle");
            if (value != null)
                return DescribeDockedObject(value);

            value = TryReadMemberByKeyword(dockingBay, "dockedVehicle");
            if (value != null)
                return DescribeDockedObject(value);

            value = TryReadMemberByKeyword(dockingBay, "vehicle");
            if (value != null)
                return DescribeDockedObject(value);

            return null;
        }

        private static object TryInvokeMember(MonoBehaviour target, params string[] methodNames)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = type.GetMethod(methodNames[i], flags, null, Type.EmptyTypes, null);
                if (method == null)
                    continue;

                try
                {
                    object value = method.Invoke(target, null);
                    if (value != null)
                        return value;
                }
                catch
                {
                }
            }

            return null;
        }

        private static object TryReadMember(MonoBehaviour target, params string[] names)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    object value = field.GetValue(target);
                    if (value != null)
                        return value;
                }

                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    object value = property.GetValue(target, null);
                    if (value != null)
                        return value;
                }
            }

            return null;
        }

        private static object TryReadMemberByKeyword(MonoBehaviour target, string keyword)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null)
                    continue;

                if (field.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                object value = field.GetValue(target);
                if (value != null)
                    return value;
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property == null || property.GetIndexParameters().Length != 0)
                    continue;

                if (property.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                try
                {
                    object value = property.GetValue(target, null);
                    if (value != null)
                        return value;
                }
                catch
                {
                }
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.GetParameters().Length != 0)
                    continue;

                if (method.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                try
                {
                    object value = method.Invoke(target, null);
                    if (value != null)
                        return value;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string DescribeDockedObject(object value)
        {
            GameObject gameObject = value as GameObject;
            if (gameObject != null)
                return gameObject.name;

            Component component = value as Component;
            if (component != null)
                return component.gameObject != null ? component.gameObject.name : component.name;

            MonoBehaviour behaviour = value as MonoBehaviour;
            if (behaviour != null)
                return behaviour.gameObject != null ? behaviour.gameObject.name : behaviour.name;

            return value.ToString();
        }
    }
}