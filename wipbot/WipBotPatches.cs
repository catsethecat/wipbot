using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wipbot.HarmonyPatches
{
    [HarmonyPatch]
    class WipBotPatches
    {
        [HarmonyPatch(typeof(LevelSelectionNavigationController), "DidActivate")]
        static void Postfix(LevelSelectionNavigationController __instance) { WipbotButtonController.instance.init(__instance.gameObject); }

        [HarmonyPatch(typeof(BeatmapLevelsModel), "Init")]
        static void Postfix(BeatmapLevelsModel __instance) { beatmapLevelsModel = __instance; }

        [HarmonyPatch(typeof(LevelCollectionNavigationController), "DidActivate")]
        static void Postfix(LevelCollectionNavigationController __instance) { navigationController = __instance; }

        [HarmonyPatch(typeof(LevelFilteringNavigationController), "DidActivate")]
        static void Postfix(LevelFilteringNavigationController __instance) { filteringController = __instance; }

        [HarmonyPatch(typeof(SelectLevelCategoryViewController), "DidActivate")]
        static void Postfix(SelectLevelCategoryViewController __instance) { categoryController = __instance; }

        [HarmonyPatch(typeof(LevelSearchViewController), "DidActivate")]
        static void Postfix(LevelSearchViewController __instance) { searchController = __instance; }
    }
}
