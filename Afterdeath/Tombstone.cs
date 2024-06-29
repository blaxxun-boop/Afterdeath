using HarmonyLib;
using UnityEngine;

namespace Afterdeath;

public class TombstoneRange : MonoBehaviour
{
	private Vector3 lastPosition;
	private PlayerGhost ghost = null!;

	public void Awake()
	{
		ghost = Player.m_localPlayer.GetComponentInChildren<PlayerGhost>();
	}

	private void OnTriggerStay(Collider other)
	{
		if (other.GetComponent<Player>() is { } player && player == Player.m_localPlayer)
		{
			if (ghost.resurrectionRemainingTime is null)
			{
				ghost.StartResurrection();
				lastPosition = player.transform.position;
				return;
			}

			if (ghost.resurrectionRemainingTime <= 0)
			{
				player.GetSEMan().RemoveStatusEffect(Afterdeath.ghostStatus);
				TombStone tombstone = GetComponentInParent<TombStone>();
				tombstone.m_container.TakeAll(player);

				return;
			}

			if (lastPosition.DistanceTo(player.transform.position) > 0.2f)
			{
				ghost.ResetResurrection();
			}
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.GetComponent<Player>() is { } player && player == Player.m_localPlayer)
		{
			ghost.ResetResurrection();
		}
	}

	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Start))]
	private static class AddTombStoneRange
	{
		private static void Postfix(TombStone __instance)
		{
			if (Player.m_localPlayer && Utils.IsGhost(Player.m_localPlayer))
			{
				TryAttachTombstone(__instance);
			}
		}
	}

	public static void TryAttachTombstone(TombStone tombStone)
	{
		if (tombStone.FindOwner() == Player.m_localPlayer)
		{
			GameObject range = new("Afterdeath Range");
			range.AddComponent<TombstoneRange>();
			SphereCollider collider = range.AddComponent<SphereCollider>();
			collider.radius = Afterdeath.respawnRange.Value;
			collider.isTrigger = true;
			collider.includeLayers = 1 << LayerMask.NameToLayer("character");
			range.transform.SetParent(tombStone.transform, false);
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetActionProgress))]
	private class AddCraftingAnimation
	{
		private static void Postfix(Player __instance, ref string name, ref float progress)
		{
			if (__instance.GetComponentInChildren<PlayerGhost>() is { resurrectionRemainingTime: { } remainingTime } && remainingTime < Utils.RespawnDelay())
			{
				progress = 1 - remainingTime / Utils.RespawnDelay();
				name = Localization.instance.Localize("$ad_respawning");
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.ClearActionQueue))]
	private class CancelResurrection
	{
		private static void Postfix(Player __instance)
		{
			if (__instance.GetComponentInChildren<PlayerGhost>() is { } ghost)
			{
				ghost.ResetResurrection();
			}
		}
	}
}
