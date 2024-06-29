﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LocalizationManager;
using LocationManager;
using ServerSync;
using UnityEngine;

namespace Afterdeath;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
public class Afterdeath : BaseUnityPlugin
{
	private const string ModName = "Afterdeath";
	private const string ModVersion = "1.0.0";
	private const string ModGUID = "org.bepinex.plugins.afterdeath";

	private static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	public static ConfigEntry<int> respawnTime = null!;
	public static ConfigEntry<int> respawnRange = null!;
	public static ConfigEntry<int> repeatedRespawnMalus = null!;
	public static ConfigEntry<int> respawnTimeCap = null!;
	private static ConfigEntry<Toggle> emptyInventorySkathiSpawn = null!;
	private static ConfigEntry<int> wispMovementSpeedBonus = null!;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	private enum Toggle
	{
		On = 1,
		Off = 0,
	}

	public static LocationManager.Location spiritHealerLocation = null!;
	public static SE_Stats ghostStatus = null!;
	private static AssetBundle assets = null!;
	public static GameObject WispGameObject = null!;

	public void Awake()
	{
		Localizer.Load();

		assets = LocationManager.Location.PrefabManager.RegisterAssetBundle("afterdeath");

		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		configSync.AddLockingConfigEntry(serverConfigLocked);
		respawnTime = config("1 - General", "Respawn Time", 3, new ConfigDescription("Time in seconds you have to stand still close to your corpse to be revived.", new AcceptableValueRange<int>(1, 30)));
		repeatedRespawnMalus = config("1 - General", "Repeated Respawn Malus", 10, new ConfigDescription("Respawn time in seconds to be added for each death on the same ingame day. Use 0 to disable this.", new AcceptableValueRange<int>(0, 30)));
		respawnTimeCap = config("1 - General", "Respawn Time Cap", 120, new ConfigDescription("Maximum time in seconds for the respawn time, including the malus.", new AcceptableValueRange<int>(1, 300)));
		respawnRange = config("1 - General", "Respawn Range", 20, new ConfigDescription("Maximum range from your tombstone to get revived.", new AcceptableValueRange<int>(1, 200)));
		emptyInventorySkathiSpawn = config("1 - General", "Empty Inventory Spawn", Toggle.On, "If on, you will spawn at Skathi, if you die without creating a tombstone. If off, you will spawn in your bed.");
		wispMovementSpeedBonus = config("1 - General", "Wisp Movement Bonus", 50, new ConfigDescription("Movement speed bonus for the wisp.", new AcceptableValueRange<int>(0, 100)));
		wispMovementSpeedBonus.SettingChanged += (_, _) =>
		{
			ghostStatus.m_speedModifier = wispMovementSpeedBonus.Value / 100f;
			if (Player.m_localPlayer?.GetSEMan().GetStatusEffect(ghostStatus.NameHash()) is SE_Stats ghost)
			{
				ghost.m_speedModifier = wispMovementSpeedBonus.Value / 100f;
			}
		};

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);

		void SetLocationAttributes(LocationManager.Location location)
		{
			try
			{
				location.MapIcon = "ghosticon.png";
			}
			catch (FileNotFoundException)
			{
				location.MapIconSprite = assets.LoadAsset<Sprite>(location.MapIcon);
			}
			location.ShowMapIcon = ShowIcon.Always;
			location.Biome = Heightmap.Biome.All & ~Heightmap.Biome.AshLands;
			location.SpawnDistance = new Range(100, 10000);
			location.SpawnAltitude = new Range(5, 500);
			location.MinimumDistanceFromGroup = 500;
			location.Count = 270;
			location.Prioritize = true;
		}

		spiritHealerLocation = new LocationManager.Location(assets, "AD_Location_Skathi");
		spiritHealerLocation.location.transform.Find("PlayerSpawn").gameObject.AddComponent<PlayerSpawn>();
		spiritHealerLocation.location.transform.Find("Skathi").gameObject.AddComponent<Skathi>();
		SetLocationAttributes(spiritHealerLocation);

