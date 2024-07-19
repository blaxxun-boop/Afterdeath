using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Afterdeath;

public static class Utils
{
	public static T ConvertStatusEffect<T>(StatusEffect statusEffect) where T : StatusEffect
	{
		T ownSE = ScriptableObject.CreateInstance<T>();

		ownSE.name = statusEffect.name;
		foreach (FieldInfo field in statusEffect.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			field.SetValue(ownSE, field.GetValue(statusEffect));
		}

		return ownSE;
	}

	public static Vector3 GetClosestLocation(Vector3 position)
	{
		IEnumerable<Vector3> locations;
		if (ZNet.instance.IsServer())
		{
			locations = ZoneSystem.instance.m_locationInstances.Values.Where(i => i.m_location.m_prefabName == Afterdeath.spiritHealerLocation.location.name).Select(i => i.m_position);
		}
		else
		{
			locations = ZoneSystem.instance.m_locationIcons.Where(kv => kv.Value == Afterdeath.spiritHealerLocation.location.name).Select(kv => kv.Key);
		}
		return locations.OrderBy(p => global::Utils.DistanceXZ(p, position)).FirstOrDefault();
	}

	public static bool IsGhost(Player player) => player.m_customData.ContainsKey("Afterdeath Ghost") && !player.IsDead();

	public static bool CarriesTombstone(Player player) => (player.GetSEMan().GetStatusEffect(Afterdeath.ghostStatus.NameHash()) as SE_AfterDeath)?.wisp.GetComponent<PlayerGhost>().carriesStone == true;

	public static void ClearCustomData(Player player)
	{
		player.m_customData.Remove("Afterdeath Ghost");
	}

	public static int CurrentDay() => EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds());

	public static int DeathCounter(Player player)
	{
		int counter = 0;
		if (player.m_customData.TryGetValue("Afterdeath Death Counter", out string deathCounter) && deathCounter.StartsWith(CurrentDay() + " ", StringComparison.Ordinal))
		{
			int.TryParse(player.m_customData["Afterdeath Death Counter"].Split(' ')[1], out counter);
		}
		return counter;
	}

	public static float RespawnDelay() => Math.Min(Afterdeath.respawnTime.Value + Math.Max(0, DeathCounter(Player.m_localPlayer) - 1) * Afterdeath.repeatedRespawnMalus.Value, Afterdeath.respawnTimeCap.Value);
}
