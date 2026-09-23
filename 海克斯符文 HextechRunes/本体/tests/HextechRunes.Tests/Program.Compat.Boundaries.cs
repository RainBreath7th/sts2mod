using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HextechRunes;
using HextechRunes.Loader;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunes.Tests;

// 2026-09 兼容性审计收口:精神过载改为海克斯版正面效果、加载器拒绝比宿主新的变体、蜡制奖励标记归属明确。
internal static partial class Program
{
	private static void NeurosurgeUpgradeSwapsVanillaPowerForHextechBuffOnlyForOwner()
	{
		var (_, first, second) = CreatePrismaticEnemyFixture();
		NeurosurgeUpgradeRune rune = CreateMutableTestModel<NeurosurgeUpgradeRune>();
		rune.Owner = first;
		AccessTools.Field(typeof(Player), "_relics").SetValue(first, new List<RelicModel> { rune });
		AccessTools.Field(typeof(Player), "_relics").SetValue(second, new List<RelicModel>());

		Neurosurge own = CreateMutableTestModel<Neurosurge>();
		own.Owner = first;
		Neurosurge foreign = CreateMutableTestModel<Neurosurge>();
		foreign.Owner = second;
		Expect(NeurosurgeUpgradeRune.ShouldSwapToHextechPower(own), "owner's Neurosurge swaps to the hextech buff");
		Expect(!NeurosurgeUpgradeRune.ShouldSwapToHextechPower(foreign), "teammate's Neurosurge keeps the vanilla debuff");

		HextechNeurosurgePower power = CreateMutableTestModel<HextechNeurosurgePower>();
		Equal(PowerType.Buff, power.Type, "hextech Neurosurge is a buff, so Artifact and debuff-triggered effects ignore it");
		Equal(PowerStackType.Counter, power.StackType, "stacks like the vanilla power");

		// 与原版同一守卫:只有持有者参与本次回合开始才施加灾厄(额外回合只带单个玩家重入)。
		Creature owner = first.Creature, teammate = second.Creature;
		Expect(HextechNeurosurgePower.ShouldApplyDoom(owner, CombatSide.Player, [owner, teammate], 3), "owner participates: apply");
		Expect(!HextechNeurosurgePower.ShouldApplyDoom(owner, CombatSide.Player, [teammate], 3), "teammate's extra turn: owner's power stays silent");
		Expect(!HextechNeurosurgePower.ShouldApplyDoom(owner, CombatSide.Player, [], 3), "empty participants: silent");
		Expect(!HextechNeurosurgePower.ShouldApplyDoom(owner, CombatSide.Enemy, [owner], 3), "enemy side turn: silent");
		Expect(!HextechNeurosurgePower.ShouldApplyDoom(owner, CombatSide.Player, [owner], 0), "no stacks: silent");
		Expect(
			typeof(HextechNeurosurgePower).GetMethod(nameof(HextechPowerBase.AfterSideTurnStartForParticipants))!.DeclaringType == typeof(HextechNeurosurgePower),
			"the power overrides the participant-aware entry, not the participant-less one");

		// 不再有任何补丁碰原版 NeurosurgePower:规范模型上读 Type 必须走纯原版路径(图书馆等第三方会遍历规范 Power)。
		Expect(
			typeof(ModEntry).Assembly.GetTypes().All(type => type.GetCustomAttributes<HarmonyPatch>().All(patch => patch.info.declaringType != typeof(NeurosurgePower))),
			"no Harmony patch targets vanilla NeurosurgePower anymore");
	}

