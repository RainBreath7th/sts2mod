using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HextechRunes;
using HextechRunes.Loader;
using MegaCrit.Sts2.Core.Entities.Cards;
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

	private static void WaxRelicRewardSaveMarkerIsOwnedAndLegacyCompatible()
	{
		ModelId wax = ModelDb.GetId<TezcatarasMercyRune>();
		Expect(HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, CustomDescriptionEncounterSourceId = wax }), "owned marker identifies a wax reward");
		Expect(HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, WasGoldStolenBack = true }), "0.9.5 saves with only the borrowed flag still restore as wax");
		Expect(!HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic, WasGoldStolenBack = true, CustomDescriptionEncounterSourceId = ModelDb.GetId<ColorDiscoveryRune>() }), "a reward that names another source is never claimed even if it borrows the same flag");
		Expect(!HextechWaxRelicReward.IsWaxSave(new SerializableReward { RewardType = RewardType.Relic }), "plain relic rewards are untouched");
	}
}
