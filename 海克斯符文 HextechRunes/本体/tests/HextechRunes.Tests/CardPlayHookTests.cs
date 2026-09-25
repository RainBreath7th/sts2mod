using System.Reflection;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;

namespace HextechRunes.Tests;

internal static partial class Program
{
	/// <summary>
	/// 禁玩/放行不再用全局 CardModel.CanPlay 补丁:阻止出牌走官方 ShouldPlay 虚方法(原版会带上 BlockedByHook 与 preventer),
	/// 蓝蜡烛走关键词修改虚方法,压轴放行只补 GrandFinale 自己的 IsPlayable。
	/// </summary>
	private static void CardPlayBlockersUseOfficialShouldPlayHook()
	{
		const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		Expect(typeof(BackToBasicsRune).GetMethod("ShouldPlay", declared) != null, "Back to Basics rune should block via ShouldPlay");
		// 自动打出在读费用和持有者之前就放行,所以无持有者的实例也能验证;3 费形态牌开局自动打出依赖这一点。
		Expect(new BackToBasicsRune().ShouldPlay(new DemonForm(), AutoPlayType.Default), "Back to Basics should not block auto-played 3-cost cards");
		Expect(typeof(KakaRune).GetMethod("ShouldPlay", declared) != null, "Kaka rune should block via ShouldPlay");
		Expect(typeof(HextechMayhemModifier).GetMethod("ShouldPlay", declared) != null, "enemy hexes should block via the modifier's ShouldPlay");
		Expect(typeof(BackToBasicsEnemyHex).GetMethod("ShouldPlay", declared) != null, "enemy Back to Basics should implement the hex-level ShouldPlay");
		Expect(typeof(BlueCandleMedkitRune).GetMethod("TryModifyKeywordsInCombat", declared) != null, "Blue Candle should clear Unplayable through the keyword hook");
		Expect(HextechPatcher.FindPatchMethod(typeof(GrandFinaleUpgradeRune), "GrandFinalePlayablePatch", "Postfix") != null, "Grand Finale allowance should be a narrow IsPlayable patch");
		Expect(typeof(GrandFinale).GetProperty("IsPlayable", declared) != null, "Grand Finale should still own its IsPlayable override");

		Expect(
			typeof(HextechCombatHooks).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).All(static nested => nested.Name is not "CanPlayPatch" and not "CanPlayWithReasonPatch"),
			"no global CardModel.CanPlay patch should remain");
	}
}
