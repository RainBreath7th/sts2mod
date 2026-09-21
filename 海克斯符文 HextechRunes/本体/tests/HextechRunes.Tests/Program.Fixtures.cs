using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
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

	private sealed class GeneratedTestRelic : RelicModel, IHextechGeneratedRune
	{
		public override RelicRarity Rarity => RelicRarity.Event;
		public string Data { get; set; } = "";
		public string ExportSelectionData() => Data;
		public bool TryImportSelectionData(string data) { Data = data; return data.StartsWith("recipe:", StringComparison.Ordinal); }
	}
}
