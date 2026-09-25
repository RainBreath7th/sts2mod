using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using FormVfxKind = HextechRunes.HextechFormVfxSafetyHooks.FormVfxKind;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void HungryExhaustsZeroOneOrTwoCardsByTier()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 1, 0), "tier one should exhaust no cards");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 2, 1), "tier two should exhaust the first card");
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 2, 1), "tier two should not exhaust the second card");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should exhaust the first card");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should exhaust the second card");
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should not exhaust the third card");
	}

	private static void InspectBlocksOnlyTheConfiguredExtraDrawTriggers()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 1, 2, fromHandDraw: true), "normal hand draw should never be blocked");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 1, 0, fromHandDraw: false), "tier one should block no extra draws");

		tracking.BeginPlayerTurnStart([2]);
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "turn-start extra draw should never be blocked");
		Equal(0, tracking.InspectExtraDrawsPreventedThisTurn.Count, "turn-start draw should not consume an inspect trigger");
		tracking.EnterPlayerPlayPhase(2);
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "tier two should block the first in-turn extra draw trigger");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "tier two should allow the second in-turn extra draw trigger");
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should block the first extra draw trigger");
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should block the second extra draw trigger");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should allow the third extra draw trigger");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);
		Equal(2, restored.InspectExtraDrawsPreventedThisTurn[3], "inspect draw count should survive a mid-turn save/load");
	}

	private static void GripConsumesOnlyTheFirstManualCardTrigger()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 1, 0), "tier one should consume no trigger");
		Expect(IGripEnemyHex.TryConsumeFirstCard(tracking, 2, 1), "tier two should consume the first card trigger");
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 2, 1), "tier two should ignore later card triggers");
		Expect(IGripEnemyHex.TryConsumeFirstCard(tracking, 3, 2), "tier three should consume the first card trigger");
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 3, 2), "tier three should ignore later card triggers");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);
		SetEqual(new ulong[] { 2, 3 }, restored.GripPlayersTriggeredThisTurn, "grip guards should survive a mid-turn save/load");
	}

	private static void HungryInspectAndGripShareEightPennyGateTexture()
	{
		const string expected = "res://HextechRunes/images/relics/eightPennyGateRune.png";
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new HungryHex()), "hungry texture");
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new InspectHex()), "inspect texture");
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new GripHex()), "grip texture");
	}

	private static void HappyAccidentUsesAllCombatPilesAtTurnStart()
	{
		CardModel[] combatPileCards =
		[
			CreateMutableTestModel<Dazed>(),
			CreateMutableTestModel<StrikeIronclad>(),
			CreateMutableTestModel<Slimed>()
		];
		Equal(2, HappyAccidentRune.CountStatusCards(combatPileCards), "Happy Accident combat pile Status count");
		Equal(0, HappyAccidentRune.ResolveOrbCount(-1, 1), "Happy Accident negative Status fallback");
		Equal(0, HappyAccidentRune.ResolveOrbCount(3, 0), "Happy Accident disabled orb count");
		Equal(3, HappyAccidentRune.ResolveOrbCount(3, 1), "Happy Accident one orb per Status");

		MethodInfo[] declaredMethods = typeof(HappyAccidentRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(
			declaredMethods.Any(static method => method.Name == nameof(HappyAccidentRune.AfterPlayerTurnStart)),
			"Happy Accident should trigger at player turn start");
		Expect(
			declaredMethods.All(static method => method.Name != "AfterCardGeneratedForCombat"),
			"Happy Accident should no longer trigger when Status cards are generated");
	}

	private static void PrismaticEggIsExcludedFromThirdAct()
	{
		Expect(
			HextechContentRegistry.PlayerRuneMetadata.HasFlag(typeof(PrismaticEggRune), PlayerRuneFlags.ThirdActExcluded),
			"Prismatic Egg should not appear in the third act rune pool");
	}

	private static void MirrorReflectionCopiesCursesButNotBasicCards()
	{
		Expect(MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<Clumsy>()), "Mirror Reflection should duplicate Curse cards");
		Expect(!MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<StrikeIronclad>()), "Mirror Reflection should not duplicate basic Strike cards");
		Expect(!MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<DefendIronclad>()), "Mirror Reflection should not duplicate basic Defend cards");
	}

	private static void MiseryRandomTargetPreservesAttributeTransfer()
	{
		MethodInfo handler = GetAsyncStateMachineMoveNext(typeof(MiseryRune).GetMethod(nameof(MiseryRune.AfterPlayerTurnStart))!);
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(handler)
			.Select(static instruction => instruction.operand).OfType<MethodInfo>().ToArray();
		Equal(1, calls.Count(static call => call.DeclaringType == typeof(HextechRuneTargeting) && call.Name == "PickRandomHittableEnemy"), "one deterministic target shared by both debuffs");
		Expect(calls.All(static call => call.Name != "get_CurrentHp"), "target does not depend on current HP");
		Equal(4, calls.Count(static call => call.Name == "Apply" && call.IsGenericMethod), "both enemy debuffs and both player gains remain");
		MiseryRune rune = CreateMutableTestModel<MiseryRune>();
		Equal(-1m, rune.DynamicVars.Strength.BaseValue, "unchanged Strength transfer");
		Equal(-1m, rune.DynamicVars.Dexterity.BaseValue, "unchanged Dexterity transfer");
	}

	private static void DrainAppliesSummonAmountToAllEnemies()
	{
		MethodInfo handler = GetAsyncStateMachineMoveNext(typeof(DrainRune).GetMethod(nameof(DrainRune.AfterSummon))!);
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(handler)
			.Select(static instruction => instruction.operand).OfType<MethodInfo>().ToArray();
		MethodInfo apply = calls.Single(static method => method.Name == "Apply" && method.IsGenericMethod
			&& method.GetGenericArguments().SequenceEqual(new[] { typeof(DoomPower) }));
		Equal(typeof(IEnumerable<Creature>), apply.GetParameters()[1].ParameterType, "Drain must apply Doom to the enemy collection");
		Expect(calls.Any(static method => method.Name == "get_HittableEnemies"), "Drain uses all hittable enemies");
		Expect(calls.All(static method => method.Name is not "get_CurrentHp" and not "op_Multiply" and not "get_DynamicVars"),
			"Drain neither selects by current HP nor multiplies the summon amount");
	}

	private static void FeyMagicUsesThreeCostWithoutTurnLimit()
	{
		Equal(3, FeyMagicRune.MinimumCardCost, "Fey Magic minimum card cost");

		MethodInfo[] declaredMethods = typeof(FeyMagicRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(declaredMethods.Any(method => method.Name == "AfterDamageGiven"), "Fey Magic should trigger after each qualifying damage event");
		Expect(declaredMethods.All(method => method.Name != "BeforeSideTurnStart"), "Fey Magic should not keep a per-turn trigger reset");
	}

	private static void GiantSlayerScalesFromEnemyMaxHp()
	{
		Equal(1m, GiantSlayerRune.ResolveDamageMultiplier(0), "zero-HP fallback multiplier");
		Equal(1m, GiantSlayerRune.ResolveDamageMultiplier(7), "below first eight-HP step multiplier");
		Equal(1.01m, GiantSlayerRune.ResolveDamageMultiplier(8), "first eight-HP step multiplier");
		Equal(1.49m, GiantSlayerRune.ResolveDamageMultiplier(399), "multiplier before cap");
		Equal(1.5m, GiantSlayerRune.ResolveDamageMultiplier(400), "fifty-percent cap multiplier");
		Equal(1.5m, GiantSlayerRune.ResolveDamageMultiplier(9999), "multiplier remains capped");
	}

	private static void SomethingForNothingDrawsAtZeroAndDiscountsFirstPaidCard()
	{
		Expect(SomethingForNothingRune.IsZeroCostPlay(0m), "zero-cost cards should draw");
		Expect(SomethingForNothingRune.IsZeroCostPlay(-1m), "negative sentinel costs should remain in the zero-cost branch");
		Expect(!SomethingForNothingRune.IsZeroCostPlay(1m), "positive-cost cards should use the discount branch");
		Equal(0, SomethingForNothingRune.ReduceCost(0, 1), "combat discount should not make costs negative");
		Equal(1, SomethingForNothingRune.ReduceCost(2, 1), "combat discount should reduce the card by one");
		Equal(2, SomethingForNothingRune.ReduceCost(2, -1), "negative reductions should be ignored");

		PlayerRuneRegistration registration = HextechPlayerRuneRegistry.Registrations.Single(
			registration => registration.Type == typeof(SomethingForNothingRune));
		Equal(HextechRarityTier.Prismatic, registration.Rarity, "Something for Nothing rarity");
		Equal("RESOURCE", registration.TagKey, "Something for Nothing tag");
		Expect(
			typeof(SomethingForNothingRune).GetMethod(
				nameof(SomethingForNothingRune.BeforeSideTurnStart),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) != null,
			"Something for Nothing should reset its paid-card trigger each turn");
	}

	private static void EchoAddsItsCopyWithoutRecursingThroughGenerationHooks()
	{
		MethodInfo hook = typeof(EchoRune).GetMethod(
			nameof(EchoRune.AfterCardGeneratedForCombat),
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(EchoRune), nameof(EchoRune.AfterCardGeneratedForCombat));
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(GetAsyncStateMachineMoveNext(hook))
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Add)),
			"Echo should add its already-cloned copy directly to the destination pile");
		Expect(
			calls.All(static method => method.DeclaringType != typeof(HextechCardGeneration)),
			"Echo copies must not recursively enter the generated-card hook chain");
	}

	private static void DeathWarrantTriggersPoisonEveryEightDraws()
	{
		MethodInfo availability = typeof(DeathWarrantRune).GetMethod(nameof(HextechRelicBase.IsAvailableForPlayer))
			?? throw new MissingMethodException(nameof(DeathWarrantRune), nameof(HextechRelicBase.IsAvailableForPlayer));
		Equal(typeof(DeathWarrantRune), availability.DeclaringType, "Death Warrant should override the player availability gate");
		Expect(
			PatchProcessor.GetOriginalInstructions(availability)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "IsSilentPlayer"),
			"Death Warrant availability should use the Silent character gate");
		Equal(8, DeathWarrantRune.CardsNeeded, "Death Warrant draw threshold");
		Equal(0, DeathWarrantRune.ResolveThresholdCrossings(0, 7), "Death Warrant should wait for eight draws");
		Equal(1, DeathWarrantRune.ResolveThresholdCrossings(7, 8), "Death Warrant should trigger on the eighth draw");
		Equal(0, DeathWarrantRune.ResolveThresholdCrossings(8, 15), "Death Warrant should preserve progress after triggering");
		Equal(2, DeathWarrantRune.ResolveThresholdCrossings(8, 24), "Death Warrant should recover every missed threshold after load or network delay");

		MethodInfo trigger = typeof(DeathWarrantRune).GetMethod(
			"TriggerPoisonCompat",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(DeathWarrantRune), "TriggerPoisonCompat");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(trigger)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Equal(typeof(PoisonPower), trigger.GetParameters()[0].ParameterType, "Death Warrant poison trigger target type");
		Expect(
			calls.Any(static method => method.Name == nameof(PoisonPower.AfterSideTurnStart)),
			"Death Warrant should use the Poison turn-start path shared by both supported game versions");
	}

	private static void MyriadSwordsUsesShuffleTriggerInsteadOfTurnEnd()
	{
		MethodInfo[] declaredMethods = typeof(MyriadSwordsRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		Expect(declaredMethods.Any(method => method.Name == "AfterShuffle"), "Myriad Swords should trigger after the owner's draw pile is shuffled");
		Expect(declaredMethods.All(method => method.Name != "BeforeTurnEnd"), "Myriad Swords should no longer trigger at turn end");
	}

	private static void MyriadSwordsExplicitlyClosesAStalePlayPile()
	{
		MethodInfo afterShuffle = typeof(MyriadSwordsRune).GetMethod(
			"AfterShuffle",
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(MyriadSwordsRune), "AfterShuffle");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(GetAsyncStateMachineMoveNext(afterShuffle))
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Add)),
			"Myriad Swords should explicitly move a lethal autoplay card out of the Play pile");
	}

	private static void CoefficientRunesStackAdditivelyWithinTheirOwnSector()
	{
		TankEngineRune tankEngine = CreateMutableTestModel<TankEngineRune>();
		tankEngine.SavedStacks = 3;
		Equal(1.18m, tankEngine.MaxHpScale, "three Tank Engine stacks should be 6% + 6% + 6%");

		FeedUpgradeRune feedUpgrade = CreateMutableTestModel<FeedUpgradeRune>();
		feedUpgrade.SavedStacks = 3;
		Equal(1.45m, feedUpgrade.MaxHpScale, "three Feed upgrade triggers should be 15% + 15% + 15%");

		NineDragonPowerRune nineDragon = CreateMutableTestModel<NineDragonPowerRune>();
		nineDragon.SavedStacks = 3;
		Equal(1.09m, nineDragon.MaxHpScale, "three Nine Dragon stacks should be 3% + 3% + 3%");
	}

	private static void CoefficientForgesShareOneAdditiveSector()
	{
		SilverAttackForge silver = CreateMutableTestModel<SilverAttackForge>();
		silver.SavedStackCount = 2;
		GoldAttackForge gold = CreateMutableTestModel<GoldAttackForge>();
		AttackForge prismatic = CreateMutableTestModel<AttackForge>();

		decimal multiplier = HextechForgeCoefficientHelper.CombineBonusFractions(
		[
			silver.DamageBonusFractionTotal,
			gold.DamageBonusFractionTotal,
			prismatic.DamageBonusFractionTotal
		]);

		Equal(1.4m, multiplier, "two silver, one gold and one prismatic attack forge should share a 40% sector");
	}

	private static void MaxHpCoefficientSectorsMultiply()
	{
		decimal multiplier = HextechMaxHpScaling.CombineScales(
			[1.35m, 1.5m, 1.18m, 1.3m],
			[7.5m, 15m, 30m]);

		Equal(4.73718375m, multiplier, "rune sectors should multiply after HP forge bonuses are added into one sector");
	}

	private static void NightmareHooksEveryDarkOrbPassiveTrigger()
	{
		MethodInfo target = HextechNightmareHooks.ResolvePassiveHookTarget();
		Equal(typeof(DarkOrb), target.DeclaringType, "nightmare hook declaring type");
		Equal(nameof(DarkOrb.Passive), target.Name, "nightmare hook method");
		SequenceEqual(
			new[] { typeof(PlayerChoiceContext), typeof(Creature) },
			target.GetParameters().Select(static parameter => parameter.ParameterType),
			"nightmare hook parameter types");
	}

	private static void NightmareEffectRunsOnceAfterEachPassiveTask()
	{
		TaskCompletionSource passive = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource effect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		int effectCount = 0;
		Task wrapped = HextechNightmareHooks.CompletePassiveThen(
			passive.Task,
			() =>
			{
				Interlocked.Increment(ref effectCount);
				return effect.Task;
			});

		Equal(0, effectCount, "nightmare must wait for the dark orb passive");
		Expect(!wrapped.IsCompleted, "nightmare wrapper should await the passive");

		passive.SetResult();
		Expect(
			SpinWait.SpinUntil(() => Volatile.Read(ref effectCount) == 1, TimeSpan.FromSeconds(1)),
			"nightmare effect should begin after the passive completes");
		Expect(!wrapped.IsCompleted, "nightmare wrapper should await its appended damage");

		effect.SetResult();
		wrapped.GetAwaiter().GetResult();
		Equal(1, effectCount, "one passive should append exactly one nightmare effect");

		int repeatedEffectCount = 0;
		for (int i = 0; i < 2; i++)
		{
			HextechNightmareHooks.CompletePassiveThen(
				Task.CompletedTask,
				() =>
				{
					repeatedEffectCount++;
					return Task.CompletedTask;
				}).GetAwaiter().GetResult();
		}
		Equal(2, repeatedEffectCount, "two passive triggers should append exactly two nightmare effects");

		int failedPassiveEffectCount = 0;
		try
		{
			HextechNightmareHooks.CompletePassiveThen(
				Task.FromException(new InvalidOperationException("passive failed")),
				() =>
				{
					failedPassiveEffectCount++;
					return Task.CompletedTask;
				}).GetAwaiter().GetResult();
			throw new InvalidOperationException("failed passive should propagate");
		}
		catch (InvalidOperationException ex) when (ex.Message == "passive failed")
		{
		}
		Equal(0, failedPassiveEffectCount, "failed passive must not append nightmare damage");
	}

	private static void WatchOutGrapefruitFoodPoolHonorsCharacterAndUniqueRelics()
	{
		IReadOnlyList<Type> commonPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: false,
			hasIceCream: false,
			hasNutritiousSoup: false);
		Type[] requestedCommonRelics =
		[
			typeof(ChosenCheese),
			typeof(LastingCandy),
			typeof(NutritiousSoup),
			typeof(BoneTea),
			typeof(EmberTea)
		];
		foreach (Type relicType in requestedCommonRelics)
		{
			Expect(commonPool.Contains(relicType), $"common food pool should contain {relicType.Name}");
		}
		Expect(!commonPool.Contains(typeof(LunarPastry)), "non-Regent food pool should exclude Lunar Pastry");
		Equal(commonPool.Count, commonPool.Distinct().Count(), "common food pool should not contain duplicate relic types");

		IReadOnlyList<Type> regentPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: false,
			hasNutritiousSoup: false);
		Expect(regentPool.Contains(typeof(LunarPastry)), "Regent food pool should contain Lunar Pastry");
		Equal(commonPool.Count + 1, regentPool.Count, "Regent food pool should add only Lunar Pastry");

		IReadOnlyList<Type> iceCreamOwnedPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: true,
			hasNutritiousSoup: false);
		Expect(!iceCreamOwnedPool.Contains(typeof(IceCream)), "owned Ice Cream should stay excluded");
		Expect(iceCreamOwnedPool.Contains(typeof(LunarPastry)), "Ice Cream exclusion should keep Regent Lunar Pastry");
		Equal(regentPool.Count - 1, iceCreamOwnedPool.Count, "owning Ice Cream should remove exactly one candidate");

		IReadOnlyList<Type> nutritiousSoupOwnedPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: false,
			hasNutritiousSoup: true);
		Expect(!nutritiousSoupOwnedPool.Contains(typeof(NutritiousSoup)), "owned Nutritious Soup should stay excluded");
		Expect(nutritiousSoupOwnedPool.Contains(typeof(IceCream)), "Nutritious Soup exclusion should keep Ice Cream");
		Expect(nutritiousSoupOwnedPool.Contains(typeof(LunarPastry)), "Nutritious Soup exclusion should keep Regent Lunar Pastry");
		Equal(regentPool.Count - 1, nutritiousSoupOwnedPool.Count, "owning Nutritious Soup should remove exactly one candidate");
	}

	private static void ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless()
	{
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<SovereignBlade>()), "sovereign blade should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionStrike>()), "minion strike should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionDiveBomb>()), "minion dive bomb should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionSacrifice>()), "minion sacrifice should count as colorless");
	}

	private static void HastyScribbleDrawsToFullHandAtTurnStart()
	{
		Equal(CardPile.MaxCardsInHand, HastyScribbleRune.CalculateCardsToDraw(0), "empty hand draw");
		Equal(6, HastyScribbleRune.CalculateCardsToDraw(4), "partially filled hand draw");
		Equal(0, HastyScribbleRune.CalculateCardsToDraw(CardPile.MaxCardsInHand), "full hand draw");
		Equal(0, HastyScribbleRune.CalculateCardsToDraw(CardPile.MaxCardsInHand + 1), "overfull hand draw");
	}

	private static void BigHandsIncreasesSummonAmountByFiftyPercent()
	{
		Equal(1.5m, BigHandsRune.SummonMultiplier, "Big Hands summon multiplier");
		Equal(15m, BigHandsRune.CalculateSummonAmount(10m), "Big Hands summon amount");
	}

	private static void SpinToWinRecognizesSupportedDelayedResources()
	{
		Expect(SpinToWinRune.IsConvertiblePower(new DrawCardsNextTurnPower()), "next-turn draw should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new EnergyNextTurnPower()), "next-turn energy should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new SummonNextTurnPower()), "next-turn summon should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new StarNextTurnPower()), "next-turn stars should convert");
		Expect(!SpinToWinRune.IsConvertiblePower(new StrengthPower()), "unrelated powers should remain unchanged");
	}

	private static void PlayerSustainRunesUseExpectedMaxHpRules()
	{
		Equal(0, DevilsDanceRune.CountMaxHpTriggers(0, 2, 3), "Devil's Dance should wait for three Attacks");
		Equal(1, DevilsDanceRune.CountMaxHpTriggers(2, 3, 3), "Devil's Dance should trigger on the third Attack");
		Equal(2, DevilsDanceRune.CountMaxHpTriggers(2, 7, 3), "Devil's Dance should preserve thresholds across turns");
		Equal(1, AncientWineRune.CalculateHealAmount(99, 2m), "Ancient Wine should floor two-percent healing with a minimum of one");
		Equal(5, AncientWineRune.CalculateHealAmount(250, 2m), "Ancient Wine should heal two percent of Max HP");
		Equal(2, SturdyRune.CalculateHealAmount(100, 50, 2m, 50m, 5m), "Sturdy should use two percent at exactly half HP");
		Equal(5, SturdyRune.CalculateHealAmount(100, 49, 2m, 50m, 5m), "Sturdy should use five percent below half HP");
	}

	private static void CollectorUsesStrictExecuteThresholdAndSharesFlyingKickExecutions()
	{
		Equal(10m, CollectorRune.ExecutePercent, "Collector execute percent");
		Equal(20, CollectorRune.CountPerExecute, "Collector count per execute");
		Expect(
			CollectorRune.IsBelowExecuteThreshold(9.99m, 100m, CollectorRune.ExecutePercent),
			"Collector should execute below ten percent max HP");
		Expect(
			!CollectorRune.IsBelowExecuteThreshold(10m, 100m, CollectorRune.ExecutePercent),
			"Collector should not execute at exactly ten percent max HP");
		Expect(
			!CollectorRune.IsBelowExecuteThreshold(1m, 0m, CollectorRune.ExecutePercent),
			"Collector should reject invalid max HP thresholds");

		MethodInfo[] declaredMethods = typeof(CollectorRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(
			declaredMethods.Any(static method => method.Name == nameof(CollectorRune.AfterDamageGiven)),
			"Collector should execute from owner damage events");
		Expect(
			declaredMethods.All(static method => method.Name != nameof(CollectorRune.AfterDeath)),
			"Collector should not count unrelated enemy deaths");
		Expect(
			declaredMethods.All(static method => method.Name != nameof(CollectorRune.ModifyDamageMultiplicativeCompat)),
			"Collector should not retain its old damage multiplier");
	}

	private static void DrawYourSwordReplacesOrbEvokeWithTwoFocus()
	{
		var rune = new DrawYourSwordRune();
		Equal(2m, rune.DynamicVars["FocusPower"].BaseValue, "Draw Your Sword Focus per Evoke");

		MethodInfo[] runeMethods = typeof(DrawYourSwordRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		Expect(runeMethods.All(method => method.Name != nameof(DrawYourSwordRune.BeforeSideTurnStart)), "Draw Your Sword should no longer remove Orbs at enemy turn start");
		Expect(runeMethods.Any(method => method.Name == nameof(DrawYourSwordRune.ReplaceOrbEvoke)), "Draw Your Sword should replace each Orb's Evoke effect");

		MethodInfo[] hookMethods = typeof(HextechPlayerRuneHooks).GetMethods(
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Expect(hookMethods.All(method => method.Name != "OrbChannelPrefix"), "Draw Your Sword should no longer intercept Orb channeling");
		Expect(HextechPatcher.FindPatchMethod(typeof(DrawYourSwordRune), "DrawYourSwordEvokePatch", "Apply") != null, "Draw Your Sword should install an Orb Evoke replacement hook");
		Expect(hookMethods.Any(method => method.Name == "OrbEvokePrefix"), "Draw Your Sword should intercept Orb Evoke effects");

		IReadOnlyList<MethodInfo> evokeMethods = HextechPlayerRuneHooks.FindLoadedOrbEvokeMethods();
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(OrbModel)), "Orb Evoke replacement should include the base implementation");
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(LightningOrb)), "Orb Evoke replacement should include concrete Orb implementations");
	}

	private static void PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries()
	{
		HextechMayhemCombatTrackingState tracking = new();
		tracking.EnemyPorcupineTemporaryThornsThisTurn[101] = 2;
		tracking.EnemyPorcupineTemporaryThornsThisTurn[102] = 0;
		tracking.EnemyPorcupineTemporaryThornsThisTurn[103] = -1;

		IReadOnlyList<(uint CombatId, int Thorns)> removal = PorcupineEnemyHex.GetTemporaryThornsToRemove(tracking);

		Equal(1, removal.Count, "porcupine temporary thorns removal count");
		Equal(101u, removal[0].CombatId, "porcupine temporary thorns removal target");
		Equal(2, removal[0].Thorns, "porcupine temporary thorns removal amount");
	}

	private static void BloodPactRequiresHpLossFromEnemyAttack()
	{
		Expect(BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 1, ValueProp.Move), "enemy attack HP loss grants Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 0, ValueProp.Move), "fully blocked attacks do not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Player, 3, ValueProp.Move), "self or allied damage does not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(null, 3, ValueProp.Unpowered), "HP costs and sourceless damage do not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 3, ValueProp.Unpowered), "enemy non-attack damage does not grant Strength");
	}

	private static void ThreeNewRunesHaveRequestedPoolsAndRarities()
	{
		(Type Type, HextechRarityTier Rarity, PlayerRuneCharacterPool? Pool)[] expected =
		[
			(typeof(ScapegoatRune), HextechRarityTier.Gold, null),
			(typeof(BloodDebtRune), HextechRarityTier.Silver, PlayerRuneCharacterPool.Ironclad),
			(typeof(NetherSoulRune), HextechRarityTier.Gold, PlayerRuneCharacterPool.Necrobinder)
		];
		foreach (var entry in expected)
		{
			var actual = HextechPlayerRuneRegistry.Registrations.Single(row => row.Type == entry.Type);
			Equal(entry.Rarity, actual.Rarity, entry.Type.Name + " rarity");
			Equal(entry.Pool, actual.CharacterPool, entry.Type.Name + " character pool");
			Equal(PlayerRuneFlags.None, actual.Flags, entry.Type.Name + " enabled");
		}
	}

	private static void ScapegoatIncludesNegativeAttributesButLeavesBuffs()
	{
		T Power<T>(int amount) where T : PowerModel, new()
		{
			var power = CreateMutableTestModel<T>();
			typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, amount);
			return power;
		}
		var strength = Power<StrengthPower>(-5);
		var dexterity = Power<DexterityPower>(-3);
		var weak = Power<WeakPower>(2);
		weak.SkipNextDurationTick = true;
		var buff = Power<StrengthPower>(4);
		var hex = Power<HexPower>(1);
		var ringing = Power<RingingPower>(1);
		var confused = Power<ConfusedPower>(1);
		var galvanic = Power<HextechGalvanicPower>(2);
		List<PowerModel> powers = [strength, hex, buff, weak, ringing, dexterity, confused, galvanic];
		var snapshot = ScapegoatRune.SnapshotDebuffs(powers);
		Expect(snapshot.SequenceEqual(new PowerModel[] { strength, hex, weak, ringing, dexterity, confused, galvanic }),
			"cleanse includes player-only debuffs in native order, but leaves buffs");
		foreach (var playerOnly in new PowerModel[] { hex, ringing, confused, galvanic, buff })
		{
			Expect(ScapegoatRune.CreateEnemyTransfer(playerOnly) == null,
				playerOnly.GetType().Name + " must never reach enemy application");
		}
		var transfers = snapshot.Select(ScapegoatRune.CreateEnemyTransfer).OfType<PowerModel>().ToArray();
		Expect(transfers.Select(p => p.GetType()).SequenceEqual(new[] { typeof(StrengthPower), typeof(WeakPower), typeof(DexterityPower) }),
			"unsafe effects cannot interrupt transfer of the remaining ordinary debuffs");
		Expect(transfers.Select(p => p.Amount).SequenceEqual(new[] { -5, 2, -3 }), "preserve negative attributes and stacks");
		Expect(!ReferenceEquals(weak, transfers[1]) && !transfers[1].SkipNextDurationTick && weak.SkipNextDurationTick,
			"enemy receives an independent copy without the player's duration exemption");
		powers.Clear();
		Equal(7, snapshot.Length, "removal cannot mutate the cleanse snapshot");
	}

	private static void BloodDebtAccumulatesPerCardAndExpiresAfterCombat()
	{
		var owner = CreateOrdinalTestPlayer(1);
		var rune = CreateMutableTestModel<BloodDebtRune>();
		rune.Owner = owner;
		var first = CreateMutableTestModel<StrikeIronclad>();
		var second = CreateMutableTestModel<StrikeIronclad>();
		var skill = CreateMutableTestModel<DefendIronclad>();
		var foreign = CreateMutableTestModel<StrikeIronclad>();
		first.Owner = second.Owner = skill.Owner = owner;
		foreign.Owner = CreateOrdinalTestPlayer(2);
		rune.GrowAttacks([first, skill, foreign], 7);
		rune.GrowAttacks([first, second], 3);
		decimal Bonus(CardModel card, ValueProp props = ValueProp.Move) =>
			rune.ModifyDamageAdditiveCompat(null, 6m, props, null, card);
		Equal(10m, Bonus(first), "loss events accumulate on the same instance even outside the hand");
		Equal(3m, Bonus(second), "same-name cards only gain while present in hand");
		Equal(0m, Bonus(skill), "skills do not grow");
		Equal(0m, Bonus(foreign), "other players' cards do not grow");
		Equal(0m, Bonus(first, ValueProp.Unpowered), "incidental damage is not an extra attack hit");
		rune.GrowAttacks([first], 0);
		rune.GrowAttacks([first], -5);
		Equal(10m, Bonus(first), "healing and zero loss grant no growth");
		rune.AfterCombatEnd(null!).GetAwaiter().GetResult();
		Equal(0m, Bonus(first), "combat end clears bonuses");
		rune.GrowAttacks([first], 4);
		rune.BeforeCombatStart().GetAwaiter().GetResult();
		Equal(0m, Bonus(first), "combat start also clears stale references");
	}

	private static void NetherSoulSnapshotsCurrentEtherealKeywordsOnce()
	{
		var owner = CreateOrdinalTestPlayer(1);
		typeof(MegaCrit.Sts2.Core.Entities.Players.Player).GetField("<Creature>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(owner, RuntimeHelpers.GetUninitializedObject(typeof(Creature)));
		typeof(MegaCrit.Sts2.Core.Entities.Players.Player).GetField("<Deck>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(owner, new CardPile(PileType.Deck));
		var addedEthereal = CreateMutableTestModel<StrikeIronclad>();
		var ordinary = CreateMutableTestModel<StrikeIronclad>();
		var foreign = CreateMutableTestModel<StrikeIronclad>();
		addedEthereal.Owner = ordinary.Owner = owner;
		foreign.Owner = CreateOrdinalTestPlayer(2);
		addedEthereal.AddKeyword(CardKeyword.Ethereal);
		foreign.AddKeyword(CardKeyword.Ethereal);
		var etherealStatus = CreateMutableTestModel<MegaCrit.Sts2.Core.Models.Cards.Void>();
		var etherealCurse = CreateMutableTestModel<MegaCrit.Sts2.Core.Models.Cards.Injury>();
		etherealStatus.Owner = etherealCurse.Owner = owner;
		etherealCurse.AddKeyword(CardKeyword.Ethereal);
		Expect(etherealStatus.Keywords.Contains(CardKeyword.Ethereal), "Void is natively ethereal");
		List<CardModel> exhausted = [addedEthereal, ordinary, foreign, addedEthereal, etherealStatus, etherealCurse];
		var snapshot = NetherSoulRune.SnapshotEtherealCards(owner, exhausted);
		Equal(1, snapshot.Length, "added keywords count; ordinary, foreign, status and curse cards do not; each instance only once");
		Expect(ReferenceEquals(addedEthereal, snapshot[0]), "play actual exhausted card rather than a copy");
		exhausted.Clear();
		Equal(1, snapshot.Length, "playing and exhausting cards cannot enlarge the batch");
	}

	private static void ThreeNewRuneHooksUseNativeCommandsAndStableTargets()
	{
		MethodInfo[] Calls(Type type, string method) => PatchProcessor.GetOriginalInstructions(
			GetAsyncStateMachineMoveNext(type.GetMethod(method)!)).Select(i => i.operand).OfType<MethodInfo>().ToArray();
		var transfer = Calls(typeof(ScapegoatRune), nameof(ScapegoatRune.AfterPlayerTurnStart));
		Expect(transfer.Any(m => m.Name == "ConsumeCombatProcOrdinal"), "transfer uses synchronized proc ordinal");
		Expect(transfer.Any(m => m.DeclaringType == typeof(HextechRuneTargeting)), "transfer chooses one stable random enemy");
		Expect(transfer.Any(m => m.DeclaringType == typeof(MegaCrit.Sts2.Core.Commands.PowerCmd) && m.Name == "Apply"), "transfer keeps native application and artifact handling");
		var replay = Calls(typeof(NetherSoulRune), nameof(NetherSoulRune.AfterSideTurnEndLate));
		Expect(replay.Any(m => m.DeclaringType == typeof(HextechAutoPlayHelper)), "exhausted cards use native autoplay");
		Expect(!replay.Any(m => m.Name == "CanPlay"), "zero energy must not block autoplay");
		Expect(replay.Any(m => m.Name == "Contains" && m.IsGenericMethod && m.GetGenericArguments().Contains(typeof(Creature))), "only the owner's turn including extra-turn participation");
	}

	private static void FiveNewRunesHaveRequestedPoolsAndRarities()
	{
		(Type Type, HextechRarityTier Rarity, PlayerRuneCharacterPool? Pool)[] expected =
		[
			(typeof(RallyingCallRune), HextechRarityTier.Gold, null),
			(typeof(EndlessRotationRune), HextechRarityTier.Prismatic, null),
			(typeof(VenomousBladeRune), HextechRarityTier.Prismatic, PlayerRuneCharacterPool.Silent),
			(typeof(MyriadManifestationsRune), HextechRarityTier.Prismatic, PlayerRuneCharacterPool.Defect),
			(typeof(KingdomArmyRune), HextechRarityTier.Prismatic, PlayerRuneCharacterPool.Regent)
		];
		foreach (var entry in expected)
		{
			PlayerRuneRegistration actual = HextechPlayerRuneRegistry.Registrations.Single(row => row.Type == entry.Type);
			Equal(entry.Rarity, actual.Rarity, entry.Type.Name + " rarity");
			Equal(entry.Pool, actual.CharacterPool, entry.Type.Name + " pool");
			Equal(PlayerRuneFlags.None, actual.Flags, entry.Type.Name + " enabled in normal selections");
		}
	}

	private static void RallyingCallSnapshotsSameModelCardsWithoutSourceOrOtherPlayers()
	{
		Player owner = CreateOrdinalTestPlayer(1);
		StrikeIronclad source = CreateMutableTestModel<StrikeIronclad>();
		StrikeIronclad first = CreateMutableTestModel<StrikeIronclad>();
		StrikeIronclad upgraded = CreateMutableTestModel<StrikeIronclad>();
		StrikeIronclad foreign = CreateMutableTestModel<StrikeIronclad>();
		DefendIronclad other = CreateMutableTestModel<DefendIronclad>();
		MegaCrit.Sts2.Core.Commands.CardCmd.Upgrade(upgraded);
		source.Owner = first.Owner = upgraded.Owner = other.Owner = owner;
		foreign.Owner = CreateOrdinalTestPlayer(2);
		List<CardModel> candidates = [source, first, other, upgraded, foreign, first];
		CardModel[] result = RallyingCallRune.SnapshotMatches(source, candidates);
		candidates.Clear();
		Equal(2, result.Length, "only two distinct owned copies, even when one is upgraded");
		Expect(ReferenceEquals(first, result[0]) && ReferenceEquals(upgraded, result[1]), "snapshot keeps pile order");
		Expect(!result.Contains(source), "returned source is not its own matching card");
		RallyingCallRune rune = CreateMutableTestModel<RallyingCallRune>();
		typeof(RallyingCallRune).GetField("_playingMatches", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(rune, true);
		Expect(rune.AfterCardPlayed(null!, CreateCardPlay(first)).IsCompletedSuccessfully, "an active matching batch cannot recursively start another batch");
	}

	private static void EndlessRotationFreesBothCostsUntilTurnEnd()
	{
		MeteorShower card = CreateMutableTestModel<MeteorShower>();
		Player owner = CreateOrdinalTestPlayer(1);
		Creature creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		typeof(Player).GetField("<Creature>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, creature);
		typeof(Creature).GetField("<Side>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(creature, CombatSide.Player);
		EndlessRotationRune rune = CreateMutableTestModel<EndlessRotationRune>();
		rune.Owner = owner;
		card.Owner = owner;
		card.EnergyCost.SetThisCombat(2);
		card.SetStarCostThisCombat(3);
		rune.MakeFreeForTurn(card);
		Equal(0, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "energy is free");
		Equal(0, card.CurrentStarCost, "stars are free");
		card.EnergyCost.AfterCardPlayedCleanup();
		Equal(0, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "playing and returning the card does not clear free energy");
		var costs = (List<TemporaryCardCost>)typeof(CardModel).GetField("_temporaryStarCosts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(card)!;
		costs.RemoveAll(cost => cost.ClearsWhenCardIsPlayed);
		Expect(rune.TryModifyStarCost(card, card.CurrentStarCost, out decimal freeStars), "star-cost hook retains free play after native post-play cleanup");
		Equal(0m, freeStars, "stars stay free when played again");
		rune.MakeFreeForTurn(card);
		card.EndOfTurnCleanup();
		rune.AfterSideTurnEndLate(null!, CombatSide.Player, [creature]).GetAwaiter().GetResult();
		Equal(2, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "original combat energy cost returns next turn");
		Equal(3, card.CurrentStarCost, "original combat star cost returns next turn");
		Expect(!rune.TryModifyStarCost(card, 3m, out decimal restoredStars) && restoredStars == 3m, "star-cost hook also expires at turn end");
	}

	private static void MyriadManifestationsCountsTypesRatherThanSlots()
	{
		Equal(0, MyriadManifestationsRune.CountOrbTypes([]), "empty queue has no extra rounds");
		Equal(1, MyriadManifestationsRune.CountOrbTypes([new LightningOrb(), new LightningOrb(), new LightningOrb()]), "three lightning count as one type");
		Equal(2, MyriadManifestationsRune.CountOrbTypes([new LightningOrb(), new FrostOrb(), new LightningOrb()]), "lightning plus frost grant two extra rounds");
		Equal(4, MyriadManifestationsRune.CountOrbTypes([new LightningOrb(), new FrostOrb(), new DarkOrb(), new PlasmaOrb()]), "plasma counts as a different orb type");
	}

	private static void VenomousBladeReadsEachTargetPoisonWithoutExtraDamageEvents()
	{
		Player owner = CreateOrdinalTestPlayer(1);
		Creature dealer = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		typeof(Player).GetField("<Creature>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, dealer);
		VenomousBladeRune rune = CreateMutableTestModel<VenomousBladeRune>();
		rune.Owner = owner;
		Shiv shiv = UninitializedCard<Shiv>();
		typeof(AbstractModel).GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(shiv, true);
		shiv.Owner = owner;
		Creature enemy = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		typeof(Creature).GetField("<Side>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, CombatSide.Enemy);
		// Power 构造器初始化 Godot 颜色资源；CLI 只需携带层数的内存模型。
		PoisonPower poison = (PoisonPower)RuntimeHelpers.GetUninitializedObject(typeof(PoisonPower));
		typeof(AbstractModel).GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(poison, true);
		typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(poison, 17);
		typeof(Creature).GetField("_powers", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, new List<PowerModel> { poison });
		Equal(17m, rune.ModifyDamageAdditiveCompat(enemy, 4m, ValueProp.Move, dealer, shiv), "add target poison to each shiv hit");
		typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(poison, 31);
		Equal(31m, rune.ModifyDamageAdditiveCompat(enemy, 4m, ValueProp.Move, dealer, shiv), "later hits read current poison");
		Equal(0m, rune.ModifyDamageAdditiveCompat(enemy, 4m, ValueProp.Unpowered, dealer, shiv), "do not amplify incidental unpowered damage");
		Equal(0m, rune.ModifyDamageAdditiveCompat(null, 4m, ValueProp.Move, dealer, shiv), "untargeted preview does not invent poison");
		StrikeIronclad strike = CreateMutableTestModel<StrikeIronclad>();
		strike.Owner = owner;
		Equal(0m, rune.ModifyDamageAdditiveCompat(enemy, 4m, ValueProp.Move, dealer, strike), "ordinary attacks do not get poison damage");
	}

	private static void FiveNewRuneHooksKeepNativeExecutionAndSynchronizedRandom()
	{
		MethodInfo[] Calls(Type type, string method) => PatchProcessor.GetOriginalInstructions(
			GetAsyncStateMachineMoveNext(type.GetMethod(method)!)).Select(i => i.operand).OfType<MethodInfo>().ToArray();
		MethodInfo[] rally = Calls(typeof(RallyingCallRune), nameof(RallyingCallRune.AfterCardPlayed));
		Expect(rally.Any(m => m.Name == nameof(HextechAutoPlayHelper.AutoPlayOrMoveToResultPile)), "same-name cards use actual autoplay");
		Expect(!rally.Any(m => m.Name == "CanPlay"), "autoplay does not require remaining energy");
		MethodInfo[] orbs = Calls(typeof(MyriadManifestationsRune), nameof(MyriadManifestationsRune.BeforeSideTurnEndEarly));
		Expect(orbs.Any(m => m.DeclaringType == typeof(HextechOrbPassiveCompat) && m.Name == "TriggerPassive"), "extra passives use the version-matched native entry");
		MethodInfo entry = typeof(HextechOrbPassiveCompat).GetMethod("TriggerPassive", BindingFlags.Static | BindingFlags.NonPublic)!;
		MethodInfo[] passive = PatchProcessor.GetOriginalInstructions(entry.GetCustomAttribute<AsyncStateMachineAttribute>() == null ? entry : GetAsyncStateMachineMoveNext(entry)).Select(i => i.operand).OfType<MethodInfo>().ToArray();
		Expect(passive.Any(m => m.DeclaringType == typeof(OrbModel) && m.Name == "TriggerPassive"
			|| m.DeclaringType == typeof(MegaCrit.Sts2.Core.Commands.OrbCmd) && m.Name == "Passive"), "native passive entry preserves trigger modifiers");
		MethodInfo[] forge = Calls(typeof(KingdomArmyRune), nameof(KingdomArmyRune.AfterForge));
		Equal(1, forge.Count(m => m.Name == "ConsumeCombatProcOrdinal"), "one synchronized ordinal per forge event");
		Equal(1, forge.Count(m => m.Name == nameof(HextechStableRandom.CreateMinionCard)), "one minion per forge event");
		Expect(forge.Any(m => m.Name == nameof(HextechCardGeneration.AddGeneratedCardToCombat)), "generated minions use normal hand and overflow handling");
	}

	private static void MultiplayerSupportRunesHaveRequestedRaritiesAndNumbers()
	{
		(Type Type, HextechRarityTier Rarity)[] expected =
		[
			(typeof(DiveBomberRune), HextechRarityTier.Silver),
			(typeof(AllForYouRune), HextechRarityTier.Gold),
			(typeof(BlossomBladeRune), HextechRarityTier.Prismatic),
			(typeof(OurHealingRune), HextechRarityTier.Gold)
		];
		foreach (var entry in expected)
		{
			PlayerRuneRegistration actual = HextechPlayerRuneRegistry.Registrations.Single(row => row.Type == entry.Type);
			Equal(entry.Rarity, actual.Rarity, entry.Type.Name + " rarity");
			Equal(PlayerRuneFlags.None, actual.Flags, entry.Type.Name + " enabled");
			Equal(null, actual.CharacterPool, entry.Type.Name + " is not character-specific");
		}

		// 测试进程不是联机局,仅联机的海克斯必须不可用。
		Player solo = CreateOrdinalTestPlayer(1);
		Expect(!CreateMutableTestModel<DiveBomberRune>().IsAvailableForPlayer(solo), "dive bomber is multiplayer-only");
		Expect(!CreateMutableTestModel<AllForYouRune>().IsAvailableForPlayer(solo), "all for you is multiplayer-only");
		Expect(!CreateMutableTestModel<BlossomBladeRune>().IsAvailableForPlayer(solo), "blossom blade is multiplayer-only");

		Equal(40m, DiveBomberRune.GetDamage(80m, 50m), "dive bomber deals half of max HP");
		Equal(37m, DiveBomberRune.GetDamage(75m, 50m), "odd max HP rounds down");
		Equal(1.25m, AllForYouRune.SustainMultiplier, "all for you is +25%");
		Equal(1m, BlossomBladeRune.GetHealAmount(80m, 2m), "2% of 80 rounds down to 1");
		Equal(3m, BlossomBladeRune.GetHealAmount(150m, 2m), "2% of 150 is 3");
		Equal(1m, BlossomBladeRune.GetHealAmount(20m, 2m), "heal is at least 1");
	}
}
