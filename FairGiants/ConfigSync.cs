using BepInEx.Configuration;
using BlindGiants;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace FairGiants;

[Serializable]
public class ConfigSync {
	// Sync
	public static bool Synced = false;

	// Vision
	public bool reduceVisionFog;
	public bool reduceVisionSnow;
	public int giantFogDivisor;

	// Ship
	public bool enhancedAntiCamp;
	public bool randomWander;

	// Aggro
	public PatchApplyLevel stealthDecaysWhen;
	public float passiveStealthDecay;
	
	public ConfigSync() {
		reduceVisionFog = Config.file_reduceVisionFog.Value;
		reduceVisionSnow = Config.file_reduceVisionSnow.Value;
		giantFogDivisor = Config.file_giantFogDivisor.Value;

		enhancedAntiCamp = Config.file_enhancedAntiCamp.Value;
		randomWander = Config.file_randomWander.Value;

		stealthDecaysWhen = Config.file_stealthDecaysWhen.Value;
		passiveStealthDecay = Config.file_passiveStealthDecay.Value;
	}

	[HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
	[HarmonyPostfix]
	public static void Init() {
		if (Synced) return;

		Plugin.Log("Syncing configs...");

		if (NetworkManager.Singleton.IsHost) {
			Plugin.Log("Client is host, no need to sync configs!");

			NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("FairGiants-OnRequestSync", OnRequestSync);
			Synced = true;
			return;
		} else {
			Plugin.Log("Requesting config sync");

			NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("FairGiants-OnRecieveSync", OnRecieveSync);
			RequestSync();
		}
	}

	[HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
	[HarmonyPostfix]
	public static void Reset() {
		Synced = false;
		Config.Instance = Config.Default;
	}

	public static void RequestSync() {
		if (!NetworkManager.Singleton.IsClient) return;

		FastBufferWriter writer = new(0, Unity.Collections.Allocator.Temp);
		NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("FairGiants-OnRequestSync", NetworkManager.ServerClientId, writer);
	}

	public static void OnRequestSync(ulong clientId, FastBufferReader reader) {
		if (!NetworkManager.Singleton.IsHost) return;

		Plugin.Log($"Client {clientId} requested config sync");

		BinaryFormatter bf = new();
		using MemoryStream stream = new();
		try {
			bf.Serialize(stream, Config.Default);
		} catch (Exception e) {
			Plugin.LogError($"Error serializing config: {e}");
			return;
		}
		byte[] bytes = stream.ToArray();
		using FastBufferWriter writer = new(bytes.Length + sizeof(int), Allocator.Temp);
		writer.WriteValueSafe(bytes.Length);
		writer.WriteBytesSafe(bytes);
		NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("FairGiants-OnRecieveSync", clientId, writer);
	}

	public static void OnRecieveSync(ulong clientId, FastBufferReader reader) {
		if (!NetworkManager.Singleton.IsClient) return;

		Plugin.Log("Recieved config data from host.");

		if (!reader.TryBeginRead(sizeof(int))) {
			Plugin.LogError("Config sync failed: Could not read size of buffer");
			return;
		}

		reader.ReadValueSafe(out int dataLen);

		if (!reader.TryBeginRead(dataLen)) {
			Plugin.LogError("Config sync failed: Could not read buffer");
			return;
		}

		byte[] data = new byte[dataLen];
		reader.ReadBytesSafe(ref data, dataLen);

		BinaryFormatter bf = new();
		using MemoryStream stream = new(data);

		try {
			Config.Instance = (ConfigSync) bf.Deserialize(stream);
		} catch (Exception e) {
			Plugin.LogError($"Error deserializing config: {e}");
			return;
		}
		
		Plugin.Log("Config values synced with host!");
		Synced = true;
	}
}
