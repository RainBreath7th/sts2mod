using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HextechRunes;
using FormVfxKind = HextechRunes.HextechFormVfxSafetyHooks.FormVfxKind;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void StuffedToRuinChallengeUsesThreeFixedActPlans()
	{
		SequenceEqual(
			new[] { typeof(StuffedToRuinChallengeModifier), typeof(DefenseCounterMasterChallengeModifier), typeof(BruteForceChallengeModifier), typeof(EightPennyGateChallengeModifier), typeof(ListlessChallengeModifier) },
			HextechCustomModelRegistry.CustomChallengeModifierTypes,
			"custom-run challenge registry");
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(StuffedToRuinChallengeModifier)),
			"challenge modifier should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.ForgottenSoul ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.PhrogParasite, MonsterHexKind.ManipulateReality ]),
			new(HextechRarityTier.Silver, [ MonsterHexKind.LeafSlime, MonsterHexKind.DizzySpinning ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(StuffedToRuinChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(StuffedToRuinChallengeModifier), 3, out _),
			"challenge should not schedule a fourth acquisition");
		HextechRunConfigurationSnapshot defaultSnapshot = HextechRuneConfiguration.GetDefaultSnapshot();
		SequenceEqual(new[] { 1, 1, 1 }, defaultSnapshot.PlayerHexCountsByAct, "challenge default player counts");
		SequenceEqual(new[] { 1, 2, 3 }, defaultSnapshot.EnemyHexCountsByAct, "challenge default enemy counts");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "challenge fixed enemy counts");
		Expect(
			defaultSnapshot.RuneRarityWeightsByAct.All(static weights => weights == new HextechRarityWeights(1, 1, 1)),
			"challenge default rarity weights should be 1:1:1 in every act");
	}

	private static void DefenseCounterMasterChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(DefenseCounterMasterChallengeModifier)),
			"defense counter challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.Exoskeleton ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.HundredRefinements, MonsterHexKind.Porcupine ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.ProteinShake, MonsterHexKind.UnmovableMountain ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(DefenseCounterMasterChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"defense counter challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"defense counter challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"defense counter challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(DefenseCounterMasterChallengeModifier), 3, out _),
			"defense counter challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "defense counter challenge fixed enemy counts");
	}

	private static void BruteForceChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(BruteForceChallengeModifier)),
			"brute force challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.Goliath ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.AstralBody, MonsterHexKind.VitalitySurge ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.StatsOnStats, MonsterHexKind.TankEngine ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(BruteForceChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"brute force challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"brute force challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"brute force challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(BruteForceChallengeModifier), 3, out _),
			"brute force challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "brute force challenge fixed enemy counts");
	}

	private static void EightPennyGateChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(EightPennyGateChallengeModifier)),
			"eight-penny gate challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.EightPennyGate ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.IGrip ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.IInspect ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(EightPennyGateChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"eight-penny gate challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"eight-penny gate challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"eight-penny gate challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(EightPennyGateChallengeModifier), 3, out _),
			"eight-penny gate challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 1, 1 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "eight-penny gate challenge fixed enemy counts");
	}

	private static void ListlessChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(ListlessChallengeModifier)),
			"listless challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Gold, [ MonsterHexKind.MonarchsGaze ]),
			new(HextechRarityTier.Silver, [ MonsterHexKind.TheLost, MonsterHexKind.TheForgotten ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.LagavulinMatriarch, MonsterHexKind.MasterOfDuality ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(ListlessChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"listless challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"listless challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"listless challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(ListlessChallengeModifier), 3, out _),
			"listless challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "listless challenge fixed enemy counts");
	}

	private static void PresetChallengesArePairwiseMutuallyExclusive()
	{
		foreach (Type selectedType in HextechCustomModelRegistry.CustomChallengeModifierTypes)
		{
			foreach (Type candidateType in HextechCustomModelRegistry.CustomChallengeModifierTypes)
			{
				Equal(
					selectedType != candidateType,
					HextechPresetChallengeRegistry.AreMutuallyExclusiveChallengeTypes(selectedType, candidateType),
					$"challenge exclusivity {selectedType.Name} -> {candidateType.Name}");
			}
		}

		Expect(
			!HextechPresetChallengeRegistry.AreMutuallyExclusiveChallengeTypes(
				typeof(StuffedToRuinChallengeModifier),
				typeof(HextechSilverRunModifier)),
			"preset challenges should not untick ordinary custom-run modifiers");
	}
}
