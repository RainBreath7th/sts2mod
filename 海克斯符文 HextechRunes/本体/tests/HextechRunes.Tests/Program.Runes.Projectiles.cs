using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
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
	private static void MagicMissileUsesThreeThreePercentHits()
	{
		Equal(3, MagicMissileRune.MissileCount, "Magic Missile hit count");
		Equal(3m, MagicMissileRune.MaxHpDamagePercent, "Magic Missile max-HP damage percent");
		Equal(0.055f, HextechCombatVfx.MagicMissileLaunchIntervalSeconds, "Magic Missile launch interval");
		Equal(0.28f, HextechCombatVfx.MagicMissileBaseFlightSeconds, "Magic Missile base flight duration");
		Equal(0.025f, HextechCombatVfx.MagicMissileFlightStepSeconds, "Magic Missile flight duration step");
		MethodInfo? afterCardPlayed = typeof(MagicMissileRune).GetMethod(
			nameof(MagicMissileRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Magic Missile should not hold the card-play hook open while projectiles resolve");
		Equal(1, MagicMissileRune.CalculateMissileDamage(1), "Magic Missile should deal at least one damage");
		Equal(3, MagicMissileRune.CalculateMissileDamage(100), "Magic Missile should deal three percent of 100 max HP");
		Equal(5, MagicMissileRune.CalculateMissileDamage(199), "Magic Missile should round max-HP damage down");
	}

	private static void TwinFlamesUsesThreeEnergyScaledHits()
	{
		Equal(3, TwinFlamesRune.MissileCount, "Twin Flames hit count");
		Equal(0m, TwinFlamesRune.ResolveMissileDamage(-1m), "Twin Flames should not create negative damage");
		Equal(0m, TwinFlamesRune.ResolveMissileDamage(0m), "zero-cost Skills should resolve to zero missile damage");
		Equal(3m, TwinFlamesRune.ResolveMissileDamage(3m), "Twin Flames damage should equal the played Skill's Energy cost");
		Expect(!TwinFlamesRune.ShouldLaunchMissiles(0m), "zero-cost Skills should not launch Twin Flames missiles");
		Expect(TwinFlamesRune.ShouldLaunchMissiles(1m), "positive-cost Skills should launch Twin Flames missiles");
		MethodInfo? afterCardPlayed = typeof(TwinFlamesRune).GetMethod(
			nameof(TwinFlamesRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Twin Flames should not hold the card-play hook open while projectiles resolve");
		Expect(
			typeof(HextechCombatVfx).GetMethod(
				"PlayTwinFlamesMissile",
				BindingFlags.Static | BindingFlags.NonPublic) != null,
			"Twin Flames should expose its blue-yellow projectile VFX path");
	}

	private static void TwinFlamesKeepsMultiplayerDamageInsideCardAction()
	{
		MethodInfo afterCardPlayed = typeof(TwinFlamesRune).GetMethod(
			nameof(TwinFlamesRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(TwinFlamesRune), nameof(TwinFlamesRune.AfterCardPlayed));
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(afterCardPlayed)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(HextechPlayerContextHelper) && method.Name == nameof(HextechPlayerContextHelper.IsNetworkMultiplayerRun)),
			"Twin Flames should use its multiplayer lockstep path in network runs");
		Expect(
			calls.Any(static method => method.Name == "ResolveVolleyDamageInLockstepAsync"),
			"Twin Flames multiplayer damage should be returned to the current card action");
	}

	private static void ProjectileRunesKeepMultiplayerDamageInsideCardAction()
	{
		foreach (Type runeType in new[] { typeof(MagicMissileRune), typeof(TwinFlamesRune), typeof(LightEmUpRune) })
		{
			MethodInfo afterCardPlayed = runeType.GetMethod(
				nameof(HextechRelicBase.AfterCardPlayed),
				BindingFlags.Instance | BindingFlags.Public)
				?? throw new MissingMethodException(runeType.Name, nameof(HextechRelicBase.AfterCardPlayed));
			MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(afterCardPlayed)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.ToArray();
			Expect(
				calls.Any(static method => method.DeclaringType == typeof(HextechPlayerContextHelper) && method.Name == nameof(HextechPlayerContextHelper.IsNetworkMultiplayerRun)),
				$"{runeType.Name} should select a multiplayer lockstep path");
			Expect(
				calls.Any(static method => method.Name == "ResolveVolleyDamageInLockstepAsync"),
				$"{runeType.Name} should return its multiplayer damage task to the card action");
		}
	}

	private static void LightEmUpUsesSixEnergyScaledTwinFlameMissiles()
	{
		Equal(4, LightEmUpRune.AttacksPerVolley, "Light Em Up attacks per volley");
		Equal(6, LightEmUpRune.MissileCount, "Light Em Up missile count");
		Equal(0m, LightEmUpRune.ResolveMissileDamage(-1m), "Light Em Up should not create negative damage");
		Equal(3m, LightEmUpRune.ResolveMissileDamage(3m), "Light Em Up damage should equal the triggering Attack's Energy cost");

		int progress = 0;
		for (int attackIndex = 0; attackIndex < 3; attackIndex++)
		{
			progress = LightEmUpRune.AdvanceAttackProgress(progress, 1m, out bool launchedEarly);
			Expect(!launchedEarly, "Light Em Up should not launch before the fourth Attack");
		}

		progress = LightEmUpRune.AdvanceAttackProgress(progress, 0m, out bool launchedAtZeroCostThreshold);
		Equal(4, progress, "zero-cost fourth Attack should hold Light Em Up at full progress");
		Expect(!launchedAtZeroCostThreshold, "zero-cost fourth Attack should not launch Light Em Up missiles");
		progress = LightEmUpRune.AdvanceAttackProgress(progress, 0m, out bool launchedWhileStored);
		Equal(4, progress, "additional zero-cost Attacks should preserve stored Light Em Up progress");
		Expect(!launchedWhileStored, "stored Light Em Up progress should wait for a positive-cost Attack");
		progress = LightEmUpRune.AdvanceAttackProgress(progress, 2m, out bool launchedAfterStoredProgress);
		Equal(0, progress, "Light Em Up should reset after launching its stored volley");
		Expect(launchedAfterStoredProgress, "positive-cost Attack should release stored Light Em Up missiles");

		MethodInfo? afterCardPlayed = typeof(LightEmUpRune).GetMethod(
			nameof(LightEmUpRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Light Em Up should not hold the card-play hook open while projectiles resolve");
		Expect(
			typeof(LightEmUpRune).GetMethod(
				nameof(LightEmUpRune.ModifyCardPlayCount),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) == null,
			"Light Em Up should no longer replay the fourth Attack");
		Expect(
			typeof(HextechCombatVfx).GetMethod(
				"PlayTwinFlamesMissile",
				BindingFlags.Static | BindingFlags.NonPublic) != null,
			"Light Em Up should reuse the blue-yellow Twin Flames projectile VFX path");
	}

	private static void PiercingThreadSplitsOneDamageEventBeforeBlock()
	{
		Equal(50m, PiercingThreadRune.PiercingPercent, "Piercing Thread percentage");
		Equal(0, PiercingThreadRune.CalculatePiercingDamage(-1m), "negative damage should not pierce");
		Equal(0, PiercingThreadRune.CalculatePiercingDamage(1m), "one damage should round its piercing half down");
		Equal(2, PiercingThreadRune.CalculatePiercingDamage(5m), "odd piercing damage should round down");
		Equal(5, PiercingThreadRune.CalculatePiercingDamage(10m), "even piercing damage should split evenly");
		Equal(3m, PiercingThreadRune.CalculateBlockableDamage(5m), "the non-piercing remainder should still hit Block");
		Equal(5m, PiercingThreadRune.CalculateBlockableDamage(10m), "half of even damage should remain blockable");
		Equal(5m, 10m - Math.Min(100m, PiercingThreadRune.CalculateBlockableDamage(10m)), "full Block should still take five piercing damage");
		Equal(7m, 11m - Math.Min(4m, PiercingThreadRune.CalculateBlockableDamage(11m)), "piercing damage and block overflow should remain one damage result");

		PlayerRuneRegistration registration = HextechPlayerRuneRegistry.Registrations.Single(
			registration => registration.Type == typeof(PiercingThreadRune));
		Equal(HextechRarityTier.Gold, registration.Rarity, "Piercing Thread rarity");
		Equal("OUTPUT", registration.TagKey, "Piercing Thread tag");
		Expect(
			HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "DamageBlockPatch", "Prefix") != null,
			"Piercing Thread should alter the blockable amount at the original block-consumption boundary");
	}
}
