using System.Reflection;
using Godot;
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
	// 夺金只在自己发钱的异步作用域内、且是金币音效时静音;作用域外与其他音效不受影响。
	private static void GoldrendSfxMuteIsScopedToGoldSounds()
	{
		FieldInfo scopeField = AccessTools.Field(typeof(GoldrendRune), "SuppressGoldSfx");
		AsyncLocal<bool> scope = (AsyncLocal<bool>)scopeField.GetValue(null)!;
		Expect(!GoldrendRune.ShouldSuppressSfx("event:/sfx/ui/gold/gold_1"), "outside the Goldrend scope gold sounds play");

		bool previous = scope.Value;
		scope.Value = true;
		try
		{
			Expect(GoldrendRune.ShouldSuppressSfx("event:/sfx/ui/gold/gold_1"), "inside the scope the small gold sound is muted");
			Expect(GoldrendRune.ShouldSuppressSfx("event:/sfx/ui/gold/gold_3"), "inside the scope the big gold sound is muted");
			Expect(!GoldrendRune.ShouldSuppressSfx("event:/sfx/heal"), "other sounds are never muted");
			Expect(!GoldrendRune.ShouldSuppressSfx(null), "null path is ignored");
		}
		finally
		{
			scope.Value = previous;
		}
	}

	private static void FortuneForgeRewardScalesByStacks()
	{
		FortuneForge forge = CreateMutableTestModel<FortuneForge>();
		Equal(100, forge.ExtraGoldRewardAmount, "single-stack Fortune Forge reward");

		forge.SavedStackCount = 2;
		Equal(200, forge.ExtraGoldRewardAmount, "two-stack Fortune Forge reward");
	}

	private static void InitialForgeGrantRunesPersistPendingTransaction()
	{
		Type[] initialForgeRunes =
		[
			typeof(StatsRune),
			typeof(StatsOnStatsRune),
			typeof(StatsOnStatsOnStatsRune),
			typeof(HailToTheKingRune)
		];
		foreach (Type type in initialForgeRunes)
		{
			Expect(
				type.IsSubclassOf(typeof(InitialForgeGrantRune)),
				$"{type.Name} should use the resumable initial forge transaction");
		}

		StatsOnStatsRune rune = new();
		Expect(!rune.SavedInitialForgeGrantPending, "initial forge transaction should default to completed");
		rune.SavedInitialForgeGrantPending = true;
		Expect(rune.SavedInitialForgeGrantPending, "pending initial forge transaction should be saveable");

		MethodInfo method = typeof(HextechForgeGrantHelper).GetMethod(
			"TryObtainRandomForges",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechForgeGrantHelper), "TryObtainRandomForges");
		Equal(typeof(Task<bool>), method.ReturnType, "initial forge transaction completion result");
	}

	private static void InitialForgeGrantLoadRecoveryPrecedesActRecovery()
	{
		MethodInfo recovery = typeof(HextechRunLifecycleHooks).GetMethod(
			"ResumePendingSelectionTransactionsAfterLoad",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRunLifecycleHooks), "ResumePendingSelectionTransactionsAfterLoad");
		MethodInfo moveNext = GetAsyncStateMachineMoveNext(recovery);
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(moveNext)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		int forgeRecoveryIndex = Array.FindIndex(
			calls,
			static method => method.Name == "ResumePendingInitialForgeGrantsAfterLoad");
		int actRecoveryIndex = Array.FindIndex(
			calls,
			static method => method.Name == "ResumePendingActSelectionAfterLoad");

		Expect(forgeRecoveryIndex >= 0, "load continuation should resume pending initial forge grants");
		Expect(
			actRecoveryIndex > forgeRecoveryIndex,
			"load continuation should finish pending initial forge grants before resuming act selection");
	}

	private static void DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune()
	{
		HextechForgeRarityWeights weights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(65, 25, 10),
			hasDiceManiac: false);

		Equal(65, weights.Silver, "silver weight");
		Equal(25, weights.Gold, "gold weight");
		Equal(10, weights.Prismatic, "prismatic weight");
		Equal(100, weights.Total, "total weight");
	}

	/// <summary>袖珍锻炉的药水槽总数封顶 16:原版 SerializablePotion 的 SlotIndex 只有 4 bit,超出会在联机同步里截断丢药水。</summary>
	private static void PocketForgeKeepsPotionSlotsWithinFourBitSlotIndex()
	{
		Equal(16, PocketForge.MaxSerializablePotionSlots, "potion slot cap must match the 4-bit SlotIndex wire format");
		Equal(2, PocketForge.ClampSlotIncrease(3, 2), "normal stacks add the full two slots");
		Equal(1, PocketForge.ClampSlotIncrease(15, 2), "the last stack only adds what fits");
		Equal(0, PocketForge.ClampSlotIncrease(16, 2), "no slots are added at the cap");
		Equal(0, PocketForge.ClampSlotIncrease(22, 2), "over-cap saves never grow further");
		Equal(0, PocketForge.ClampSlotIncrease(3, -5), "negative requests are ignored");
	}

	/// <summary>掷骰狂人 50%±10、红包 25%±5 的药水式动态掉率:掉落降档、未掉升档、始终夹在 0~100 内,默认偏移就是基础值。</summary>
	private static void ForgeDropChanceAdjustsLikePotionOdds()
	{
		Equal(50, HextechDynamicDropChance.CurrentChance(0, DiceManiacRune.BaseDropChance), "Dice Maniac starts at its base drop chance");
		int offset = HextechDynamicDropChance.NextOffset(0, DiceManiacRune.BaseDropChance, DiceManiacRune.DropChanceStep, dropped: true);
		Equal(40, HextechDynamicDropChance.CurrentChance(offset, DiceManiacRune.BaseDropChance), "a drop lowers Dice Maniac by ten");
		offset = HextechDynamicDropChance.NextOffset(offset, DiceManiacRune.BaseDropChance, DiceManiacRune.DropChanceStep, dropped: false);
		offset = HextechDynamicDropChance.NextOffset(offset, DiceManiacRune.BaseDropChance, DiceManiacRune.DropChanceStep, dropped: false);
		Equal(60, HextechDynamicDropChance.CurrentChance(offset, DiceManiacRune.BaseDropChance), "two misses raise Dice Maniac by twenty");
		for (int i = 0; i < 20; i++)
		{
			offset = HextechDynamicDropChance.NextOffset(offset, DiceManiacRune.BaseDropChance, DiceManiacRune.DropChanceStep, dropped: false);
		}
		Equal(100, HextechDynamicDropChance.CurrentChance(offset, DiceManiacRune.BaseDropChance), "drop chance is capped at one hundred");
		for (int i = 0; i < 40; i++)
		{
			offset = HextechDynamicDropChance.NextOffset(offset, DiceManiacRune.BaseDropChance, DiceManiacRune.DropChanceStep, dropped: true);
		}
		Equal(0, HextechDynamicDropChance.CurrentChance(offset, DiceManiacRune.BaseDropChance), "drop chance is floored at zero");

		Equal(25, HextechDynamicDropChance.CurrentChance(0, RedEnvelopeRune.BaseForgeChance), "Red Envelope forge side starts at twenty-five");
		int envelope = HextechDynamicDropChance.NextOffset(0, RedEnvelopeRune.BaseForgeChance, RedEnvelopeRune.ForgeChanceStep, dropped: true);
		Equal(20, HextechDynamicDropChance.CurrentChance(envelope, RedEnvelopeRune.BaseForgeChance), "a forge drop lowers Red Envelope by five");
		envelope = HextechDynamicDropChance.NextOffset(envelope, RedEnvelopeRune.BaseForgeChance, RedEnvelopeRune.ForgeChanceStep, dropped: false);
		Equal(25, HextechDynamicDropChance.CurrentChance(envelope, RedEnvelopeRune.BaseForgeChance), "a gold result raises Red Envelope by five");
		Equal(-25, HextechDynamicDropChance.ClampOffset(-999, RedEnvelopeRune.BaseForgeChance), "saved offsets are clamped on load");
	}

	private static void DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights()
	{
		HextechForgeRarityWeights defaultWeights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(65, 25, 10),
			hasDiceManiac: true);
		Equal(65, defaultWeights.Silver, "default silver weight");
		Equal(50, defaultWeights.Gold, "default gold weight");
		Equal(20, defaultWeights.Prismatic, "default prismatic weight");
		Equal(135, defaultWeights.Total, "default total weight");

		HextechForgeRarityWeights customWeights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(10, 20, 30),
			hasDiceManiac: true);
		Equal(10, customWeights.Silver, "custom silver weight");
		Equal(40, customWeights.Gold, "custom gold weight");
		Equal(60, customWeights.Prismatic, "custom prismatic weight");
		Equal(110, customWeights.Total, "custom total weight");
	}

	private static void RandomForgeShopRelicUpdatesDisplayedPrice()
	{
		RandomForgeShopRelic relic = new();

		Equal(HextechRuneConfiguration.GetDefaultRandomForgeShopPrice(), relic.DynamicVars["Price"].IntValue, "default displayed forge price");
		relic.SetDisplayedPrice(777);
		Equal(777, relic.DynamicVars["Price"].IntValue, "updated displayed forge price");
		relic.SetDisplayedPrice(99999);
		Equal(9999, relic.DynamicVars["Price"].IntValue, "displayed forge price clamps to config maximum");
		relic.SetDisplayedPrice(-12);
		Equal(0, relic.DynamicVars["Price"].IntValue, "displayed forge price clamps to config minimum");
	}

	private static void BigHammerForgeBonusAvoidsHammerTimeDoubleScaling()
	{
		Equal(15m, BigHammerRune.CalculateForgeAmount(10m, 50m, sourceAlreadyIncludesBonus: false), "direct forge bonus");
		Equal(15m, BigHammerRune.CalculateForgeAmount(15m, 50m, sourceAlreadyIncludesBonus: true), "hammer time propagated forge");
	}

	private static void HundredRefinementsRequiresTwoBodyForges()
	{
		var rune = new HundredRefinementsRune();
		Equal(2, rune.DynamicVars["BodyForges"].IntValue, "Hundred Refinements body forge requirement");
	}
}
