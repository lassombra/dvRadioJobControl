using DV;
using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;

namespace dvRadioJobControl;

public static class Main
{
    // Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager
    private static bool Load(UnityModManager.ModEntry modEntry)
    {
        modEntry.OnToggle = OnToggle;
        return true;
    }

    private static bool OnToggle(UnityModManager.ModEntry modEntry, bool active)
    {
        Harmony harmony = new Harmony(modEntry.Info.Id);
        if (active) {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        } else {
            harmony.UnpatchAll(modEntry.Info.Id);
        }
        return true;
    }
}
