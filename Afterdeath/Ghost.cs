using System;
using System.Collections.Generic;
using UnityEngine;

namespace Afterdeath;

public class SE_AfterDeath : SE_Stats
{
	public GameObject wisp = null!;
	public float startPlayerMass;

	public override void Setup(Character character)
	{
		if (character is Player player)
		{
			if (Afterdeath.repeatedRespawnMalus.Value > 0 && player.m_customData.TryGetValue("Afterdeath Ghost", out string ghostStatus) && ghostStatus == "pending")
			{
				player.m_customData["Afterdeath Death Counter"] = Utils.CurrentDay() + " " + (Utils.DeathCounter(player) + 1);
			}
			
			player.m_customData["Afterdeath Ghost"] = "attached";

			bool original = ZNetView.m_forceDisableInit;
			if (player.GetComponent<ZNetView>().GetZDO() is null)
			{
				ZNetView.m_forceDisableInit = true;
			}
			wisp = Instantiate(Afterdeath.WispGameObject, player.transform);
			ZNetView.m_forceDisableInit = original;

			foreach (Skathi skathi in FindObjectsOfType<Skathi>(true))
			{
				skathi.gameObject.SetActive(true);
			}

			foreach (TombStone tombStone in FindObjectsOfType<TombStone>())
			{
				TombstoneRange.TryAttachTombstone(tombStone);
			}

			Rigidbody rigidbody = player.GetComponent<Rigidbody>();
			startPlayerMass = rigidbody.mass;
			rigidbody.mass = 0.5f;
		}
		base.Setup(character);
	}

	public override void Stop()
	{
		if (m_character is Player player)
		{
			Utils.ClearCustomData(player);
			ZNetScene.instance.Destroy(wisp);
			foreach (TombstoneRange range in FindObjectsOfType<TombstoneRange>())
			{
				Destroy(range.gameObject);
			}
			player.m_lavaHeatLevel = 0;
			player.m_lavaTimer = 1000;
			player.GetComponent<Rigidbody>().mass = startPlayerMass;
		}
		base.Stop();
	}

	public override void OnDamaged(HitData hit, Character attacker)
	{
		hit.ApplyModifier(0);
	}

	public override void ModifyWalkVelocity(ref Vector3 vel)
	{
		base.ModifyWalkVelocity(ref vel);
		Vector3 pos = m_character.transform.position;
		if (m_character.AboveOrInLava() && vel.y < 5 && pos.y < ZoneSystem.instance.GetGroundHeight(pos) + 3)
		{
			vel.y = 5;
		}
		if (m_character.GetLiquidLevel() + 3 > pos.y && vel.y < 3 && m_character.GetLiquidLevel() > ZoneSystem.instance.GetGroundHeight(pos))
		{
			vel.y = 3;
		}
	}
}

public class PlayerGhost : MonoBehaviour
{
	private GameObject spawnVFX = null!;
	private ZNetView netview = null!;
	private Player? player;
	private readonly List<string> activeVisuals = new();
	public float? resurrectionRemainingTime;

	public void Awake()
	{
		netview = GetComponent<ZNetView>();
		spawnVFX = transform.Find("AD_Respawn_Start").gameObject;
		player = GetComponentInParent<Player>();
		if (player is null)
		{
			transform.Find("wisp/Particle System Force Field").gameObject.SetActive(false);
		}
		else if (netview.GetZDO() is { } zdo)
		{
			zdo.Set("player", player.GetZDOID());
		}

		UpdatePlayerVisual();
		UpdatePlayerInitial();
	}

	private void UpdatePlayerInitial()
	{ 
		player!.m_ghostMode = true;
        player.GetComponent<FootStep>().enabled = false;
	}

	private void UpdatePlayerVisual()
	{
		if (player is null)
		{
			if (ZNetScene.instance.FindInstance(netview.GetZDO().GetZDOID("player")) is not { } playerObject)
			{
				return;
			}
			player = playerObject.GetComponent<Player>();
			UpdatePlayerInitial();
		}

		int i = player.m_visual.transform.childCount;
		while (i-- > 0)
		{
			GameObject visual = player.m_visual.transform.GetChild(i).gameObject;
			if (visual.activeSelf)
			{
				visual.SetActive(false);
				activeVisuals.Add(visual.name);
			}
		}
	}

	public void Update()
	{
		UpdatePlayerVisual();

		if (netview.GetZDO() is not { } zdo)
		{
			return;
		}

		long ticks = zdo.GetLong("end time");
		if (ticks == 0)
		{
			resurrectionRemainingTime = null;
		}
		else
		{
			resurrectionRemainingTime = (float)(new DateTime(ticks) - ZNet.instance.GetTime()).TotalSeconds;
		}

		if (resurrectionRemainingTime == null || resurrectionRemainingTime > Utils.RespawnDelay())
		{
			spawnVFX.SetActive(false);
		}
		else
		{
			spawnVFX.SetActive(true);
			ParticleSystem.MainModule flare = spawnVFX.transform.Find("Flare").GetComponent<ParticleSystem>().main;
			flare.startSizeMultiplier = 5 - 4 * resurrectionRemainingTime.Value / Utils.RespawnDelay();
		}
	}

	public void OnDestroy()
	{
		if (player)
		{
			player!.m_ghostMode = false;
			if (player.gameObject.activeInHierarchy && !player.IsDead())
			{
				foreach (string visual in activeVisuals)
				{
					player.m_visual.transform.Find(visual)?.gameObject.SetActive(true);
				}
				player.GetComponent<FootStep>().enabled = true;
			}
		}
	}

	public void StartResurrection()
	{
		netview.GetZDO().Set("end time", ZNet.instance.GetTime().AddSeconds(Utils.RespawnDelay() + 0.2f).Ticks);
	}

	public void ResetResurrection()
	{
		netview.GetZDO().Set("end time", 0L);
	}
}
