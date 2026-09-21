using System.Reflection;
using Godot;
using HarmonyLib;
using FormVfxKind = HextechRunes.HextechFormVfxSafetyHooks.FormVfxKind;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void MadScientistOrbLayoutOnlyTweensFirstTen()
	{
		Equal(10, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 11, 11), "the eleventh Mad Scientist orb should skip layout tweening");
		Equal(10, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 40, 40), "Mad Scientist tween work should stay capped as slots grow");
		Equal(7, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 40, 7), "the first ten visible slots should keep their normal tween");
		Equal(11, HextechPlayerRuneHooks.ResolveTweenedOrbCount(false, 11, 11), "non-Mad Scientist large layouts should keep existing tween behavior");

		MethodInfo layout = typeof(HextechPlayerRuneHooks).GetMethod(
			"OrbTweenLayoutPrefixCore",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechPlayerRuneHooks), "OrbTweenLayoutPrefixCore");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(layout)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(Engine) && method.Name == nameof(Engine.GetProcessFrames)),
			"Mad Scientist orb layout should coalesce duplicate work within one process frame");
		Expect(
			calls.Any(static method => method.Name == "set_Position"),
			"overflow orbs should move directly to their unchanged layout target");
	}

	private static void SovereignBladeVfxSyncUsesVanillaForgeScale()
	{
		Expect(Math.Abs(0.9f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(0)) < 0.0001f, "zero-damage blade scale");
		Expect(Math.Abs(0.955f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(10)) < 0.0001f, "base blade scale");
		Expect(Math.Abs(2f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(200)) < 0.0001f, "fully scaled blade");
		Expect(Math.Abs(2f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(999)) < 0.0001f, "blade scale cap");
	}

	private static void SlowCookVfxUsesDedicatedPressureCookerTextures()
	{
		string[] slowCookPaths =
		[
			HextechAssets.SlowCookHeatGlowPath,
			HextechAssets.SlowCookAoeGradientPath,
			HextechAssets.SlowCookAoeGradientSubtlePath,
			HextechAssets.SlowCookAoeEdgePath,
			HextechAssets.SlowCookAoePolarPath,
			HextechAssets.SlowCookEdgeAccentPath,
			HextechAssets.SlowCookGroundRingPath,
			HextechAssets.SlowCookFlameNoisePath,
			HextechAssets.SlowCookInnerFirePath,
			HextechAssets.SlowCookInnerFireBPath,
			HextechAssets.SlowCookFlarePath
		];

		Expect(
			slowCookPaths.All(static path => path.StartsWith("res://HextechRunes/images/effects/slow_cook/", StringComparison.Ordinal)),
			"Slow Cook VFX should load only its dedicated Pressure Cooker textures");
		Expect(
			slowCookPaths.All(static path => path != HextechAssets.MikaelsBlessingAoeRunePath),
			"Slow Cook VFX must not reuse Mikael's Blessing texture");
		Equal(slowCookPaths.Length, slowCookPaths.Distinct(StringComparer.Ordinal).Count(), "Slow Cook VFX texture paths");
		Equal(800f, SlowCookAuraVisual.ResolveWidth(160f), "Slow Cook aura width for a normal player hitbox");
		Equal(800f, SlowCookAuraVisual.ResolveWidth(500f), "Slow Cook aura width should not be reduced by hitbox scaling");
		Expect(
			SlowCookAuraVisual.FlowShaderCode.Contains("anchored_gradient", StringComparison.Ordinal),
			"Slow Cook aura should retain a stationary coverage sample while its texture details move");
		Expect(
			SlowCookAuraVisual.FlowShaderCode.Contains("intensity = min(intensity, 0.90)", StringComparison.Ordinal),
			"Slow Cook aura should cap per-layer brightness spikes");
	}

	private static void FormVfxSafetySkipsMissingHolder()
	{
		Expect(
			!HextechFormVfxSafetyHooks.ShouldRunOriginal(hasFormVfxHolder: false),
			"form VFX should be skipped when a custom character has no holder");
		Expect(
			HextechFormVfxSafetyHooks.ShouldRunOriginal(hasFormVfxHolder: true),
			"form VFX should retain vanilla behavior when the holder exists");
	}

	private static void SymphonyOfWarPreservesDemonAndSerpentFormVfx()
	{
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Demon,
				FormVfxKind.Serpent),
			"Symphony of War should preserve Serpent Form VFX when Demon Form is added");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Serpent,
				FormVfxKind.Demon),
			"Symphony of War should preserve Demon Form VFX when Serpent Form is added");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Demon),
			"later non-Symphony forms should not erase Demon Form VFX");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Serpent),
			"later non-Symphony forms should not erase Serpent Form VFX");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Other),
			"non-Symphony forms should retain vanilla last-form-wins behavior");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: false,
				FormVfxKind.Demon,
				FormVfxKind.Serpent),
			"players without Symphony of War should keep vanilla replacement behavior");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Demon,
				FormVfxKind.Demon),
			"reapplying a form should replace its stale same-type VFX");
	}

	/// <summary>
	/// 批处理不再拦截任何 Hook.* 分发点:它只管一组并行飞行动画(替代逐张内置动画)和进场偏移。
	/// 出牌事件由代表牌走原版 CardCmd.AutoPlay 如实发出。
	/// </summary>
	private static void FormAutoPlayBatchOnlySuppressesDuplicateFlyVfx()
	{
		DemonForm firstCard = new();
		DemonForm secondCard = new();
		DemonForm outsideCard = new();
		HextechFormAutoPlayBatchState batch = new([firstCard, secondCard]);

		using (batch.BeginPowerCardFlyVfxPreview([firstCard, secondCard]))
		{
			Expect(batch.ShouldPlayPowerCardFlyVfx(firstCard), "form batch should show the first card in its group VFX");
			Expect(batch.ShouldPlayPowerCardFlyVfx(secondCard), "form batch should show later cards in its group VFX");
		}
		Expect(!batch.ShouldPlayPowerCardFlyVfx(firstCard), "form batch should suppress the first card's built-in duplicate VFX");
		Expect(!batch.ShouldPlayPowerCardFlyVfx(secondCard), "form batch should suppress later cards' built-in duplicate VFX");
		Expect(batch.ShouldPlayPowerCardFlyVfx(outsideCard), "form batch should not suppress VFX for non-batch cards");

		string[] hookTargets = BuildPatchManifest()
			.Where(line => line.StartsWith("combat.form-auto-play", StringComparison.Ordinal))
			.Where(line => line.Contains("MegaCrit.Sts2.Core.Hooks.Hook.", StringComparison.Ordinal))
			.ToArray();
		Expect(hookTargets.Length == 0, "form batch must not patch any Hook.* dispatcher: " + string.Join("; ", hookTargets));
	}

	private static void FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay()
	{
		DemonForm firstCard = new();
		DemonForm middleCard = new();
		DemonForm lastCard = new();
		DemonForm outsideCard = new();
		HextechFormAutoPlayBatchState batch = new([firstCard, middleCard, lastCard]);

		Expect(batch.TryGetHorizontalOffset(firstCard, out float firstOffset), "first form should have an entry offset");
		Expect(batch.TryGetHorizontalOffset(middleCard, out float middleOffset), "middle form should have an entry offset");
		Expect(batch.TryGetHorizontalOffset(lastCard, out float lastOffset), "last form should have an entry offset");
		Equal(-190f, firstOffset, "first form should enter left of center");
		Equal(0f, middleOffset, "middle form should enter at center");
		Equal(190f, lastOffset, "last form should enter right of center");
		Expect(!batch.TryGetHorizontalOffset(outsideCard, out _), "non-batch cards should keep the vanilla play target");
	}

	/// <summary>
	/// 代表牌走原版结算自己的数值 × 出牌次数;次要牌的贡献 = Σ(数值 × 各自出牌次数),0 次不贡献。
	/// 代表牌优先选流电牌,保证整批只触发一次电击。
	/// </summary>
	private static void FormAutoPlaySecondaryContributionSumsAmountTimesPlayCount()
	{
		decimal total = HextechFormAutoPlayHooks.SumSecondaryContribution([(2m, 1), (2m, 2), (3m, 0)]);
		Equal(6m, total, "secondary contribution should weight each card by its own play count and skip zero plays");
		Equal(0m, HextechFormAutoPlayHooks.SumSecondaryContribution([]), "no secondaries means no extra power");

		// 规范模型不能读 DynamicVars;这里只验证代表牌的选择规则。
		DemonForm first = new();
		DemonForm second = new();
		Expect(
			ReferenceEquals(HextechFormAutoPlayHooks.SelectPrimary([first, second]), first),
			"without galvanized the first form is the representative");
		Equal(1m, HextechFormAutoPlayHooks.GetFormAmount(new ReaperForm()), "reaper form contributes one stack per play without touching dynamic vars");
	}

	private static void FormAutoPlayBatchCombinesOnlyEffectNeutralEnchantments()
	{
		Expect(HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(null), "unenchanted forms should combine");
		Expect(
			HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new MegaCrit.Sts2.Core.Models.Enchantments.Clone()),
			"forms enchanted only with Clone should combine");
		Expect(
			!HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new MegaCrit.Sts2.Core.Models.Enchantments.Sharp()),
			"forms with effect-changing enchantments should keep the per-card path");
		Expect(
			!HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new UniversalSpiral()),
			"forms with replay enchantments should keep the per-card path");
	}
}
