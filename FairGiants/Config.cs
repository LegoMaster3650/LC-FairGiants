﻿using BepInEx.Configuration;
using FairGiants;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace BlindGiants;
public class Config {
	// Sync
	public static ConfigSync Default;
	public static ConfigSync Instance;

	// Vision
	public static ConfigEntry<bool> file_reduceVisionFog;
	public static ConfigEntry<bool> file_reduceVisionSnow;
	public static ConfigEntry<int> file_giantFogDivisor;

	// Ship
	public static ConfigEntry<bool> file_enhancedAntiCamp;
	public static ConfigEntry<bool> file_randomWander;
	
	// Aggro
	public static ConfigEntry<PatchApplyLevel> file_stealthDecaysWhen;
	public static ConfigEntry<float> file_passiveStealthDecay;

	public static void Bind(ConfigFile config) {
		file_reduceVisionFog = config.Bind(
			"Vision",
			"ReduceVisionFog",
			true,
			"If true, divides giant sight range by GiantFogDivisor when a moon is foggy"
		);
		file_reduceVisionSnow = config.Bind(
			"Vision",
			"ReduceVisionSnow",
			true,
			"If true, divides giant sight range by GiantFogDivisor when a moon is snowy"
		);
		file_giantFogDivisor = config.Bind(
			"Vision",
			"GiantFogDivisor",
			3,
			"The amount to divide giant sight range by"
		);
		
		file_enhancedAntiCamp = config.Bind(
			"Ship",
			"EnhancedAntiCamp",
			true,
			"If true, fixes some issues with the base game's anti-camp when losing a player near the ship."
		);
		file_randomWander = config.Bind(
			"Ship",
			"RandomWander",
			true,
			"If true, uses custom logic to wander to a random point away from the ship. If false, uses the vanilla point of the furthest point from the ship."
		);

		file_stealthDecaysWhen = config.Bind(
			"Aggro",
			"StealthDecaysWhen",
			PatchApplyLevel.Solo,
			"When to allow all stealth meters to passively decay when a giant sees no players."
		);
		file_passiveStealthDecay = config.Bind(
			"Aggro",
			"PassiveStealthDecay",
			0.2f,
			new ConfigDescription("How much stealth decays each second when a giant sees no players. Vanilla decay is 0.33", new AcceptableValueRange<float>(0.0f, 1.0f))
		);

		Default = new ConfigSync();
		Instance = new ConfigSync();
	}
}
