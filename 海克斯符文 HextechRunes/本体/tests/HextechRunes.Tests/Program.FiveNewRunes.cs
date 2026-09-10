using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
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
}
