using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void CrossOrbKeepsSilkenTressOnFinalRewardsInEitherRelicOrder()
	{
		Type[] added = new[] { typeof(Anger), typeof(Uppercut), typeof(Impervious), typeof(Glam) }
			.Where(type => !ModelDb.Contains(type)).ToArray();
		Harmony harmony = new("HextechRunes.Tests.CrossOrb");
		try
		{
			foreach (Type type in added) ModelDb.Inject(type);
			// 隔离完整爬塔对象和随机选牌，只替代监听者枚举与候选选择。
			// 保留原版两阶段 Hook、华美发束克隆/附魔及一次性消耗流程。
			harmony.Patch(AccessTools.Method(typeof(RunState), nameof(RunState.IterateHookListeners)),
				prefix: new HarmonyMethod(typeof(Program), nameof(CrossOrbTestListeners)));
			harmony.Patch(AccessTools.Method(typeof(CrossOrbRune), "TryCreateNonCommonCard"),
				prefix: new HarmonyMethod(typeof(Program), nameof(CrossOrbTestReplacement)));
			foreach (bool tressFirst in new[] { true, false })
			{
				var (_, owner, other) = CreatePrismaticEnemyFixture();
				RunState run = (RunState)owner.RunState;
				AccessTools.Field(typeof(RunState), "_allCards").SetValue(run, new List<CardModel>());
				CrossOrbRune orb = CreateMutableTestModel<CrossOrbRune>(); orb.Owner = owner;
				orb.DynamicVars["CommonReductionPercent"].BaseValue = 100m;
				SilkenTress tress = CreateMutableTestModel<SilkenTress>(); tress.Owner = owner;
				List<RelicModel> relics = tressFirst ? [tress, orb] : [orb, tress];
				AccessTools.Field(typeof(Player), "_relics").SetValue(owner, relics);
				AccessTools.Field(typeof(Player), "_relics").SetValue(other, new List<RelicModel>());
				CardCreationOptions options = new([], CardCreationSource.Other, CardRarityOddsType.Uniform);
				options = options.WithFlags(CardCreationFlags.IsCardReward);
				List<CardCreationResult> rewards = [new(run.CreateCard<Anger>(owner)), new(run.CreateCard<Impervious>(owner))];
				Expect(Hook.TryModifyCardRewardOptions(run, owner, rewards, options, out var modifiers), "reward is modified");
				Expect(rewards[0].Card is Uppercut, "common reward is still replaced by Cross Orb");
				Expect(rewards[1].Card is Impervious, "rare reward keeps its identity");
				Expect(rewards.All(r => r.Card.Enchantment is Glam { Amount: 1 }), "all final choices retain native Glam regardless of relic order");
				Equal(1, modifiers.Count(m => ReferenceEquals(m, tress)), "Tress triggers exactly once");
				Expect(rewards[0].ModifyingRelics.Contains(orb) && rewards[0].ModifyingRelics.Contains(tress), "both reward modifiers remain tracked");
				foreach (AbstractModel modifier in modifiers) modifier.AfterModifyingCardRewardOptions().GetAwaiter().GetResult();
				Expect(tress.IsUsedUp, "first reward consumes Tress normally");
				List<CardCreationResult> next = [new(run.CreateCard<Anger>(owner))];
				Hook.TryModifyCardRewardOptions(run, owner, next, options, out _);
				Expect(next[0].Card is Uppercut && next[0].Card.Enchantment == null, "later rewards still filter rarity without repeating Glam");
			}
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
			foreach (Type type in added) ModelDb.Remove(type);
		}
	}

	private static bool CrossOrbTestListeners(RunState __instance, ref IEnumerable<AbstractModel> __result)
	{
		__result = __instance.Players.SelectMany(player => player.Relics);
		return false;
	}

	private static bool CrossOrbTestReplacement(Player player, out CardCreationResult? result, ref bool __result)
	{
		result = new CardCreationResult(((RunState)player.RunState).CreateCard<Uppercut>(player));
		__result = true;
		return false;
	}
}
