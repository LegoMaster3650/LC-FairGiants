using BepInEx;
using BepInEx.Logging;
using BlindGiants.Patches;
using FairGiants;
using HarmonyLib;

namespace BlindGiants;

[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
public class Plugin : BaseUnityPlugin {
	public const string pluginGuid = "3650.FairGiants";
	public const string pluginName = "FairGiants";
	public const string pluginVersion = "1.0.0";

	private static Plugin Instance;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used")]
	private void Awake() {
		Instance ??= this;

		Logger.LogInfo("Patching giants...");

		var harmony = new Harmony(pluginGuid);
		// Patches
		harmony.PatchAll(typeof(ForestGiantAIPatch));

		Logger.LogInfo("Giants patched!");
		Logger.LogInfo("Loading config...");

		BlindGiants.Config.Bind(base.Config);
		harmony.PatchAll(typeof(ConfigSync));

		Logger.LogInfo("Config loaded!");
	}

	public static void Log(string msg) => Instance.Logger.LogInfo(msg);

	public static void LogError(string msg) => Instance.Logger.LogError(msg);

	public static void LogDebug(string msg) => Instance.Logger.LogDebug(msg);

}