		LocationManager.Location outerAshlands = new(spiritHealerLocation.location);
		SetLocationAttributes(outerAshlands);
		outerAshlands.Biome = Heightmap.Biome.AshLands;
		outerAshlands.SpawnArea = Heightmap.BiomeArea.Edge;
		outerAshlands.Count = 280;
		LocationManager.Location innerAshlands = new(spiritHealerLocation.location);
		SetLocationAttributes(innerAshlands);
		innerAshlands.Biome = Heightmap.Biome.AshLands;
		innerAshlands.SpawnArea = Heightmap.BiomeArea.Median;
		innerAshlands.ForestThreshold = new Range(0, 0.5f);
		innerAshlands.Count = 300;

		ghostStatus = Utils.ConvertStatusEffect<SE_AfterDeath>(assets.LoadAsset<SE_Stats>("AD_SE_Ghost"));
		ghostStatus.m_speedModifier = wispMovementSpeedBonus.Value / 100f;
		ghostStatus.m_jumpStaminaUseModifier = -1f;
		ghostStatus.m_runStaminaDrainModifier = -1f;
		ghostStatus.m_damageModifier = -1f;
		ghostStatus.m_fallDamageModifier = -1f;
		WispGameObject = assets.LoadAsset<GameObject>("VFX_AD_Ghost_Spirit");
		WispGameObject.AddComponent<PlayerGhost>();
	}

	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	private static class RegisterPrefabs
	{
		private static void Prefix(ZNetScene __instance)
		{
			__instance.m_prefabs.Add(WispGameObject);
			__instance.m_prefabs.Add(assets.LoadAsset<GameObject>("AD_Respawn_End"));
		}
	}

	[HarmonyPatch(typeof(Game), nameof(Game.FindSpawnPoint))]
	private static class MoveToSkathi
	{
		public static bool maySkipSkathi = false;

		private static void Prefix(Game __instance, out bool __state) => __state = __instance.m_respawnAfterDeath || !__instance.m_playerProfile.HaveLogoutPoint(); // is respawn
		
		private static void Postfix(Game __instance, ref Vector3 point, bool __state, ref bool __result)
		{
			if (__state && (!maySkipSkathi || emptyInventorySkathiSpawn.Value == Toggle.On) && __instance.m_playerProfile.HaveDeathPoint())
			{
				point = Utils.GetClosestLocation(__instance.GetPlayerProfile().GetDeathPoint());
				ZNet.instance.SetReferencePosition(point);
				__result = ZNetScene.instance.IsAreaReady(point);
				if (!__result)
				{
					point = Vector3.zero;
				}
			}
			maySkipSkathi = false;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
	private static class StoreGhost
	{
		private static void Prefix(Player __instance)
		{
			if (__instance.m_inventory.NrOfItems() == 0)
			{
				MoveToSkathi.maySkipSkathi = true;
			}
			else
			{
				__instance.m_customData["Afterdeath Ghost"] = "pending";
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Load))]
	private static class LoadGhost
	{
		private static void Postfix(Player __instance)
		{
			if (Utils.IsGhost(__instance))
			{
				__instance.m_seman.AddStatusEffect(ghostStatus);
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	private static class ResurrectionCompatibility
	{
		private static void Postfix(Player __instance)
		{
			if (!__instance.IsDead() && __instance.m_customData.TryGetValue("Afterdeath Ghost", out string state) && state == "pending")
			{
				Utils.ClearCustomData(__instance);
			}
		}
	}

	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GetLocationIcons))]
	private static class HideSkathiPins
	{
		private static void Postfix(Dictionary<Vector3, string> icons)
		{
			if (Player.m_localPlayer && !Utils.IsGhost(Player.m_localPlayer))
			{
				string name = spiritHealerLocation.location.name;
				List<Vector3> remove = icons.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList();
				foreach (Vector3 pos in remove)
				{
					icons.Remove(pos);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateLocationPins))]
	private static class SilenceMinimapLog
	{
		private static readonly MethodInfo DevLog = AccessTools.DeclaredMethod(typeof(ZLog), nameof(ZLog.DevLog));
		private static readonly MethodInfo Log = AccessTools.DeclaredMethod(typeof(ZLog), nameof(ZLog.Log));
		
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(Log) || instruction.Calls(DevLog))
				{
					yield return new CodeInstruction(OpCodes.Pop);
				}
				else
				{
					yield return instruction;
				}
			}
		}
	}
}