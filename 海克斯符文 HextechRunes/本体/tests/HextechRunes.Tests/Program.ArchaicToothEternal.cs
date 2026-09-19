using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void ArchaicToothTransformsEternalOnlyWithinItsNativeObtainTask()
	{
		Type[] added = new[] { typeof(Bash), typeof(Break), typeof(Neutralize), typeof(Suppress), typeof(Unleash),
			typeof(Protector), typeof(FallingStar), typeof(MeteorShower), typeof(Dualcast), typeof(Quadcast) }
			.Where(type => !ModelDb.Contains(type)).ToArray();
		FieldInfo loadedField = AccessTools.Field(typeof(HextechRuneConfiguration), "_loaded");
		object config = AccessTools.Field(typeof(HextechRuneConfiguration), "_config").GetValue(null)!;
		PropertyInfo enabledProperty = config.GetType().GetProperty("ModEnabled")!;
		object? loaded = loadedField.GetValue(null);
		object? enabled = enabledProperty.GetValue(config);
		Harmony harmony = new("HextechRunes.Tests.ArchaicToothEternal");
		try
		{
			foreach (Type type in added) ModelDb.Inject(type);
			loadedField.SetValue(null, true);
			enabledProperty.SetValue(config, true);
			RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
			AccessTools.Property(typeof(RunState), nameof(RunState.Modifiers)).SetValue(run, Array.Empty<ModifierModel>());
			AccessTools.Field(typeof(RunState), "_allCards").SetValue(run, new List<CardModel>());
			Player owner = CreateOrdinalTestPlayer(1);
			AccessTools.Field(typeof(Player), "_runState").SetValue(owner, run);
			AccessTools.Field(typeof(Player), "<Creature>k__BackingField").SetValue(owner, RuntimeHelpers.GetUninitializedObject(typeof(Creature)));
			CardPile deck = new(PileType.Deck);
			AccessTools.Field(typeof(Player), "<Deck>k__BackingField").SetValue(owner, deck);
			CardModel bash = run.CreateCard<Bash>(owner);
			CardCmd.Upgrade(bash, CardPreviewStyle.None);
			CardCmd.Enchant(CreateMutableTestModel<TezcatarasEmber>(), bash, 1m);
			CardModel other = run.CreateCard<Neutralize>(owner);
			other.AddKeyword(CardKeyword.Eternal);
			// 只填充托管牌堆；不触发需要 Godot 的牌堆 UI 通知。
			((List<CardModel>)AccessTools.Field(typeof(CardPile), "_cards").GetValue(deck)!).AddRange([bash, other]);
			ExpectThrows<InvalidOperationException>(() => new CardTransformation(bash, run.CreateCard<Break>(owner)), "native eternal deck transformation reproduces the error");

			foreach (Type patch in typeof(ArchaicToothEternalHooks).GetNestedTypes(BindingFlags.NonPublic)
				.Where(type => type.GetCustomAttribute<HextechPatchAttribute>() != null))
			{
				harmony.CreateClassProcessor(patch).Patch();
			}
			// 保留古老牙齿的真实选牌、升级、附魔复制及异步调用。
			// 仅替代转换命令的场景/动画部分，仍调用原版 CardTransformation 的限制检查。
			harmony.Patch(AccessTools.Method(typeof(CardCmd), nameof(CardCmd.Transform), [typeof(CardModel), typeof(CardModel), typeof(CardPreviewStyle)]),
				prefix: new HarmonyMethod(typeof(Program), nameof(VerifyEternalToothTransformPrefix)));
			ArchaicTooth tooth = CreateMutableTestModel<ArchaicTooth>();
			tooth.Owner = owner;
			Task obtained = tooth.AfterObtained();
			Expect(!bash.IsTransformable && !other.IsTransformable, "pending async conversion does not leak permission to caller");
			obtained.GetAwaiter().GetResult();
			Expect(!bash.IsTransformable && !bash.IsRemovable, "completion retains eternal and clears conversion permission");
			enabledProperty.SetValue(config, false);
			ExpectThrows<InvalidOperationException>(() => tooth.AfterObtained().GetAwaiter().GetResult(), "disabled mod preserves native eternal refusal");
			Expect(!bash.IsTransformable, "failed conversion leaves no permission behind");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
			enabledProperty.SetValue(config, enabled);
			loadedField.SetValue(null, loaded);
			foreach (Type type in added) ModelDb.Remove(type);
		}
	}

	private static bool VerifyEternalToothTransformPrefix(CardModel original, CardModel replacement, ref Task<CardPileAddResult?> __result)
	{
		__result = VerifyEternalToothTransform(original, replacement);
		return false;
	}

	private static async Task<CardPileAddResult?> VerifyEternalToothTransform(CardModel original, CardModel replacement)
	{
		_ = new CardTransformation(original, replacement);
		await Task.Yield();
		_ = new CardTransformation(original, replacement);
		Expect(replacement is Break && replacement.IsUpgraded, "native tooth changes upgraded Bash into upgraded Break");
		Expect(replacement.Enchantment is TezcatarasEmber && replacement.Enchantment.Amount == original.Enchantment!.Amount, "native enchantment cloning retains ember amount");
		Equal(0, replacement.EnergyCost.GetWithModifiers(CostModifiers.None), "ember retains zero cost");
		Expect(original.Keywords.Contains(CardKeyword.Eternal) && replacement.Keywords.Contains(CardKeyword.Eternal), "eternal stays on both card instances");
		Expect(!original.IsRemovable && !original.Owner.Deck.Cards.Last().IsTransformable, "neither removal nor another eternal card is allowed");
		return new CardPileAddResult { success = true, cardAdded = replacement };
	}
}
