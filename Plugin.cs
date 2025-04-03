using SPT.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Shorthands for relevant classes, these need to be changed with each etf build, all of these are located in GetActionClass
using LeaveItemClass = GetActionsClass.Class1647; // Hide objective, 
using BeaconPlantClass = GetActionsClass.Class1648; // Protect beacon class, this class is usually +1 of the above
using DisarmTrapClass = GetActionsClass.Class1641; // Disarm tripwire, this usually is - 6 of the first class above?

// TODO: Add bool to ignore multitool for tripwires?

namespace PlantTimeModifier
{
    // TODO: DONT FORGET TO UPDATE VERSION NUMBER
    [BepInPlugin("com.utjan.PlantTimeModifier", "utjan.PlantTimeModifier", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        internal static ConfigEntry<bool> enabledPlugin;
        internal static ConfigEntry<float> timeMultiplierRepair;
        internal static ConfigEntry<float> timeMultiplierHide;
        internal static ConfigEntry<float> timeMultiplierProtect;
        internal static ConfigEntry<float> timeMultiplierDisarm;

        private void Awake() //Awake() will run once when your plugin loads
        {
            enabledPlugin = Config.Bind(
                "Main Settings",
                "Enable Mod",
                true,
                new ConfigDescription("Enable timer multipliers", null, new ConfigurationManagerAttributes { Order = 5 })
            );

            timeMultiplierRepair = Config.Bind(
                "Main Settings",
                "Repair objective Time Multiplier",
                0.5f,
                new ConfigDescription("Multiplies the duration when doing 'Repairing objective' task action. 0.5 = time is halved. 2.0 = time is doubled. 0 is instant", new AcceptableValueRange<float>(0, 5), new ConfigurationManagerAttributes { Order = 4 })
            );

            timeMultiplierHide = Config.Bind(
                "Main Settings",
                "Hide objective Time Multiplier",
                0.5f,
                new ConfigDescription("Multiplies the duration when doing 'Hiding objective' task action. 0.5 = time is halved. 2.0 = time is doubled. 0 is instant", new AcceptableValueRange<float>(0, 5), new ConfigurationManagerAttributes { Order = 3 })
            );

            timeMultiplierProtect = Config.Bind(
                "Main Settings",
                "Protect objective Time Multiplier",
                0.5f,
                new ConfigDescription("Multiplies the time it takes to protect task objective. Like when placing a MS2000 marker. 0.5 = time is halved. 2.0 = time is doubled. 0 is instant", new AcceptableValueRange<float>(0, 5), new ConfigurationManagerAttributes { Order = 2 })
            );

            timeMultiplierDisarm = Config.Bind(
                "Main Settings",
                "Disarm tripwire Time Multiplier",
                0.5f,
                new ConfigDescription("Mutiplies the time it takes to disarm placed tripwires, having a multitool affects this further. 0.5 = time is halved. 2.0 = time is doubled. 0 is instant", new AcceptableValueRange<float>(0, 5), new ConfigurationManagerAttributes { Order = 1 })
            );

            LogSource = Logger;

            new LeaveItemPatch().Enable();
            new BeaconPlantPatch().Enable();
            new DisarmTripPatch().Enable();
        }
    }

    internal class LeaveItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Class located in GetActionsClass
            return AccessTools.Method(typeof(LeaveItemClass), nameof(LeaveItemClass.method_0));
        }

        //Save list of objective zoneId's and plantTime to and make sure we're multiplying the base plantTime value on repeat actions
        static List<KeyValuePair<string, float>> LeaveItemList = new List<KeyValuePair<string, float>>(); //zoneId, plantTime

        [PatchPrefix]
        static void Prefix(LeaveItemClass __instance)
        {
            if (!Plugin.enabledPlugin.Value)
                return;

            float plantTime;
            ConditionLeaveItemAtLocation itemToPlant = __instance.class1645_0.resultLeaveItem;
            var pair = LeaveItemList.FirstOrDefault(p => p.Key == itemToPlant.zoneId);
            if (pair.Key != null)
            {
#if DEBUG
                Plugin.LogSource.LogWarning($"READING SAVED PLANTTIME {pair.Value} from zoneId {pair.Key}");
#endif
                plantTime = pair.Value;
            }
            else
            {
                LeaveItemList.Add(new KeyValuePair<string, float>(itemToPlant.zoneId, itemToPlant.plantTime));
                plantTime = itemToPlant.plantTime;
            }

#if DEBUG
            Plugin.LogSource.LogWarning($"BASE LEAVE ITEM TIME {itemToPlant.plantTime}");
            if (__instance.isMultitool)
                Plugin.LogSource.LogWarning($"REPAIRING OBJECTIVE DETECTED");
#endif

            float multiplier = (__instance.isMultitool == true) ? Plugin.timeMultiplierRepair.Value : Plugin.timeMultiplierHide.Value;

            itemToPlant.plantTime = plantTime * multiplier;

#if DEBUG
            Plugin.LogSource.LogWarning($"MODIFIED LEAVE ITEM TIME {itemToPlant.plantTime}");
#endif
        }
    }

    internal class BeaconPlantPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Class is usually +1 of above class on line 71
            return AccessTools.Method(typeof(BeaconPlantClass), nameof(BeaconPlantClass.method_0));
        }

        //Save list of objective zoneId's and plantTime to and make sure we're multiplying the base plantTime value on repeat actions
        static List<KeyValuePair<string, float>> ResultBeaconList = new List<KeyValuePair<string, float>>(); //zoneId, plantTime

        [PatchPrefix]
        static void Prefix(BeaconPlantClass __instance)
        {
            if (!Plugin.enabledPlugin.Value)
                return;

            float plantTime;
            ConditionPlaceBeacon beaconToPlant = __instance.resultBeacon;
            var pair = ResultBeaconList.FirstOrDefault(p => p.Key == beaconToPlant.zoneId);
            if (pair.Key != null)
            {
#if DEBUG
                Plugin.LogSource.LogWarning($"READING SAVED PLANTTIME {pair.Value} from zoneId {pair.Key}");
#endif
                plantTime = pair.Value;
            }
            else
            {
                ResultBeaconList.Add(new KeyValuePair<string, float>(beaconToPlant.zoneId, beaconToPlant.plantTime));
                plantTime = beaconToPlant.plantTime;
            }

#if DEBUG
            Plugin.LogSource.LogWarning($"BASE BEACON PLANT TIME {beaconToPlant.plantTime}");
#endif

            beaconToPlant.plantTime = plantTime * Plugin.timeMultiplierProtect.Value;

#if DEBUG
            Plugin.LogSource.LogWarning($"MODIFIED BEACON PLANT TIME {beaconToPlant.plantTime}");
#endif
        }
    }

    internal class DisarmTripPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(DisarmTrapClass), nameof(DisarmTrapClass.method_0));
        }

        [PatchPrefix]
        static void Prefix(DisarmTrapClass __instance)
        {
            // Adjust plant time based on modifier
            __instance.plantTime *= Plugin.timeMultiplierDisarm.Value;

#if DEBUG
            Plugin.LogSource.LogWarning($"MODIFIED LEAVE ITEM TIME {__instance.plantTime}");
#endif
        }
    }
}
