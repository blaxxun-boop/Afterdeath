using HarmonyLib;
using UnityEngine;

namespace Afterdeath;

public static class BlockStuff
{
	[HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
	private static class DisableAutoPickup
	{
		private static bool Prefix(Player __instance) => !Utils.IsGhost(__instance);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.PlayerAttackInput))]
	private static class DisableAttacking
	{
		private static bool Prefix(Player __instance) => !Utils.IsGhost(__instance);
	}

	[HarmonyPatch(typeof(Character), nameof(Character.UpdateHeatEffects))]
	private static class DisableLavaDamage
	{
		private static bool Prefix(Character __instance) => __instance is Player player && !Utils.IsGhost(player);
	}

	[HarmonyPatch(typeof(Character), nameof(Character.UpdateSmoke))]
	private static class DisableSmoke
	{
		private static bool Prefix(Character __instance) => __instance is Player player && !Utils.IsGhost(player);
	}

	[HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
	private static class DisableTeleport
	{
		private static bool Prefix() => !Utils.IsGhost(Player.m_localPlayer);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.UpdateEnvStatusEffects))]
	private class DisableEnvEffects
	{
		private static bool Prefix() => !Utils.IsGhost(Player.m_localPlayer);
	}

	[HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
	private class DisableSkillLeveling
	{
		private static bool Prefix(Skills __instance) => !Utils.IsGhost(__instance.m_player);
	}

	[HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
	private class DisableExploration
	{
		[HarmonyPriority(Priority.First)]
		private static bool Prefix() => !Utils.IsGhost(Player.m_localPlayer);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.FindHoverObject))]
	private static class DisableInteractText
	{
		private static void Postfix(Player __instance, ref GameObject? hover)
		{
			if (Utils.IsGhost(__instance) && hover is not null && global::Utils.GetPrefabName(hover) != "Skathi")
			{
				hover = null;
			}
		}
	}
}
