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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void FourPrismaticEnemiesKeepIdentityAndStrengthScope()
	{
		MonsterHexKind[] kinds = [MonsterHexKind.ReforgedHelmet, MonsterHexKind.EndlessRotation,
			MonsterHexKind.SomethingForNothing, MonsterHexKind.CorruptedBranch];
		Type[] icons = [typeof(ReforgedHelmetRune), typeof(EndlessRotationRune), typeof(SomethingForNothingRune), typeof(CorruptedBranchRune)];
		for (int i = 0; i < kinds.Length; i++)
		{
			Equal(140 + i, (int)kinds[i], "append-only identity");
			var row = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == kinds[i]);
			Equal(HextechRarityTier.Prismatic, row.Rarity, "prismatic enemy");
			Equal(icons[i], row.IconRelicType, "reuse matching player icon");
			Expect(!row.Disabled && HextechEnemyHexEffects.RegisteredKinds.Contains(kinds[i]), "enabled and implemented");
		}
		var (context, first, second) = CreatePrismaticEnemyFixture();
		Creature enemy = CreatePrismaticTestCreature(CombatSide.Enemy, (CombatState)first.Creature.CombatState!);
		ReforgedHelmetEnemyHex effect = new();
		Equal(3m, effect.ModifyPowerAmountReceived(context, new StrengthPower(), enemy, 3m, null), "positive enemy strength is no longer doubled");
		Equal(0m, effect.ModifyPowerAmountReceived(context, new StrengthPower(), enemy, -3m, first.Creature), "enemy strength reduction is blocked");
		Equal(0m, effect.ModifyPowerAmountReceived(context, new StrengthPower(), enemy, -3m, enemy), "self-applied strength loss including temporary expiry is blocked");
		Equal(-3m, effect.ModifyPowerAmountReceived(context, new DexterityPower(), enemy, -3m, null), "other stat reductions remain allowed");
		Equal(-3m, effect.ModifyPowerAmountReceived(context, new StrengthPower(), second.Creature, -3m, enemy), "player strength reduction remains allowed");
		Equal(3m, effect.ModifyPowerAmountReceived(context, new DexterityPower(), enemy, 3m, null), "other powers unchanged");
		Equal(3m, effect.ModifyPowerAmountReceived(context, new StrengthPower(), second.Creature, 3m, null), "players unchanged");
	}

	private static void EnemyRotationStacksOnlyCurrentHandUntilTurnEnd()
	{
		var (context, first, second) = CreatePrismaticEnemyFixture();
		CardModel card = CreateMutableTestModel<StrikeIronclad>();
		CardModel other = CreateMutableTestModel<StrikeIronclad>();
		card.Owner = first;
		other.Owner = second;
		var hand = (List<CardModel>)AccessTools.Field(typeof(CardPile), "_cards").GetValue(first.PlayerCombatState!.Hand)!;
		hand.Add(card);
		((List<CardModel>)AccessTools.Field(typeof(CardPile), "_cards").GetValue(second.PlayerCombatState!.Hand)!).Add(other);
		EndlessRotationEnemyHex effect = new();
		effect.AfterShuffle(context, null!, first).GetAwaiter().GetResult();
		effect.AfterShuffle(context, null!, first).GetAwaiter().GetResult();
		Equal(3, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "two shuffles add two");
		Equal(1, other.EnergyCost.GetWithModifiers(CostModifiers.Local), "teammate hand unaffected");
		CardModel later = CreateMutableTestModel<StrikeIronclad>();
		later.Owner = first;
		hand.Add(later);
		Equal(1, later.EnergyCost.GetWithModifiers(CostModifiers.Local), "later draw not taxed retroactively");
		card.EnergyCost.AfterCardPlayedCleanup();
		Equal(3, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "returning card keeps whole-turn tax");
		card.EnergyCost.EndOfTurnCleanup();
		Equal(1, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "turn end restores cost");
	}

	private static void EnemyZeroCostExhaustUsesPlayCostRatherThanPayment()
	{
		var (context, first, _) = CreatePrismaticEnemyFixture();
		SomethingForNothingEnemyHex effect = new();
		CardModel attack = CreateMutableTestModel<StrikeIronclad>();
		attack.Owner = first;
		var free = new ResourceInfo { EnergyValue = 0, EnergySpent = 0, StarValue = 0, StarsSpent = 0 };
		var auto = new ResourceInfo { EnergyValue = 2, EnergySpent = 0, StarValue = 0, StarsSpent = 0 };
		Equal(PileType.Exhaust, effect.ModifyCardPlayResultPileTypeAndPosition(context, attack, false, free, PileType.Discard, CardPilePosition.Bottom)!.Value.Item1, "discounted zero-cost card exhausts");
		Expect(effect.ModifyCardPlayResultPileTypeAndPosition(context, attack, true, auto, PileType.Discard, CardPilePosition.Bottom) == null, "free autoplay of costly card does not qualify");
		CardModel power = CreateMutableTestModel<Corruption>();
		power.Owner = first;
		Equal(PileType.Exhaust, effect.ModifyCardPlayResultPileTypeAndPosition(context, power, true, free, PileType.None, CardPilePosition.Bottom)!.Value.Item1, "zero-cost power uses native exhaust instead of removal");
	}

	private static readonly List<(CardModel Card, PileType Pile)> EnemyBranchGenerated = [];

	private static void EnemyCorruptedBranchKeepsOwnerAndRestoresRandomSequence()
	{
		Type[] pool = [typeof(Burn), typeof(Dazed), typeof(Slimed), typeof(Wound), typeof(MegaCrit.Sts2.Core.Models.Cards.Void)];
		Type[] added = pool.Where(type => !ModelDb.Contains(type)).ToArray();
		Harmony harmony = new("HextechRunes.Tests.EnemyCorruptedBranch");
		try
		{
			foreach (Type type in added) ModelDb.Inject(type);
			// 只隔离牌堆动画与存档 UI；保留真实状态牌创建、随机抽选与战斗序号。
			harmony.Patch(AccessTools.Method(typeof(HextechCardGeneration), "AddGeneratedCardToCombat"),
				prefix: new HarmonyMethod(typeof(Program), nameof(CaptureEnemyBranchGenerated)));
			var (context, first, second) = CreatePrismaticEnemyFixture();
			CorruptedBranchEnemyHex effect = new();
			CardModel source = CreateMutableTestModel<StrikeIronclad>();
			source.Owner = first;
			effect.AfterCardExhausted(context, null!, source, true).GetAwaiter().GetResult();
			string saved = context.Tracking.Serialize();
			effect.AfterCardExhausted(context, null!, source, false).GetAwaiter().GetResult();
			Type expectedNext = EnemyBranchGenerated[^1].Card.GetType();
			context.Tracking.Restore(saved);
			effect.AfterCardExhausted(context, null!, source, false).GetAwaiter().GetResult();
			Equal(expectedNext, EnemyBranchGenerated[^1].Card.GetType(), "restored ordinal reproduces next status");
			CardModel teammateSource = CreateMutableTestModel<StrikeIronclad>();
			teammateSource.Owner = second;
			effect.AfterCardExhausted(context, null!, teammateSource, false).GetAwaiter().GetResult();
			Equal(4, EnemyBranchGenerated.Count, "one status per actual exhaust, including ethereal");
			Expect(EnemyBranchGenerated.All(row => row.Pile == PileType.Draw && pool.Contains(row.Card.GetType())), "only fixed status pool into draw pile");
			Expect(EnemyBranchGenerated.Take(3).All(row => row.Card.Owner == first) && EnemyBranchGenerated[^1].Card.Owner == second, "each status belongs to the exhausting player");
			Equal(2, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(context.Tracking, first, nameof(CorruptedBranchEnemyHex)), "first player counter");
			Equal(1, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(context.Tracking, second, nameof(CorruptedBranchEnemyHex)), "independent teammate counter");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
			EnemyBranchGenerated.Clear();
			foreach (Type type in added) ModelDb.Remove(type);
		}
	}

	private static bool CaptureEnemyBranchGenerated(CardModel card, PileType pileType, bool addedByPlayer, CardPilePosition position, ref Task<CardPileAddResult?> __result)
	{
		Expect(!addedByPlayer && position == CardPilePosition.Random, "enemy generated card uses random insertion");
		EnemyBranchGenerated.Add((card, pileType));
		__result = Task.FromResult<CardPileAddResult?>(null);
		return false;
	}

	private static (HextechEnemyHexContext Context, Player First, Player Second) CreatePrismaticEnemyFixture()
	{
		RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
		Player first = CreateOrdinalTestPlayer(1), second = CreateOrdinalTestPlayer(2);
		AccessTools.Field(typeof(RunState), "_players").SetValue(run, new List<Player> { first, second });
		FieldInfo history = AccessTools.Field(typeof(RunState), "_mapPointHistory");
		history.SetValue(run, Activator.CreateInstance(history.FieldType));
		AccessTools.Property(typeof(RunState), "Rng").SetValue(run, new RunRngSet("FOUR-ENEMY-HEXES"));
		CombatState combat = new(runState: run);
		foreach (Player player in new[] { first, second })
		{
			AccessTools.Field(typeof(Player), "_runState").SetValue(player, run);
			AccessTools.Field(typeof(Player), "<Creature>k__BackingField").SetValue(player, CreatePrismaticTestCreature(CombatSide.Player, combat));
			PlayerCombatState state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
			AccessTools.Field(typeof(PlayerCombatState), "<Hand>k__BackingField").SetValue(state, new CardPile(PileType.Hand));
			AccessTools.Field(typeof(PlayerCombatState), "<DrawPile>k__BackingField").SetValue(state, new CardPile(PileType.Draw));
			AccessTools.Field(typeof(PlayerCombatState), "<DiscardPile>k__BackingField").SetValue(state, new CardPile(PileType.Discard));
			AccessTools.Field(typeof(PlayerCombatState), "<ExhaustPile>k__BackingField").SetValue(state, new CardPile(PileType.Exhaust));
			AccessTools.Field(typeof(PlayerCombatState), "<PlayPile>k__BackingField").SetValue(state, new CardPile(PileType.Play));
			AccessTools.Field(typeof(Player), "<Deck>k__BackingField").SetValue(player, new CardPile(PileType.Deck));
			AccessTools.Property(typeof(Player), "PlayerCombatState").SetValue(player, state);
		}
		HextechMayhemModifier modifier = CreateMutableTestModel<HextechMayhemModifier>();
		AccessTools.Field(typeof(ModifierModel), "_runState").SetValue(modifier, run);
		return (new HextechEnemyHexContext(modifier), first, second);
	}

	private static Creature CreatePrismaticTestCreature(CombatSide side, CombatState combat)
	{
		Creature creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		AccessTools.Field(typeof(Creature), "<Side>k__BackingField").SetValue(creature, side);
		AccessTools.Field(typeof(Creature), "_currentHp").SetValue(creature, 50);
		AccessTools.Property(typeof(Creature), "CombatState").SetValue(creature, combat);
		return creature;
	}
}