	private static void LoaderRefusesNewerVariantForKnownOlderHost()
	{
		string root = Path.Combine(Path.GetTempPath(), "hextech-loader-test-" + Guid.NewGuid().ToString("N"));
		try
		{
			string libRoot = Path.Combine(root, "lib");
			string variantDirectory = Path.Combine(libRoot, "0.110.0");
			Directory.CreateDirectory(variantDirectory);
			File.WriteAllText(Path.Combine(variantDirectory, "compat-target.txt"), "0.110.0");
			byte[] dll = "not a real assembly"u8.ToArray();
			string dllPath = Path.Combine(variantDirectory, "HextechRunes.dll");
			File.WriteAllBytes(dllPath, dll);
			string manifest = JsonSerializer.Serialize(new
			{
				variants = new[] { new { compatTarget = "0.110.0", directory = "lib/0.110.0", assembly = "HextechRunes.dll", sha256 = Convert.ToHexString(SHA256.HashData(dll)) } }
			});
			File.WriteAllText(Path.Combine(root, "hextech-runes-variants.manifest"), manifest);

			MethodInfo pick = AccessTools.Method(typeof(LoaderBootstrap), "PickVariant");
			Expect(pick != null, "loader exposes PickVariant(loaderDirectory, libRoot, host)");
			object? Pick(Version? host) => pick!.Invoke(null, [root, libRoot, host]);
			static string Target(object candidate) => (string)AccessTools.Property(candidate.GetType(), "CompatTarget").GetValue(candidate)!;

			Equal("0.110.0", Target(Pick(new Version(0, 110, 0))!), "exact host picks its own variant");
			Equal("0.110.0", Target(Pick(new Version(0, 111, 0))!), "newer host falls back to the newest variant not above it");
			Equal("0.110.0", Target(Pick(null)!), "unknown host keeps using the newest bundled variant");
			Expect(Pick(new Version(0, 107, 1)) == null, "known older host with no compatible variant refuses to load instead of picking a newer one");
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	// 伤害命令作用域:Prefix 在调用方上下文入栈,Postfix 必须在同步返回前把调用方恢复;
	// 而原方法内部(在 Postfix 之前捕获了上下文的 await 续体)仍然看得到自己的命令 ID。
	private static void DamageCommandScopeRestoresCallerContextAndKeepsTaskContext()
	{
		MethodInfo prefix = HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "DamageCommandPatch", "Prefix")
			?? throw new InvalidOperationException("damage command prefix missing");
		MethodInfo postfix = HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "DamageCommandPatch", "Postfix")
			?? throw new InvalidOperationException("damage command postfix missing");
		Equal(0L, HextechCombatHooks.CurrentActualDamageCommandId, "clean caller context before the command");

		object?[] prefixArgs = [null];
		prefix.Invoke(null, prefixArgs);
		long commandId = (long)prefixArgs[0]!;
		Equal(commandId, HextechCombatHooks.CurrentActualDamageCommandId, "prefix pushes the id for the original method's synchronous part");

		// 模拟原方法内部在 Postfix 之前捕获上下文的续体:await 之后仍应看到自己的 ID。
		TaskCompletionSource<IEnumerable<DamageResult>> original = new();
		static async Task<long> ProbeAfterAwait(Task pending) { await pending; return HextechCombatHooks.CurrentActualDamageCommandId; }
		Task<long> insideProbe = ProbeAfterAwait(original.Task);

		object?[] postfixArgs = [commandId, original.Task];
		postfix.Invoke(null, postfixArgs);
		Task<IEnumerable<DamageResult>> wrapped = (Task<IEnumerable<DamageResult>>)postfixArgs[1]!;
		Equal(0L, HextechCombatHooks.CurrentActualDamageCommandId, "postfix restores the caller context before the task is returned");
		Expect(!wrapped.IsCompleted, "wrapper waits for the original task");

		original.SetResult([]);
		wrapped.GetAwaiter().GetResult();
		Equal(commandId, insideProbe.GetAwaiter().GetResult(), "continuations captured inside the command still see their own id");
		Equal(0L, HextechCombatHooks.CurrentActualDamageCommandId, "caller context stays clean after completion");

		// 嵌套:内层命令结束后外层 ID 仍在。
		object?[] outer = [null];
		prefix.Invoke(null, outer);
		object?[] inner = [null];
		prefix.Invoke(null, inner);
		Equal((long)inner[0]!, HextechCombatHooks.CurrentActualDamageCommandId, "inner id on top");
		object?[] innerPost = [inner[0], Task.FromResult<IEnumerable<DamageResult>>([])];
		postfix.Invoke(null, innerPost);
		Equal((long)outer[0]!, HextechCombatHooks.CurrentActualDamageCommandId, "outer id restored after the inner command returns");
		object?[] outerPost = [outer[0], Task.FromResult<IEnumerable<DamageResult>>([])];
		postfix.Invoke(null, outerPost);
		Equal(0L, HextechCombatHooks.CurrentActualDamageCommandId, "clean after the outer command returns");
	}

	// 规范遗物被图鉴或第三方遍历时会读计数器 getter;RelicModel.Owner 在规范模型上 AssertMutable,所以必须先判 IsCanonical。
	private static void NearDeathFeastCountersAreSafeOnCanonicalRelic()
	{
		NearDeathFeastRune canonical = (NearDeathFeastRune)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(NearDeathFeastRune));
		Expect(canonical.IsCanonical, "uninitialized model is canonical");
		Expect(!canonical.ShowCounter, "canonical relic shows no counter and does not touch Owner");
		Equal(0, canonical.DisplayAmount, "canonical relic displays 0 and does not touch Owner");
	}

	private static void WaxRelicRewardSaveMarkerIsOwnedAndLegacyCompatible()
	{
		ModelId wax = ModelDb.GetId<TezcatarasMercyRune>();
		Expect(HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, CustomDescriptionEncounterSourceId = wax }), "owned marker identifies a wax reward");
		Expect(HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, WasGoldStolenBack = true }), "0.9.5 saves with only the borrowed flag still restore as wax");
		Expect(!HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, WasGoldStolenBack = true, CustomDescriptionEncounterSourceId = ModelDb.GetId<ColorDiscoveryRune>() }), "a reward that names another source is never claimed even if it borrows the same flag");
		Expect(!HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic }), "plain relic rewards are untouched");
	}
}
