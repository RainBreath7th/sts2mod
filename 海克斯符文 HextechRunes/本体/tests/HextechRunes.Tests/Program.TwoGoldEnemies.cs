using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void EnemyMoreTheMerrierUsesPooledRelicsForAllThreeMultipliers()
	{
		var (context, first, second) = CreatePrismaticEnemyFixture();
		List<RelicModel> firstRelics = Enumerable.Range(0, 11).Select(_ => (RelicModel)CreateMutableTestModel<MoreTheMerrierRune>()).ToList();
		List<RelicModel> secondRelics = Enumerable.Range(0, 10).Select(_ => (RelicModel)CreateMutableTestModel<MoreTheMerrierRune>()).ToList();
		AccessTools.Field(typeof(Player), "_relics").SetValue(first, firstRelics);
		AccessTools.Field(typeof(Player), "_relics").SetValue(second, secondRelics);
		MoreTheMerrierEnemyHex effect = new();
		Equal(1.10m, effect.ModifyDamageMultiplicative(context, null, 10m, ValueProp.Move, null, null), "21 relics across two players grant ten percent");
		Equal(1.10m, effect.ModifyBlockMultiplicative(context, first.Creature, 10m, ValueProp.Move, null, null), "same block coefficient");
		Equal(1.10m, effect.ModifyEnemyHealMultiplicative(context, first.Creature, 10m), "same healing coefficient");
		secondRelics.Add(CreateMutableTestModel<MoreTheMerrierRune>());
		Equal(1.11m, effect.ModifyEnemyHealMultiplicative(context, first.Creature, 10m), "pool before rounding; changes update immediately");
		firstRelics.Clear();
		secondRelics.Clear();
		Equal(1m, effect.ModifyEnemyHealMultiplicative(context, first.Creature, 10m), "no relics means no bonus");
		var row = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == effect.Kind);
		Equal(144, (int)row.Kind, "append-only ID");
		Expect(row.Rarity == HextechRarityTier.Gold && !row.Disabled && row.IconRelicType == typeof(MoreTheMerrierRune), "enabled gold with matching icon");
	}

	private static void EnemyEnlightenmentFloorsDiscountedCostsWithoutChangingBase()
	{
		var (context, first, _) = CreatePrismaticEnemyFixture();
		CardModel card = CreateMutableTestModel<StrikeIronclad>();
		card.Owner = first;
		EnlightenmentEnemyHex effect = new();
		card.EnergyCost.SetThisTurn(0);
		Equal(1m, effect.ModifyEnergyCostInCombatLate(context, card, card.EnergyCost.GetWithModifiers(CostModifiers.Local)), "turn-free cards cost one after local modifiers");
		Equal(1m, effect.ModifyEnergyCostInCombatLate(context, card, -1m), "negative modified costs also floor to one");
		Equal(2m, effect.ModifyEnergyCostInCombatLate(context, card, 2m), "positive costs above one remain intact");
		Equal(0, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "floor does not overwrite original temporary cost");
		CardModel x = CreateMutableTestModel<Whirlwind>();
		x.Owner = first;
		Equal(0m, effect.ModifyEnergyCostInCombatLate(context, x, 0m), "X is not converted to fixed cost");
		card.EnergyCost.EndOfTurnCleanup();
		Equal(1, card.EnergyCost.GetWithModifiers(CostModifiers.Local), "native cleanup still restores original card cost");
		var row = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == effect.Kind);
		Equal(145, (int)row.Kind, "append-only ID");
		Expect(row.Rarity == HextechRarityTier.Gold && !row.Disabled && row.IconRelicType == typeof(EnlightenmentRune), "enabled gold with matching icon");
		var zeroCostEnemy = new SomethingForNothingEnemyHex();
		var resources = new ResourceInfo { EnergyValue = 1, EnergySpent = 1, StarValue = 0, StarsSpent = 0 };
		Expect(zeroCostEnemy.ModifyCardPlayResultPileTypeAndPosition(context, card, false, resources, PileType.Discard, CardPilePosition.Bottom) == null, "raised play cost no longer triggers zero-cost exhaust");
	}
}
