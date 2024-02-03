using BepInEx.Logging;
using FairGiants;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.Text;

namespace BlindGiants.Patches;
[HarmonyPatch(typeof(ForestGiantAI))]
public class ForestGiantAIPatch {

	/*
	 * 
	 * Vision Fix
	 * 
	 */

	[HarmonyPatch("LookForPlayers")]
	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> SearchDistancePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new CodeMatcher(instructions)
			// Match method call for EnemyAI::GetAllPlayersInLineOfSight
			.MatchForward(true,
				new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(EnemyAI), "GetAllPlayersInLineOfSight"))
			)

			// Match argument loading ldc.i4.s aka range
			.MatchBack(false,
				new CodeMatch(OpCodes.Ldc_I4_S)
			)
			.Advance(1)

			// Capture argument and pass it through ClampRange
			.InsertAndAdvance(
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ForestGiantAIPatch), "ClampRange"))
			);
		return matcher.InstructionEnumeration();
	}

	[HarmonyPatch("GiantSeePlayerEffect")]
	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> FearDistancePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new CodeMatcher(instructions)
			// Match method call for EnemyAI::GetAllPlayersInLineOfSight
			.MatchForward(true,
				new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(EnemyAI), "HasLineOfSightToPosition"))
			)

			// Match argument loading ldc.i4.s aka range
			.MatchBack(false,
				new CodeMatch(OpCodes.Ldc_I4_S)
			)
			.Advance(1)

			// Capture argument and pass it through ClampRange
			.InsertAndAdvance(
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ForestGiantAIPatch), "ClampRange"))
			);
		return matcher.InstructionEnumeration();
	}

	public static int ClampRange(int range) {
		return ((TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy && Config.Instance.reduceVisionFog) || (TimeOfDay.Instance.currentLevel.levelIncludesSnowFootprints && Config.Instance.reduceVisionSnow)) ? range / Config.Instance.giantFogDivisor : range;
	}

	/*
	 * 
	 * Ship Anti-Camp Fix
	 * 
	 */

	[HarmonyPatch("DoAIInterval")]
	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> GiantRoamPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new CodeMatcher(instructions)
			// Match method call for EnemyAI::SwitchToBehaviourState
			.MatchForward(false,
				new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(EnemyAI), "ChooseFarthestNodeFromPosition"))
			)
			.Advance(3)

			// LeaveShipPatch(this); position = ChooseFarNodeFromShip(this, position)
			.InsertAndAdvance(
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ForestGiantAIPatch), "LeaveShipPatch")),
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldloc_1),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ForestGiantAIPatch), "ChooseFarNodeFromShip")),
				new CodeInstruction(OpCodes.Stloc_1)
			);
		return matcher.InstructionEnumeration();
	}

	//Could be made more general use but that'd require more tricky IL injection above :(
	public static Vector3 ChooseFarNodeFromShip(ForestGiantAI ai, Vector3 farPosition) {
		// return with farPosition if random ship wander disabled
		if (!Config.Instance.randomWander) return farPosition;

		// ship position and random init
		Vector3 ship = StartOfRound.Instance.elevatorTransform.position;
		var random = new System.Random(ai.RoundUpToNearestFive(ai.transform.position.x) + ai.RoundUpToNearestFive(ai.transform.position.z));

		// calculate nodes
		IEnumerable<(GameObject, float)> nodesEnum = ai.allAINodes
			.Select((GameObject node) => (node, Vector3.Distance(ship, node.transform.position)))
			.Where(((GameObject node, float dist) x) => x.dist >= 102)
			.OrderByDescending(((GameObject node, float dist) x) => x.dist + random.Next(-12, 12));
		ai.nodesTempArray = [..nodesEnum.Select(((GameObject node, float dist) x) => x.node)];
		float[] dists = [..nodesEnum.Select(((GameObject node, float dist) x) => x.dist)];
		if (ai.nodesTempArray.Length <= 0) return farPosition;

		// weight = chance to pick each node scaled 0.0-0.5
		float weightBase = (0.7f * dists.Sum() / ai.nodesTempArray.Length) + (0.3f * (dists.First() + dists.Last()));
		// calculate weight
		double weight = Mathf.Clamp(
			// 50% @ 100 dist, 8% @ 152.5 dist, 1% @ 208.5 dist
			weightBase < 152.5 ? (-.8f * (weightBase - 100) + 50) : (-.125f * (weightBase - 152.5f) + 8)
			// scaled 1-50%
			, 1, 50)
			// gets 50% less common over 30-80 nodes
			* Mathf.Clamp(130 - ai.nodesTempArray.Length, 50, 100)
			// divide both calculations by 100 for use as weight
			* .0001;

		// get position
		Vector3 position = farPosition;
		for (int i = 1; i < ai.nodesTempArray.Length; i++) { //skipping 0 as it's the default
			if (random.NextDouble() < weight) break;
			if (!ai.PathIsIntersectedByLineOfSight(ai.nodesTempArray[i].transform.position, calculatePathDistance: false, avoidLineOfSight: false)) {
				ai.mostOptimalDistance = Vector3.Distance(ship, ai.nodesTempArray[i].transform.position);
				position = ai.nodesTempArray[i].transform.position;
				if (i >= ai.nodesTempArray.Length - 1) {
					break;
				}
			}
		}

		//Plugin.Log($"ALL: {ai.allAINodes.Length} POSSIBLE: {ai.nodesTempArray.Length}");
		//Plugin.Log($"WEIGHT: {(0.7 * dists.Sum() / ai.nodesTempArray.Length) + (0.3 * (dists.First() + dists.Last()))} @ {weight * 100.0} | {weight}");
		//Plugin.Log($"TARGET: {position}");
		return position;
	}

	public static void LeaveShipPatch(ForestGiantAI ai) {
		if (!Config.Instance.enhancedAntiCamp) return;
		Plugin.Log("Roaming Away");
		ai.roamPlanet ??= new AISearchRoutine();
		ai.StopSearch(ai.roamPlanet, clear: true);
		ai.roamPlanet.searchWidth = 35f;
	}


	[HarmonyPatch("FinishedCurrentSearchRoutine")]
	[HarmonyPrefix]
	public static void GiantFinishSearch(ForestGiantAI __instance) {
		if (!Config.Instance.enhancedAntiCamp) return;
		Plugin.LogDebug("Giant Finished a Search");
		if (__instance.roamPlanet != null && __instance.roamPlanet.searchWidth < 200f) __instance.roamPlanet.searchWidth = 200f;
	}
	
	/*
	 * 
	 * Aggro Fix
	 * 
	 */

	[HarmonyPatch("LookForPlayers")]
	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> AggroPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new CodeMatcher(instructions)
			.End()

			// Match timeSpentStaring = 0f
			.MatchBack(false,
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(OpCodes.Ldc_R4, 0f),
				new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(ForestGiantAI), "timeSpentStaring"))
			)

			// LowerAllAggro(this)
			.InsertAndAdvance(
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ForestGiantAIPatch), "LowerAllAggro"))
			);
		return matcher.InstructionEnumeration();
	}

	public static void LowerAllAggro(ForestGiantAI ai) {
		if (!(Config.Instance.stealthDecaysWhen == PatchApplyLevel.Always || (Config.Instance.stealthDecaysWhen == PatchApplyLevel.Solo && StartOfRound.Instance.connectedPlayersAmount <= 0))) return;

		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++) {
			ai.playerStealthMeters[i] = Mathf.Clamp(ai.playerStealthMeters[i] - (Time.deltaTime * Config.Instance.passiveStealthDecay), 0f, 1f);
		}
	}

}
