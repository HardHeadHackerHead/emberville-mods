using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EmbervilleMP.Api;
using HarmonyLib;
using UnityEngine;

namespace DamageColors
{
    // Recolours the damage numbers on your own screen. ClientOnly: only the player who has it needs it, so it never
    // blocks a join. A SOFT dependency: it works without Ember Multiplayer too (the attribute is only read by the
    // multiplayer mod; nothing else here touches it).
    [BepInPlugin("quadslab.emberville.damagecolors", "Damage Colors", "1.0.0")]
    [BepInDependency("dev.emberville.mp", BepInDependency.DependencyFlags.SoftDependency)]
    [MultiplayerMod(Requirement.ClientOnly)]
    public class DamageColorsPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<Color> Colour;

        private void Awake()
        {
            Colour = Config.Bind("Colours", "Damage", new Color(0.45f, 0.85f, 1f), "Colour of damage numbers.");
            new Harmony("quadslab.emberville.damagecolors").PatchAll(typeof(DamageColorsPlugin).Assembly);
            // (the soft-dependency pattern: ask BepInEx first, touch the API only in a method of its own)
            if (Chainloader.PluginInfos.ContainsKey("dev.emberville.mp")) Logger.LogInfo("Damage Colors: with Ember Multiplayer (client only)");
        }
    }

    [HarmonyPatch(typeof(UIDamagePop), nameof(UIDamagePop.ShowDamage))]
    internal static class RecolourPatch
    {
        private static void Postfix(UIDamagePop __instance, UIDamageEvent damageEvent)
        {
            if (damageEvent.Result == DamageResult.Success && damageEvent.DamageAmount > 0f) __instance.label.color = DamageColorsPlugin.Colour.Value;
        }
    }
}
