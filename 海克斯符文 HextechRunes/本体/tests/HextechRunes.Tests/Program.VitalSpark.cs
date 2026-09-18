using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static readonly List<(Creature Target, decimal Amount)> VitalSparkApplications = [];

	private static void PlayerVitalSparkScopesCardsAndCleansUp()
	{
		Harmony harmony = new("HextechRunes.Tests.VitalSpark");
		Type[] added = new[] { typeof(Tainted), typeof(TaintedPower) }.Where(type => !ModelDb.Contains(type)).ToArray();
		try
		{
			foreach (Type type in added) ModelDb.Inject(type);
			foreach (Type patch in typeof(HextechVitalSparkCompatibilityHooks).GetNestedTypes(System.Reflection.BindingFlags.NonPublic))
			{
				if (patch.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
					harmony.CreateClassProcessor(patch).Patch();
			}
			// 隔离命令所需的场景/历史；实际运行 Power 的完整生命周期与原版清除侵蚀命令。
			var afflict = typeof(CardCmd).GetMethods().Single(m => m.Name == "Afflict" && !m.IsGenericMethodDefinition);
			harmony.Patch(afflict, prefix: new HarmonyMethod(typeof(Program), nameof(ApplyVitalSparkTestAffliction)));
			var apply = typeof(PowerCmd).GetMethods().Single(m => m.Name == "Apply" && !m.IsGenericMethodDefinition);
			harmony.Patch(apply, prefix: new HarmonyMethod(typeof(Program), nameof(CaptureVitalSparkPollution)));
			var (_, first, second) = CreatePrismaticEnemyFixture();
			AccessTools.Field(typeof(Creature), "<Player>k__BackingField").SetValue(first.Creature, first);
			AccessTools.Field(typeof(Creature), "<Player>k__BackingField").SetValue(second.Creature, second);
			AccessTools.Field(typeof(Creature), "_powers").SetValue(first.Creature, new List<PowerModel>());
			AccessTools.Field(typeof(Creature), "_powers").SetValue(second.Creature, new List<PowerModel>());
			CardModel AddCard<T>(Player player, CardPile pile) where T : CardModel, new()
			{
				T card = CreateMutableTestModel<T>();
				card.Owner = player;
				((List<CardModel>)AccessTools.Field(typeof(CardPile), "_cards").GetValue(pile)!).Add(card);
				return card;
			}
			CardModel skill = AddCard<DefendIronclad>(first, first.PlayerCombatState!.Hand);
			CardModel exhausted = AddCard<DefendIronclad>(first, first.PlayerCombatState.ExhaustPile);
			CardModel attack = AddCard<StrikeIronclad>(first, first.PlayerCombatState.DrawPile);
			CardModel teammate = AddCard<DefendIronclad>(second, second.PlayerCombatState!.Hand);
			HextechVitalSparkPower power = CreateMutableTestModel<HextechVitalSparkPower>();
			AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, first.Creature);
			AccessTools.Field(typeof(PowerModel), "_amount").SetValue(power, 2);
			Equal(PowerType.Debuff, power.Type, "player vital spark is a cleansable debuff");
			power.AfterApplied(null, null).GetAwaiter().GetResult();
			power.BeforeCombatStart().GetAwaiter().GetResult();
			Equal(2, skill.Affliction!.Amount, "initial application and combat start do not double stacks");
			Equal(2, exhausted.Affliction!.Amount, "includes exhaust pile");
			Expect(attack.Affliction == null && teammate.Affliction == null, "only owner's skills are afflicted");
			AccessTools.Field(typeof(PowerModel), "_amount").SetValue(power, 5);
			power.AfterPowerAmountChanged(null!, power, 3, null, null).GetAwaiter().GetResult();
			Equal(5, skill.Affliction.Amount, "stack increase updates existing afflictions");
			AccessTools.Field(typeof(PowerModel), "_amount").SetValue(power, 1);
			power.AfterPowerAmountChanged(null!, power, -4, null, null).GetAwaiter().GetResult();
			Equal(1, exhausted.Affliction.Amount, "stack reduction updates all piles");
			CardModel generated = AddCard<DefendIronclad>(first, first.PlayerCombatState.DiscardPile);
			power.AfterCardEnteredCombat(generated).GetAwaiter().GetResult();
			Equal(1, generated.Affliction!.Amount, "new skills receive current stacks");
			power.AfterCardEnteredCombat(teammate).GetAwaiter().GetResult();
			Expect(teammate.Affliction == null, "teammate's new skills stay untouched");
			var foreignTainted = ModelDb.Affliction<Tainted>().ToMutable();
			foreignTainted.Card = teammate;
			foreignTainted.Amount = 7;
			AccessTools.Property(typeof(CardModel), nameof(CardModel.Affliction)).SetValue(teammate, foreignTainted);
			power.AfterCardPlayed(null!, CreateCardPlay(skill)).GetAwaiter().GetResult();
			power.AfterCardPlayed(null!, CreateCardPlay(teammate)).GetAwaiter().GetResult();
			Equal(1, VitalSparkApplications.Count, "only owner's play adds pollution");
			Equal((first.Creature, 1m), VitalSparkApplications[0], "pollution targets owner using current stacks");
			Expect(ScapegoatRune.CreateEnemyTransfer(power) == null, "player-only debuff cannot transfer to an enemy");
			power.AfterRemoved(first.Creature).GetAwaiter().GetResult();
			Expect(skill.Affliction == null && exhausted.Affliction == null && generated.Affliction == null, "cleansing removes owner's skill afflictions");
			Equal(7, teammate.Affliction!.Amount, "cleansing does not remove teammate's pollution");

			CombatState combat = (CombatState)first.Creature.CombatState!;
			Creature enemy = CreatePrismaticTestCreature(CombatSide.Enemy, combat);
			foreach (Creature creature in new[] { first.Creature, second.Creature, enemy })
			{
				AccessTools.Field(typeof(Creature), "_powers").SetValue(creature, new List<PowerModel>());
				combat.AddCreature(creature);
			}
			var playerPowers = (List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(first.Creature)!;
			var enemyPowers = (List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(enemy)!;
			playerPowers.Add(power);
			VitalSparkPower native = CreateMutableTestModel<VitalSparkPower>();
			AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(native, enemy);
			AccessTools.Field(typeof(PowerModel), "_amount").SetValue(native, 2);
			enemyPowers.Add(native);
			power.AfterApplied(null, null).GetAwaiter().GetResult();
			native.BeforeCombatStart().GetAwaiter().GetResult();
			Equal(3, skill.Affliction!.Amount, "native combat-start write preserves native 2 plus player 1");
			VitalSparkApplications.Clear();
			power.AfterCardPlayed(null!, CreateCardPlay(skill)).GetAwaiter().GetResult();
			native.AfterCardPlayed(null!, CreateCardPlay(skill)).GetAwaiter().GetResult();
			Equal(3m, VitalSparkApplications.Sum(row => row.Amount), "both real play hooks add each source exactly once");
			AccessTools.Field(typeof(PowerModel), "_amount").SetValue(native, 4);
			native.AfterPowerAmountChanged(null!, native, 2, null, null).GetAwaiter().GetResult();
			Equal(5, skill.Affliction.Amount, "native amount change is reconciled after its overwrite");
			playerPowers.Clear();
			power.AfterRemoved(first.Creature).GetAwaiter().GetResult();
			Equal(4, skill.Affliction.Amount, "cleansing player debuff retains native contribution");
			playerPowers.Add(power);
			power.AfterApplied(null, null).GetAwaiter().GetResult();
			enemyPowers.Clear();
			native.AfterRemoved(enemy).GetAwaiter().GetResult();
			Equal(1, skill.Affliction!.Amount, "native removal retains player contribution after native clears cards");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
			VitalSparkApplications.Clear();
			foreach (Type type in added) ModelDb.Remove(type);
		}
	}

	private static bool ApplyVitalSparkTestAffliction(AfflictionModel affliction, CardModel card, decimal amount, ref Task<AfflictionModel?> __result)
	{
		affliction.Card = card;
		affliction.Amount = (int)amount;
		AccessTools.Property(typeof(CardModel), nameof(CardModel.Affliction)).SetValue(card, affliction);
		__result = Task.FromResult<AfflictionModel?>(affliction);
		return false;
	}

	private static bool CaptureVitalSparkPollution(PowerModel power, Creature target, decimal amount, ref Task __result)
	{
		Expect(power is TaintedPower, "skill play applies native pollution");
		VitalSparkApplications.Add((target, amount));
		__result = Task.CompletedTask;
		return false;
	}
}
