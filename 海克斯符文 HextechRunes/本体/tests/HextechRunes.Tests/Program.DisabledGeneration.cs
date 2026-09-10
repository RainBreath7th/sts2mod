using System.Reflection;
using HextechRunes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void NaturalRelicPoolPreservesVanillaRngAndForeignContent()
	{
		// 保留原版 Starter，确保过滤依据是内容归属而非稀有度。
		RelicModel[] vanilla = [new MegaCrit.Sts2.Core.Models.Relics.Anchor(), new Vajra(), new PenNib(), new CrackedCore()];
		RelicModel[] registered = [vanilla[0], new BlankCheckRune(), vanilla[1], new DoubleVisionRune(), vanilla[2], new VoltaicUpgradeRune(), vanilla[3]];
		RelicModel[] filtered = HextechNaturalRelicPoolHooks.FilterNaturalRelics(registered).ToArray();
		SequenceEqual(vanilla, filtered, "natural pool preserves other content and its order");
		Equal(7, registered.Length, "filter must not mutate model registration");

		foreach (uint seed in new uint[] {0, 1, 42, 123456789})
		{
			Rng baseline = new(seed);
			Rng fixedRng = new(seed);
			Rng oldRng = new(seed);
			new RelicGrabBag().Populate(vanilla, baseline);
			new RelicGrabBag().Populate(filtered, fixedRng);
			new RelicGrabBag().Populate(registered, oldRng);
			Equal(GetGenerationRngCounter(baseline), GetGenerationRngCounter(fixedRng), "pool RNG consumption must match vanilla");
			Expect(GetGenerationRngCounter(oldRng) > GetGenerationRngCounter(baseline), "unfiltered Starter entries reproduce the old RNG drift");
			for (int i = 0; i < 32; i++)
			{
				Equal(baseline.NextInt(1000000), fixedRng.NextInt(1000000), "later encounter/Boss random stream must remain identical");
			}
		}
	}

	private static ulong GetGenerationRngCounter(Rng rng)
	{
#if STS2_110_OR_NEWER
		return (ulong)rng.ToSerializable().counter;
#else
		return (ulong)rng.Counter;
#endif
	}

	private static void RunActivationUsesSnapshotBeforeFreezeAndPreservesFrozenValue()
	{
		// ModifierModel 构造器接触 Godot 本地化；这里只检查托管的本局配置状态。
		HextechMayhemModifier modifier = (HextechMayhemModifier)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(HextechMayhemModifier));
		HextechMayhemRunContext context = new();
		typeof(HextechMayhemModifier).GetField("_runContext", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(modifier, context);
		context.RunConfigurationSnapshot = HextechRuneConfiguration.GetDefaultSnapshot() with {ModEnabled = false};
		Expect(!modifier.IsModActiveForRun, "disabled snapshot applies before the first act roll");
		context.ModActiveForRun = false;
		context.RunConfigurationSnapshot = context.RunConfigurationSnapshot with {ModEnabled = true};
		Expect(!modifier.IsModActiveForRun, "reenabling only affects the next run");
		context.ModActiveForRun = true;
		context.RunConfigurationSnapshot = context.RunConfigurationSnapshot with {ModEnabled = false};
		Expect(modifier.IsModActiveForRun, "disabling does not change an already active run");
	}

	private static void DisabledStageBranchesBeforeEnemyGeneration()
	{
		MethodInfo method = typeof(HextechRuneSelectionCoordinator).GetMethod(nameof(HextechRuneSelectionCoordinator.HandleStageSelection))!;
		MethodInfo moveNext = GetAsyncStateMachineMoveNext(method);
		MethodInfo[] calls = HarmonyLib.PatchProcessor.GetOriginalInstructions(moveNext)
			.Select(static instruction => instruction.operand).OfType<MethodInfo>().ToArray();
		int freeze = Array.FindIndex(calls, static call => call.Name == "FreezeModActiveForRunAndCheckDisabled");
		int generate = Array.FindIndex(calls, static call => call.Name == "ResolveNewMonsterHexesForAct");
		Expect(freeze >= 0 && generate > freeze, "host-synchronized disable check must precede additional enemy rolls");
	}
}
