using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
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
		T Power<T>(int amount) where T : PowerModel
		{
			var power = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
			typeof(AbstractModel).GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, true);
			typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, amount);
			return power;
		}
		var strength = Power<StrengthPower>(-5);
		var dexterity = Power<DexterityPower>(-3);
		var weak = Power<WeakPower>(2);
		var buff = Power<StrengthPower>(4);
		List<PowerModel> powers = [strength, buff, weak, dexterity];
		var snapshot = ScapegoatRune.SnapshotDebuffs(powers);
		Equal(3, snapshot.Length, "negative attributes and ordinary debuffs all transfer");
		Expect(snapshot.SequenceEqual(new PowerModel[] { strength, weak, dexterity }), "preserve stable native power order and exclude buffs");
		powers.Clear();
		Equal(3, snapshot.Length, "removal cannot mutate the transfer snapshot");
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
		List<CardModel> exhausted = [addedEthereal, ordinary, foreign, addedEthereal];
		var snapshot = NetherSoulRune.SnapshotEtherealCards(owner, exhausted);
		Equal(1, snapshot.Length, "added keywords count; ordinary and foreign cards do not; each instance only once");
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
		Expect(transfer.Any(m => m.Name == "ClonePreservingMutability"), "transfer does not reuse the player's power instance");
		Expect(transfer.Any(m => m.DeclaringType == typeof(MegaCrit.Sts2.Core.Commands.PowerCmd) && m.Name == "Apply"), "transfer keeps native application and artifact handling");
		var replay = Calls(typeof(NetherSoulRune), nameof(NetherSoulRune.AfterSideTurnEndLate));
		Expect(replay.Any(m => m.DeclaringType == typeof(HextechAutoPlayHelper)), "exhausted cards use native autoplay");
		Expect(!replay.Any(m => m.Name == "CanPlay"), "zero energy must not block autoplay");
		Expect(replay.Any(m => m.Name == "Contains" && m.IsGenericMethod && m.GetGenericArguments().Contains(typeof(Creature))), "only the owner's turn including extra-turn participation");
	}
}
