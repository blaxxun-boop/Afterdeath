using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace Afterdeath;

public static class GhostWorld
{
	private static bool DefaultPostProcessingSet = false;
	private static ColorGradingModel.Settings DefaultColorGradingSettings;

	[HarmonyPatch(typeof(PostProcessingBehaviour), nameof(PostProcessingBehaviour.OnEnable))]
	private static class SaveDefaultSettings
	{
		private static void Prefix(PostProcessingBehaviour __instance)
		{
			if (__instance.profile != null && !DefaultPostProcessingSet)
			{
				DefaultColorGradingSettings = __instance.profile.colorGrading.settings;
				DefaultPostProcessingSet = true;
			}
		}
	}

	[HarmonyPatch(typeof(PostProcessingBehaviour), nameof(PostProcessingBehaviour.OnPreCull))]
	private static class SetGhostWorldSettings
	{
		private static void Postfix(ref ColorGradingComponent ___m_ColorGrading)
		{
			if ((FejdStartup.instance ? Object.FindObjectOfType<Player>() : Player.m_localPlayer) is { } player && Utils.IsGhost(player))
			{
				ColorGradingModel.TonemappingSettings ctSettings = new()
				{
					tonemapper = ColorGradingModel.Tonemapper.Neutral,
					neutralBlackIn = 20,
					neutralWhiteIn = 10,
					neutralBlackOut = 50,
					neutralWhiteOut = 10,
					neutralWhiteLevel = 5.3f,
					neutralWhiteClip = 30,
				};

				ColorGradingModel.BasicSettings cbSettings = new()
				{
					postExposure = 1,
					temperature = -8,
					tint = 0,
					hueShift = 0,
					saturation = 0,
					contrast = 3,
				};

				ColorGradingModel.ChannelMixerSettings cmSettings = new()
				{
					red = new Vector3(1, 0, 0),
					green = new Vector3(0, 1, 0),
					blue = new Vector3(1, 0, 1),
					currentEditingChannel = 0,
				};

				ColorGradingModel.Settings cSettings = new()
				{
					tonemapping = ctSettings,
					basic = cbSettings,
					channelMixer = cmSettings,
					colorWheels = DefaultColorGradingSettings.colorWheels,
					curves = DefaultColorGradingSettings.curves,
				};

				___m_ColorGrading.model.settings = cSettings;
			}
			else
			{
				___m_ColorGrading.model.settings = DefaultColorGradingSettings;
			}
		}
	}
}
