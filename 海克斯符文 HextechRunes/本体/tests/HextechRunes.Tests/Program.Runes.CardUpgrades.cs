using System.Reflection;
using System.Runtime.CompilerServices;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static readonly List<(Player Player, int Gold)> UpgradeGoldRewards = [];

	private static T UpgradeTestPower<T>(Creature owner, int amount) where T : PowerModel
	{
		T power = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
		AccessTools.Field(typeof(AbstractModel), "<IsMutable>k__BackingField").SetValue(power, true);
		AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, owner);
		AccessTools.Field(typeof(PowerModel), "_amount").SetValue(power, amount);
		return power;
	}

	private static void RoyaltiesUpgradePaysImmediatelyAndPreservesLegacyAccrual()
	{
		WithImmediateGoldFixture((first, second, _, listener) =>
		{
			RoyaltiesUpgradeRune rune = CreateMutableTestModel<RoyaltiesUpgradeRune>();
			rune.Owner = first;
			RoyaltiesPower power = UpgradeTestPower<RoyaltiesPower>(first.Creature, 19);
			AccessTools.Field(typeof(Creature), "_powers").SetValue(first.Creature, new List<PowerModel> { power });
			rune.BeforeCombatStart().GetAwaiter().GetResult();
			rune.AfterPlayerTurnStart(null!, second).GetAwaiter().GetResult();
			Equal(0, listener.Calls, "teammate's turn does not grant royalties");
			int gold = first.Gold;
			rune.AfterPlayerTurnStart(null!, first).GetAwaiter().GetResult();
			rune.AfterPlayerTurnStart(null!, first).GetAwaiter().GetResult();
			Equal(0, rune.SavedCountThisCombat, "immediate income does not accrue for a second payout");
			Equal(gold + 38, first.Gold, "full royalties amount each turn, paid immediately with no compounding");
			Equal(19, power.Amount, "original combat-end royalties remain intact");
			rune.AfterCombatEnd((CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom))).GetAwaiter().GetResult();
			Equal(0, UpgradeGoldRewards.Count, "no battle-end duplicate");
			// 旧版本保存的尚未领取计数仍能还原并结算一次。
			rune.SavedCountThisCombat = 13;
#if STS2_109_OR_NEWER
			MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.CacheSavedPropertiesForTypeDebug(typeof(RoyaltiesUpgradeRune));
#else
			HextechSavedPropertyBootstrap.InjectModelType(typeof(RoyaltiesUpgradeRune));
#endif
			SerializableRelic saved = rune.ToSerializable();
			int count = saved.Props!.ints!.Single(p => p.name == nameof(RoyaltiesUpgradeRune.SavedCountThisCombat)).value;
			RoyaltiesUpgradeRune loaded = CreateMutableTestModel<RoyaltiesUpgradeRune>();
			loaded.Owner = first;
			loaded.SavedCountThisCombat = count;
			loaded.AfterCombatEnd((CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom))).GetAwaiter().GetResult();
			loaded.AfterCombatEnd((CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom))).GetAwaiter().GetResult();
			Equal(1, UpgradeGoldRewards.Count, "one fixed combat reward");
			Equal((first, 13), UpgradeGoldRewards[0], "legacy saved accrual belongs only to its owner");
			Equal(0, loaded.SavedCountThisCombat, "payout clears accrued counter");
			loaded.BeforeCombatStart().GetAwaiter().GetResult();
			Equal(0, loaded.SavedCountThisCombat, "next combat starts empty");
		});
	}

	private static bool CaptureUpgradeGoldReward(Player player, Reward reward)
	{
		UpgradeGoldRewards.Add((player, ((GoldReward)reward).Amount));
		return false;
	}

	private static void PlayerUpgradeKeywordsAndNoDrawStayOwnerScoped()
	{
		var (_, first, second) = CreatePrismaticEnemyFixture();
		BulletTimeUpgradeRune bullet = CreateMutableTestModel<BulletTimeUpgradeRune>(); bullet.Owner = first;
		BulletTime card = CreateMutableTestModel<BulletTime>(); card.Owner = first;
		NoDrawPower noDraw = UpgradeTestPower<NoDrawPower>(first.Creature, 1);
		Equal(0m, bullet.ModifyPowerAmountGivenMultiplicative(noDraw, first.Creature, 1, first.Creature, card), "suppress only Bullet Time's No Draw");
		Equal(1m, bullet.ModifyPowerAmountGivenMultiplicative(noDraw, first.Creature, 1, first.Creature, null), "other No Draw remains");
		BulletTime foreignBullet = CreateMutableTestModel<BulletTime>(); foreignBullet.Owner = second;
		Equal(1m, bullet.ModifyPowerAmountGivenMultiplicative(noDraw, second.Creature, 1, second.Creature, foreignBullet), "teammate unaffected");
		RebootUpgradeRune reboot = CreateMutableTestModel<RebootUpgradeRune>(); reboot.Owner = first;
		Reboot rebootCard = CreateMutableTestModel<Reboot>(); rebootCard.Owner = first;
		HashSet<CardKeyword> keywords = [CardKeyword.Exhaust];
		Expect(reboot.TryModifyKeywordsInCombat(rebootCard, keywords) && !keywords.Contains(CardKeyword.Exhaust), "owner's Reboot loses exhaust");
		Reboot foreignReboot = CreateMutableTestModel<Reboot>(); foreignReboot.Owner = second; keywords.Add(CardKeyword.Exhaust);
		Expect(!reboot.TryModifyKeywordsInCombat(foreignReboot, keywords) && keywords.Contains(CardKeyword.Exhaust), "teammate Reboot keeps exhaust");
		HangUpgradeRune hang = CreateMutableTestModel<HangUpgradeRune>(); hang.Owner = first;
		Hang hangCard = CreateMutableTestModel<Hang>(); hangCard.Owner = first;
		keywords.Clear();
		Expect(hang.TryModifyKeywordsInCombat(hangCard, keywords) && keywords.Contains(CardKeyword.Exhaust), "upgraded Hang exhausts");
		HextechHangPower power = UpgradeTestPower<HextechHangPower>(second.Creature, 4);
		Equal(4m, power.ModifyDamageMultiplicativeCompat(second.Creature, 3, ValueProp.Unpowered, null, null), "Hang amplifies non-card damage");
		Equal(4m, power.ModifyDamageMultiplicativeCompat(second.Creature, 3, ValueProp.Move, first.Creature, card), "Hang amplifies other attacks");
		Equal(1m, power.ModifyDamageMultiplicativeCompat(first.Creature, 3, ValueProp.Move, second.Creature, card), "Hang is target scoped");
		Equal(8, 4 + HangUpgradeRune.NextIncrease(4), "repeated Hang doubles the multiplier");
	}

	private static void ClawUpgradeSeparatesPermanentGrowthFromNativeCombatGrowth()
	{
		var (_, first, second) = CreatePrismaticEnemyFixture();
		ClawUpgradeRune rune = CreateMutableTestModel<ClawUpgradeRune>(); rune.Owner = first;
		Claw deck = CreateMutableTestModel<Claw>(); deck.Owner = first;
		Claw combat = CreateMutableTestModel<Claw>(); combat.Owner = first; combat.DeckVersion = deck;
		Claw otherCopy = CreateMutableTestModel<Claw>(); otherCopy.Owner = first; otherCopy.DeckVersion = deck;
		Claw foreign = CreateMutableTestModel<Claw>(); foreign.Owner = second;
		AccessTools.Method(typeof(Claw), "BuffFromClawPlay").Invoke(combat, [2m]);
		AccessTools.Method(typeof(Claw), "BuffFromClawPlay").Invoke(otherCopy, [2m]);
		rune.GrowClaws([deck, combat, combat, otherCopy, foreign]);
		Equal(4m, deck.DynamicVars.Damage.BaseValue, "deck grows once despite multiple combat copies");
		Equal(6m, combat.DynamicVars.Damage.BaseValue, "native plus-two is kept only in combat");
		Equal(6m, otherCopy.DynamicVars.Damage.BaseValue, "every current combat copy grows once");
		Equal(3m, foreign.DynamicVars.Damage.BaseValue, "teammate's Claw is unchanged");
		SerializableCard saved = new();
		Type store = typeof(HextechSelfUpgradeCardStore);
		AccessTools.Method(store.GetNestedType("ToSerializablePatch", BindingFlags.NonPublic), "Postfix").Invoke(null, [deck, saved]);
		Equal(1, saved.Props!.ints!.Single(p => p.name == HextechSelfUpgradeCardStore.DamageBonusSavedPropertyName).value, "save contains permanent one, not native combat two");
		Claw loaded = CreateMutableTestModel<Claw>(); loaded.Owner = first;
		AccessTools.Method(store.GetNestedType("FromSerializablePatch", BindingFlags.NonPublic), "Postfix").Invoke(null, [saved, loaded]);
		Equal(4m, loaded.DynamicVars.Damage.BaseValue, "loading restores only permanent growth");
	}

	private static void PersistentPowerUpgradesDoNotAffectOtherPlayers()
	{
		var (_, first, second) = CreatePrismaticEnemyFixture();
		foreach (Player player in new[] { first, second })
		{
			AccessTools.Field(typeof(Creature), "<Player>k__BackingField").SetValue(player.Creature, player);
			AccessTools.Field(typeof(Player), "_relics").SetValue(player, new List<RelicModel>());
		}
		RageUpgradeRune rage = CreateMutableTestModel<RageUpgradeRune>(); rage.Owner = first;
		ReflectUpgradeRune reflect = CreateMutableTestModel<ReflectUpgradeRune>(); reflect.Owner = first;
		((List<RelicModel>)AccessTools.Field(typeof(Player), "_relics").GetValue(first)!).AddRange([rage, reflect]);
		foreach (var (runeType, power, foreignPower) in new (Type, PowerModel, PowerModel)[]
		{
			(typeof(RageUpgradeRune), UpgradeTestPower<RagePower>(first.Creature, 3), UpgradeTestPower<RagePower>(second.Creature, 3)),
			(typeof(ReflectUpgradeRune), UpgradeTestPower<ReflectPower>(first.Creature, 3), UpgradeTestPower<ReflectPower>(second.Creature, 3))
		})
		{
			MethodInfo prefix = AccessTools.Method(runeType.GetNestedTypes(BindingFlags.NonPublic).Single(), "Prefix");
			object?[] args = [power, null];
			Equal(false, (bool)prefix.Invoke(null, args)!, "owner's cleanup is skipped");
			((Task)args[1]!).GetAwaiter().GetResult();
			Equal(true, (bool)prefix.Invoke(null, [foreignPower, null])!, "teammate keeps native cleanup");
		}
	}

	private static void CardUpgradeReplacementBodiesMatchReviewedVanilla()
	{
		(Type Type, string Name)[] targets =
		[
			(typeof(LoopPower), nameof(LoopPower.AfterPlayerTurnStart)),
			(typeof(RagePower), nameof(RagePower.AfterSideTurnEnd)),
			(typeof(ReflectPower), nameof(ReflectPower.AfterSideTurnStart)),
			(typeof(FlakCannon), "OnPlay"), (typeof(Hang), "OnPlay"), (typeof(Neurosurge), "OnPlay"),
			(typeof(InfernoPower), nameof(InfernoPower.AfterDamageReceived)),
			(typeof(FlameBarrierPower), nameof(FlameBarrierPower.AfterDamageReceived))
		];
		var expected = HextechVanillaCopyGuard.LoadExpectedHashes();
		List<string> rows = [];
		foreach (var (type, name) in targets)
		{
			MethodInfo entry = AccessTools.Method(type, name);
			IEnumerable<MethodInfo> methods = entry.GetCustomAttribute<AsyncStateMachineAttribute>() == null
				? [entry] : [entry, GetAsyncStateMachineMoveNext(entry)];
			foreach (MethodInfo method in methods)
			{
				string key = HextechVanillaCopyGuard.DescribeTarget(method);
				string hash = HextechVanillaCopyGuard.ComputeIlHash(method)!;
				rows.Add($"{key}={hash}");
				if (Environment.GetEnvironmentVariable("HEXTECH_WRITE_UPGRADE_GUARD") != "1")
					Expect(expected.TryGetValue(key, out string? frozen) && frozen == hash, "review native upgrade target after IL drift: " + key);
			}
		}
		if (Environment.GetEnvironmentVariable("HEXTECH_WRITE_UPGRADE_GUARD") == "1")
		{
			string path = Path.Combine(FindTestsSourceDirectory(), "..", $"vanilla_copy_guard.{ModInfo.TargetGameVersion}.txt");
			string[] old = File.Exists(path) ? File.ReadAllLines(path) : ["# 原版局部替换守卫：入口与异步结算体。"];
			File.WriteAllLines(path, old.Concat(rows).Distinct());
		}
	}

	private static void SearingAttackRuneGrantsUpgradedCard()
	{
		Expect(typeof(HextechOwnerPoolTokenCard).IsAbstract, "owner-pool token card base should stay abstract");
		Expect(
			!HextechCustomModelRegistry.CustomCardTypes.Contains(typeof(HextechOwnerPoolTokenCard)),
			"owner-pool token card base must not enter the concrete model registry");
		Equal(
			HextechCustomModelRegistry.CustomCardTypes.Count,
			HextechCustomModelRegistry.CustomCardTypes.Count(
				static type => typeof(HextechOwnerPoolTokenCard).IsAssignableFrom(type) && !type.IsAbstract),
			"all registered custom cards should use the owner-pool token card contract");

		SearingAttackCard card = CreateMutableTestModel<SearingAttackCard>();

		SearingAttackRune.UpgradeGrantedCard(card);

		Equal(1, card.CurrentUpgradeLevel, "granted Searing Attack upgrade level");
		Equal(16m, card.DynamicVars.Damage.BaseValue, "granted Searing Attack damage");
	}

	private static void CardUpgradePickupAndAvailabilityRules()
	{
		BloodlettingUpgradeRune singleForm = new();
		Expect(singleForm.GrantsCardOnPickup, "ordinary card upgrade runes should grant their target card");
		Expect(singleForm.HasUponPickupEffect, "ordinary card upgrade runes should advertise their pickup effect");
		Expect(singleForm.MeetsCardAvailabilityRequirement([]), "ordinary card upgrade runes should not require the target card");

		BashUpgradeRune bash = new();
		NeutralizeUpgradeRune neutralize = new();
		FallingStarUpgradeRune fallingStar = new();
		UnleashUpgradeRune unleash = new();
		DualcastUpgradeRune dualcast = new();
		RelicModel[] dualFormRunes = [ bash, neutralize, fallingStar, unleash, dualcast ];
		foreach (RelicModel rune in dualFormRunes)
		{
			Expect(!rune.HasUponPickupEffect, $"{rune.GetType().Name} should not grant a card on pickup");
			Expect(
				rune is IHextechSelectionFooterProvider footerProvider
				&& footerProvider.GetSelectionFooterText() == null,
				$"{rune.GetType().Name} should not show a pickup footer");
		}

		Expect(!bash.MeetsCardAvailabilityRequirement([]), "Bash upgrade should require Bash or Break");
		Expect(bash.MeetsCardAvailabilityRequirement([new Bash()]), "Bash upgrade should accept Bash");
		Expect(bash.MeetsCardAvailabilityRequirement([new Break()]), "Bash upgrade should accept Break");
		Expect(!neutralize.MeetsCardAvailabilityRequirement([]), "Neutralize upgrade should require Neutralize or Suppress");
		Expect(neutralize.MeetsCardAvailabilityRequirement([new Neutralize()]), "Neutralize upgrade should accept Neutralize");
		Expect(neutralize.MeetsCardAvailabilityRequirement([new Suppress()]), "Neutralize upgrade should accept Suppress");
		Expect(!fallingStar.MeetsCardAvailabilityRequirement([]), "Falling Star upgrade should require Falling Star or Meteor Shower");
		Expect(fallingStar.MeetsCardAvailabilityRequirement([new FallingStar()]), "Falling Star upgrade should accept Falling Star");
		Expect(fallingStar.MeetsCardAvailabilityRequirement([new MeteorShower()]), "Falling Star upgrade should accept Meteor Shower");
		Expect(!unleash.MeetsCardAvailabilityRequirement([]), "Unleash upgrade should require Unleash or Protector");
		Expect(unleash.MeetsCardAvailabilityRequirement([new Unleash()]), "Unleash upgrade should accept Unleash");
		Expect(unleash.MeetsCardAvailabilityRequirement([new Protector()]), "Unleash upgrade should accept Protector");
		Expect(!dualcast.MeetsCardAvailabilityRequirement([]), "Dualcast upgrade should require Dualcast or Quadcast");
		Expect(dualcast.MeetsCardAvailabilityRequirement([new Dualcast()]), "Dualcast upgrade should accept Dualcast");
		Expect(dualcast.MeetsCardAvailabilityRequirement([new Quadcast()]), "Dualcast upgrade should accept Quadcast");

		Expect((object)new StrikeUpgradeRune() is not IHextechSelectionFooterProvider, "Strike upgrade should not show a pickup footer");
		Expect((object)new DefendUpgradeRune() is not IHextechSelectionFooterProvider, "Defend upgrade should not show a pickup footer");
		Expect(!StrikeUpgradeRune.HasBasicStrike([]), "Strike upgrade should require a basic Strike");
		Expect(StrikeUpgradeRune.HasBasicStrike([new StrikeIronclad()]), "Strike upgrade should accept a basic Strike");
		Expect(!DefendUpgradeRune.HasBasicDefend([]), "Defend upgrade should require a basic Defend");
		Expect(DefendUpgradeRune.HasBasicDefend([new DefendIronclad()]), "Defend upgrade should accept a basic Defend");
	}

	private static void BashUpgradeStrengthMatchesVulnerableApplied()
	{
		Bash bash = CreateMutableTestModel<Bash>();
		Equal(2m, BashUpgradeRune.CalculateStrengthGain(bash), "base Bash vulnerable and Strength");
		CardCmd.Upgrade(bash);
		Equal(3m, BashUpgradeRune.CalculateStrengthGain(bash), "upgraded Bash vulnerable and Strength");

		Break breakCard = CreateMutableTestModel<Break>();
		Equal(5m, BashUpgradeRune.CalculateStrengthGain(breakCard), "base Break vulnerable and Strength");
		CardCmd.Upgrade(breakCard);
		Equal(7m, BashUpgradeRune.CalculateStrengthGain(breakCard), "upgraded Break vulnerable and Strength");

		Equal(0m, BashUpgradeRune.CalculateStrengthGain(new StrikeIronclad()), "unrelated card Strength");
	}

	private static void StarterUpgradeCapsTerminateExternalUpgradeToMaxLoops()
	{
		Equal(999, HextechStarterUpgradeHooks.UpgradeLevelCap, "starter multi-upgrade cap");
		Equal(
			999,
			HextechStarterUpgradeHooks.ResolveOwnedMaxUpgradeLevel(0),
			"owned basic cards with the matching rune use the +999 cap");
		Equal(
			1001,
			HextechStarterUpgradeHooks.ResolveOwnedMaxUpgradeLevel(1001),
			"owned legacy over-cap cards remain loadable but cannot grow further");
		Equal(
			1,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(0, isDeserializing: false),
			"new unowned cards keep the vanilla cap");
		Equal(
			1,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(998, isDeserializing: false),
			"ordinary unowned cards do not inherit the rune cap");
		Equal(
			1001,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(1000, isDeserializing: true),
			"legacy over-cap saves can replay the next upgrade level");

		int simulatedUpgradeLevel = 0;
		int upgradeCount = 0;
		while (simulatedUpgradeLevel < HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(
			simulatedUpgradeLevel,
			isDeserializing: false))
		{
			simulatedUpgradeLevel++;
			upgradeCount++;
			Expect(upgradeCount <= 1, "UpgradeAllCards-style loop must terminate at the vanilla cap");
		}

		Equal(1, simulatedUpgradeLevel, "UpgradeAllCards-style loop final level");
		Equal(1, upgradeCount, "UpgradeAllCards-style loop iteration count");

		SearingAttackCard searingAttack = CreateMutableTestModel<SearingAttackCard>();
		Equal(999, searingAttack.MaxUpgradeLevel, "Searing Attack cap");
	}

	private static void CreativeAiUpgradeRuneUpgradesGeneratedPowerCards()
	{
		CreativeAi card = CreateMutableTestModel<CreativeAi>();

		Expect(CreativeAiUpgradeRune.UpgradeGeneratedCard(card), "Creative AI should generate an upgraded Power card");
		Equal(1, card.CurrentUpgradeLevel, "Creative AI generated card upgrade level");
		Expect(!CreativeAiUpgradeRune.UpgradeGeneratedCard(card), "an already upgraded generated card should not be upgraded twice");

		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(BlankCheckRune).GetMethod(nameof(BlankCheckRune.AfterPlayerTurnStart))!),
			nameof(BlankCheckRune));
		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(MindOverMatterRune).GetMethod(nameof(MindOverMatterRune.BeforeHandDraw))!),
			nameof(MindOverMatterRune));
		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(SingularityAIRune).GetMethod(nameof(SingularityAIRune.BeforeHandDraw))!),
			nameof(SingularityAIRune));
		ExpectCombatGenerationFilters(
			typeof(CorruptedBranchRune).GetMethod("CreateRandomCombatCard", BindingFlags.Instance | BindingFlags.NonPublic)!,
			nameof(CorruptedBranchRune));
		ExpectCombatGenerationFilters(
			typeof(ColorDiscoveryRune).GetMethod("GetOtherCharacterCards", BindingFlags.NonPublic | BindingFlags.Static)!,
			nameof(ColorDiscoveryRune));
	}

	private static void SubroutineUpgradeCombatMoveGateResetsAcrossCombats()
	{
		SubroutineUpgradeRune rune = new();

		Expect(rune.TryConsumeCombatStartMove(), "first combat-start move should be consumed");
		Expect(!rune.TryConsumeCombatStartMove(), "same combat should reject a second move");

		rune.BeforeCombatStart().GetAwaiter().GetResult();
		Expect(rune.TryConsumeCombatStartMove(), "combat start should reset the move gate");

		rune.AfterCombatEnd(null!).GetAwaiter().GetResult();
		Expect(rune.TryConsumeCombatStartMove(), "combat end should clear the move gate");
	}

	private static void DualcastUpgradeReturnsBothCastCardsToHand()
	{
		Expect(
			DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Dualcast>()),
			"Dualcast Upgrade should return Dualcast to hand");
		Expect(
			DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Quadcast>()),
			"Dualcast Upgrade should return Quadcast to hand");
		Expect(
			!DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Zap>()),
			"Dualcast Upgrade should ignore unrelated cards");
		Expect(
			DualcastUpgradeRune.CanReturnFromResultPile(PileType.Discard),
			"normal result piles should be redirected to hand");
		Expect(
			!DualcastUpgradeRune.CanReturnFromResultPile(PileType.None),
			"temporary copies with no result pile should still disappear");
		DualcastUpgradeRune rune = new();
		Expect(!rune.GrantsCardOnPickup, "Dualcast Upgrade should not grant a card when obtained");
		Expect(!rune.HasUponPickupEffect, "Dualcast Upgrade should not advertise a pickup effect");
	}

	private static void PactsEndUpgradeDamageScalesWithExhaustPile()
	{
		Equal(0m, PactsEndUpgradeRune.CalculateBonusDamage(0, 6m), "empty exhaust pile bonus");
		Equal(30m, PactsEndUpgradeRune.CalculateBonusDamage(5, 6m), "five-card exhaust pile bonus");
		Equal(0m, PactsEndUpgradeRune.CalculateBonusDamage(-1, 6m), "negative exhaust count clamps");
	}

	private static void BrandUpgradeDamageScalesWithPermanentPlayCount()
	{
		Equal(3, BrandUpgradeRune.DamagePercentPerBrand, "Brand damage percent per play");
		Equal(1m, BrandUpgradeRune.CalculateDamageMultiplier(0, BrandUpgradeRune.DamagePercentPerBrand), "zero brand plays");
		Equal(1.03m, BrandUpgradeRune.CalculateDamageMultiplier(1, BrandUpgradeRune.DamagePercentPerBrand), "one brand play");
		Equal(1.30m, BrandUpgradeRune.CalculateDamageMultiplier(10, BrandUpgradeRune.DamagePercentPerBrand), "ten brand plays");
	}

	private static void NewCardUpgradeRunesUseExpectedTriggerRules()
	{
		Equal(0, ThornmailRune.CalculateThorns(19m), "Thornmail should floor partial Max HP steps");
		Equal(1, ThornmailRune.CalculateThorns(20m), "Thornmail should grant one Thorns per twenty Max HP");
		Equal(4, ThornmailRune.CalculateThorns(99m), "Thornmail should have no legacy bonus cap");
		var waveOwner = CreateOrdinalTestPlayer(1);
		var ownWave = CreateMutableTestModel<CorrosiveWave>();
		var teammateWave = CreateMutableTestModel<CorrosiveWave>();
		var ownStrike = CreateMutableTestModel<StrikeIronclad>();
		ownWave.Owner = ownStrike.Owner = waveOwner;
		teammateWave.Owner = CreateOrdinalTestPlayer(2);
		Expect(CorrosiveWaveUpgradeRune.GrantsExhaust(ownWave, waveOwner), "the owner's Corrosive Wave gains the Exhaust keyword");
		Expect(!CorrosiveWaveUpgradeRune.GrantsExhaust(teammateWave, waveOwner), "a teammate's Corrosive Wave is untouched");
		Expect(!CorrosiveWaveUpgradeRune.GrantsExhaust(ownStrike, waveOwner), "Corrosive Wave upgrade should not exhaust other cards");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Power, hasUpgradeRune: false), "vanilla Storm should still trigger for Power cards");
		Expect(!StormUpgradeRune.ShouldTrigger(CardType.Attack, hasUpgradeRune: false), "vanilla Storm should ignore Attacks");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Attack, hasUpgradeRune: true), "upgraded Storm should trigger for Attacks");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Skill, hasUpgradeRune: true), "upgraded Storm should trigger for Skills");
		Expect(ReanimateUpgradeRune.ShouldCountDeath(wasRemovalPrevented: false), "Reanimate should count Minion and Small Hand deaths like Melancholy");
		Expect(!ReanimateUpgradeRune.ShouldCountDeath(wasRemovalPrevented: true), "Reanimate should ignore a death that was prevented");
		Equal(7, BodySlamUpgradeRune.CalculateFisticuffsBlock(7, 0), "Body Slam should count total damage like Fisticuffs");
		Equal(10, BodySlamUpgradeRune.CalculateFisticuffsBlock(7, 3), "Body Slam should add overkill damage like Fisticuffs");
		Equal(7, WroughtInWarUpgradeRune.CalculateFisticuffsBlock(7, 0), "Wrought in War should count total damage like Fisticuffs");
		Equal(10, WroughtInWarUpgradeRune.CalculateFisticuffsBlock(7, 3), "Wrought in War should add overkill damage like Fisticuffs");
		Expect(DecisionsDecisionsUpgradeRune.CanSelectCard(isUnplayable: false), "Decisions should allow playable cards of any type");
		Expect(!DecisionsDecisionsUpgradeRune.CanSelectCard(isUnplayable: true), "Decisions should still reject Unplayable cards");
		Equal(3, DecisionsDecisionsUpgradeRune.AddRequestedPlayCount(1, 3), "Decisions should resolve all three plays inside one card-play wrapper");
		Equal(4, DecisionsDecisionsUpgradeRune.AddRequestedPlayCount(2, 3), "Decisions replay count should combine additively with another replay");
	}

	private static void HiddenGemUpgradeMovesNewReplayTargetToHand()
	{
		StrikeIronclad target = CreateMutableTestModel<StrikeIronclad>();
		Expect(
			HiddenGemUpgradeRune.IsEligibleReplayTarget(target),
			"Hidden Gem should accept a playable card without Replay");

		target.BaseReplayCount = 1;
		Expect(
			!HiddenGemUpgradeRune.IsEligibleReplayTarget(target),
			"Hidden Gem should retain the vanilla restriction against cards that already have Replay");
		Equal(PileType.Hand, HiddenGemUpgradeRune.ReplayTargetPile, "Hidden Gem upgraded replay target pile");
	}

	private static void DragonSoulAndMikaelsUseUpdatedUpgradeValues()
	{
		MikaelsBlessingCard mikaels = CreateMutableTestModel<MikaelsBlessingCard>();
		Equal(0, mikaels.EnergyCost.GetWithModifiers(CostModifiers.Local), "base Mikael's Blessing costs zero");
		Equal(10m, mikaels.DynamicVars["HealPercent"].BaseValue, "base Mikael's Blessing heals ten percent");
		Expect(mikaels.CanonicalKeywords.Contains(CardKeyword.Retain), "Mikael's Blessing retains");
		CardCmd.Upgrade(mikaels);
		Equal(0, mikaels.EnergyCost.GetWithModifiers(CostModifiers.Local), "upgraded Mikael's Blessing still costs zero");
		Equal(15m, mikaels.DynamicVars["HealPercent"].BaseValue, "upgrade increases healing to fifteen percent");
		InfernalDragonSoulCard infernal = CreateMutableTestModel<InfernalDragonSoulCard>();
		Equal(0, infernal.EnergyCost.GetWithModifiers(CostModifiers.Local), "Infernal Dragon Soul costs zero");
		Equal(8m, infernal.DynamicVars["BurnPower"].BaseValue, "Infernal Dragon Soul applies eight Burn");
		CardCmd.Upgrade(infernal);
		Equal(8m, infernal.DynamicVars["BurnPower"].BaseValue, "upgraded Infernal Dragon Soul retains eight Burn");
		Expect(infernal.Keywords.Contains(CardKeyword.Innate), "upgraded Infernal Dragon Soul is Innate");
		Equal(2m, new AncientWineRune().DynamicVars["HealPercent"].BaseValue, "Ancient Wine heals two percent after a Skill");
	}
}
