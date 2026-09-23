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
	private static void EnemyCompensationDefersHalfDamageRoundedDown()
	{
		Equal((0m, 0), CompensationEnemyHex.SplitDamage(0m), "zero damage split");
		Equal((1m, 0), CompensationEnemyHex.SplitDamage(1m), "one damage stays immediate");
		Equal((1m, 1), CompensationEnemyHex.SplitDamage(2m), "even damage splits evenly");
		Equal((2m, 1), CompensationEnemyHex.SplitDamage(3m), "odd damage rounds the deferred half down");
		Equal((3m, 2), CompensationEnemyHex.SplitDamage(5m), "five damage preserves total after split");
		Equal((3.5m, 2), CompensationEnemyHex.SplitDamage(5.5m), "fractional damage preserves its immediate remainder");
		Equal((500m, 499), CompensationEnemyHex.SplitDamage(999m), "large odd damage split");
	}

	private static void PlayerCompensationRequiresActiveCombatContext()
	{
		Expect(
			CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: true, combatStateMatchesRun: true),
			"Compensation should replace damage during the active combat it belongs to");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: false, currentRoomIsCombat: true, combatStateMatchesRun: true),
			"Compensation should not replace event or other out-of-combat damage");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: false, combatStateMatchesRun: true),
			"Compensation should require the current room to be a combat room");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: true, combatStateMatchesRun: false),
			"Compensation should reject stale combat state from another run");
	}

	private static void NextTurnDamageUsesTurnStartSnapshot()
	{
		Equal(0, HextechNextTurnDamagePower.GetDamageToResolve(5, 0), "new stacks should not resolve during the turn they are applied");
		Equal(5, HextechNextTurnDamagePower.GetDamageToResolve(5, 5), "all stacks present at turn start should resolve");
		Equal(5, HextechNextTurnDamagePower.GetDamageToResolve(8, 5), "stacks added during turn-start hooks should wait for the following turn");
		Equal(3, HextechNextTurnDamagePower.GetDamageToResolve(3, 5), "resolution should never exceed the current amount");
		Equal(0, HextechNextTurnDamagePower.GetDamageToResolve(-1, 5), "negative amounts should never deal damage");
	}

	private static void NextTurnDamageDoesNotRetriggerCompensation()
	{
		Expect(!HextechNextTurnDamagePower.IsResolvingDamage, "next-turn damage guard should start inactive");
		Expect(!CompensationEnemyHex.ShouldSkipDamageReplacement(), "ordinary damage should remain eligible for compensation");

		bool skippedDuringResolution = false;
		HextechNextTurnDamagePower.RunWithDamageResolutionGuard(() =>
		{
			skippedDuringResolution = CompensationEnemyHex.ShouldSkipDamageReplacement();
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedDuringResolution, "next-turn damage must bypass compensation instead of being delayed again");
		Expect(!HextechNextTurnDamagePower.IsResolvingDamage, "next-turn damage guard should reset after guarded work");
	}

	private static void EnemyCompensationSkipsOutbreakPoisonResponse()
	{
		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithOutbreakPowerPoisonResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement();
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedInsideGuard, "outbreak poison response damage should skip compensation replacement");
		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should reset after guarded work");
	}

	private static void EnemyCompensationSkipsSleightOfFleshResponse()
	{
		Expect(!HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse, "sleight response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithSleightOfFleshPowerDebuffResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement();
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedInsideGuard, "sleight of flesh response damage should skip compensation replacement to avoid the poison recursion stack overflow");
		Expect(!HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse, "sleight response guard should reset after guarded work");
	}

	private static void CompensationReplacementGuardScopesAsyncWork()
	{
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should start inactive");
		TaskCompletionSource gate = new();
		bool sawActiveBeforeAwait = false;
		bool sawActiveAfterAwait = false;
		Task guarded = HextechCombatHooks.RunWithCompensationReplacementGuard(async () =>
		{
			sawActiveBeforeAwait = HextechCombatHooks.IsApplyingCompensationReplacement;
			await gate.Task;
			sawActiveAfterAwait = HextechCombatHooks.IsApplyingCompensationReplacement;
		});

		Expect(sawActiveBeforeAwait, "compensation replacement guard should be active before guarded work awaits");
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should not leak to caller context");
		gate.SetResult();
		guarded.GetAwaiter().GetResult();
		Expect(sawActiveAfterAwait, "compensation replacement guard should remain active after await inside guarded work");
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should reset after guarded work");

		HextechScopedDepthGuard enteredTaskGuard = new();
		TaskCompletionSource enteredTaskGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
		bool enteredTaskActiveBeforeAwait = false;
		bool enteredTaskActiveAfterAwait = false;
		bool afterCompletionSawInactiveGuard = false;

		async Task ObserveEnteredTask()
		{
			enteredTaskActiveBeforeAwait = enteredTaskGuard.IsActive;
			await enteredTaskGate.Task;
			enteredTaskActiveAfterAwait = enteredTaskGuard.IsActive;
		}

		enteredTaskGuard.Enter();
		Task enteredTask = ObserveEnteredTask();
		Task wrappedEnteredTask = enteredTaskGuard.WrapEnteredTask(
			enteredTask,
			() =>
			{
				afterCompletionSawInactiveGuard = !enteredTaskGuard.IsActive;
				return Task.CompletedTask;
			});

		Expect(enteredTaskActiveBeforeAwait, "entered task guard should be active before the original task awaits");
		Expect(!enteredTaskGuard.IsActive, "wrapping an entered task should immediately unwind the caller context");
		enteredTaskGate.SetResult();
		wrappedEnteredTask.GetAwaiter().GetResult();
		Expect(enteredTaskActiveAfterAwait, "entered task guard should remain active after await inside the original task");
		Expect(afterCompletionSawInactiveGuard, "entered task completion callback should run after the guarded context exits");
		Expect(!enteredTaskGuard.IsActive, "entered task guard should remain inactive in the caller after completion");

		enteredTaskGuard.Enter();
		enteredTaskGuard.Enter();
		Task nestedSynchronousTask = enteredTaskGuard.WrapEnteredTask(Task.CompletedTask);
		Expect(enteredTaskGuard.IsActive, "wrapping a completed nested task should preserve the parent guard scope");
		nestedSynchronousTask.GetAwaiter().GetResult();
		enteredTaskGuard.Exit();
		Expect(!enteredTaskGuard.IsActive, "nested completed task guard should unwind exactly one depth");
	}

	private static void CompensationReplacementSuppressesSleightOfFleshResponse()
	{
		Expect(
			!HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(true),
			"sleight response should not be suppressed outside compensation replacement");

		bool suppressedInsideGuard = false;
		HextechCombatHooks.RunWithCompensationReplacementGuard(() =>
		{
			suppressedInsideGuard = HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(true);
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(suppressedInsideGuard, "sleight response should be suppressed during compensation replacement");
		Expect(
			!HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(false),
			"sleight response should not be suppressed when the power change would not trigger sleight");
	}
}
