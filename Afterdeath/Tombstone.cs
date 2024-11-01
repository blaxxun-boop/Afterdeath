using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Afterdeath;

public class TombstoneRange : MonoBehaviour
{
	private Vector3 lastPosition;
	private ZNetView netview = null!;
	private ZSyncTransform syncTransform = null!;
	public PlayerGhost? ghost;
	private FloatingTerrainDummy? dummy;
	private TombStone tombStone = null!;
	private bool playerInRange = false;
	private bool? isOwner = null;
	private bool carried = false;
	private bool? inLava = null;

	public void Awake()
	{
		tombStone = GetComponent<TombStone>();
		ghost = Player.m_localPlayer?.GetComponentInChildren<PlayerGhost>();
		netview = GetComponent<ZNetView>();
		syncTransform = GetComponent<ZSyncTransform>();
		
		netview.Unregister("OpenRespons");
		netview.Register<bool>("OpenRespons", (uid, success) =>
		{
			if (!ghost)
			{
				GetComponent<Container>().RPC_OpenRespons(uid, success);
				return;
			}
			if (success)
			{
				syncTransform.m_characterParentSync = true;
				Transform attach = ghost!.transform.Find("attach");
				transform.SetParent(attach, false);
				transform.localPosition = Vector3.zero;
				dummy = GetComponent<FloatingTerrain>().m_dummy;
				if (dummy)
				{
					dummy.transform.SetParent(attach, false);
					GetComponent<FloatingTerrain>().enabled = false;
					dummy.transform.localPosition = Vector3.zero;
				}
				netview.GetZDO().Set("Afterdeath Carried", Player.m_localPlayer!.GetZDOID());
			}
			else
			{
				Player.m_localPlayer!.Message(MessageHud.MessageType.Center, "$msg_inuse");
			}
		});
	}

	private void Update()
	{
		if (!netview.IsValid())
		{
			return;
		}
		
		OwnerUpdate();

		if (GetComponent<Rigidbody>() is not { } body)
		{
			return;
		}
		
		bool isKinematic = ZDOMan.instance.GetZDO(netview.GetZDO().GetZDOID("Afterdeath Carried")) is not null;
		if (body.isKinematic != isKinematic)
		{
			GetComponent<FloatingTerrain>().m_lastHeightmap = null;
		}

		GetComponent<FloatingTerrain>().enabled = !isKinematic;
		body.isKinematic = isKinematic;
	}
	
	private void OwnerUpdate()
	{
		if (!ghost)
		{
			return;
		}
		isOwner ??= tombStone.FindOwner() == ghost!.player;
		if (!isOwner.Value)
		{
			return;
		}
		if (Vector3.Distance(ghost!.transform.position, transform.position) > Afterdeath.respawnRange.Value)
		{
			if (playerInRange)
			{
				playerInRange = false;
				ghost.ResetResurrection();
			}
		}
		else
		{
			bool carryingStone = tombStone.m_nview.GetZDO().GetZDOID("Afterdeath Carried") == ghost.player!.GetZDOID();
			if (!carried || carryingStone)
			{
				ghost.carriesStone = carryingStone;
				carried = carryingStone;
			}
			if (carried)
			{
				transform.localPosition = Vector3.zero;
				if (dummy)
				{
					dummy!.transform.localPosition = Vector3.zero;
				}
			}

			Vector3 position = transform.position;
			float groundHeight = ZoneSystem.instance.GetGroundHeight(ghost.transform.position);
			if (!ghost.carriesStone && !playerInRange && position.y < 3500)
			{
				if (inLava == null && WorldGenerator.IsAshlands(position.x, position.z))
				{
					ZoneSystem.instance.GetGroundData(ref position, out Vector3 _, out Heightmap.Biome _, out Heightmap.BiomeArea _, out Heightmap? hmap);
					if (hmap)
					{
						inLava = Mathf.Min(1f, hmap.GetLava(position), global::Utils.SmoothStep(0.1f, 1f, hmap.GetLava(position))) > ghost.player.m_minLavaMaskThreshold;
					}
				}

				if ((ZoneSystem.instance.m_waterLevel > groundHeight && tombStone.GetComponent<Floating>().GetFloatDepth() < 0) || (ghost.player.AboveOrInLava() && inLava == true))
				{
					tombStone.m_nview.InvokeRPC("RequestOpen", Game.instance.GetPlayerProfile().GetPlayerID());
				}
			}
			playerInRange = true;
			
			if (position.y < 2500 && (ZoneSystem.instance.m_waterLevel > groundHeight || ghost.player.AboveOrInLava()))
			{
				if (carryingStone)
				{
					ghost.player.Message(MessageHud.MessageType.Center, "$ad_tombstone_picked_up");
				}
				
				ghost.ResetResurrection();
				
				return;
			}
			
			if (carryingStone)
			{
				if (!Minimap.instance.IsExplored(position))
				{
					ghost.player.Message(MessageHud.MessageType.Center, "$ad_tombstone_picked_up");
					
					return;
				}
				
				tombStone.transform.SetParent(null);
				Vector3 tombStonePosition = position with { y = groundHeight + 0.5f };
				tombStone.m_nview.GetZDO().Set(ZDOVars.s_spawnPoint, tombStonePosition);
				tombStone.m_nview.GetZDO().Set("Afterdeath Carried", ZDOID.None);
				tombStone.transform.position = tombStonePosition;
				if (dummy is not null)
				{
					dummy.transform.SetParent(null);
					dummy.transform.position = tombStonePosition;
				}
				carried = false;
				ghost.carriesStone = false;
			}

			if (ghost.resurrectionRemainingTime is null)
			{
				ghost.StartResurrection();
				lastPosition = ghost.transform.position;
				
				return;
			}

			if (ghost.resurrectionRemainingTime <= 0)
			{
				ghost.player.GetSEMan().RemoveStatusEffect(Afterdeath.ghostStatus);
				TombStone tombstone = GetComponentInParent<TombStone>();
				tombstone.m_container.TakeAll(ghost.player);

				return;
			}

			if (lastPosition.DistanceTo(ghost.transform.position) > 0.2f)
			{
				ghost.ResetResurrection();
			}
		}
	}

	public void OnDestroy()
	{
		if (carried && ghost)
		{
			ghost!.carriesStone = false;
		}
	}

	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Start))]
	private static class AddTombStoneRange
	{
		private static void Postfix(TombStone __instance)
		{
			__instance.gameObject.AddComponent<TombstoneRange>();
		}
	}

	[HarmonyPatch]
	private class AddCraftingAnimation
	{
		private static MethodInfo TargetMethod() => typeof(Player).GetMethods().Single(m => m.Name == nameof(Player.GetActionProgress) && m.GetParameters().Length == 3);
		
		private static void Postfix(Player __instance, ref string name, ref float progress, ref Player.MinorActionData data)
		{
			if (__instance.GetComponentInChildren<PlayerGhost>() is { resurrectionRemainingTime: { } remainingTime } && remainingTime < Utils.RespawnDelay())
			{
				progress = 1 - remainingTime / Utils.RespawnDelay();
				name = "$ad_respawning";
				data = new Player.MinorActionData
				{
					m_type = (Player.MinorActionData.ActionType)(-1),
					m_progressText = name,
					m_duration = 1,
				};
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

	[HarmonyPatch(typeof(TombStone), nameof(TombStone.PositionCheck))]
	private static class DisablePositionCheck
	{
		private static bool Prefix(TombStone __instance) => ZDOMan.instance.GetZDO(__instance.GetComponent<ZNetView>().GetZDO().GetZDOID("Afterdeath Carried")) is null;
	}
}
