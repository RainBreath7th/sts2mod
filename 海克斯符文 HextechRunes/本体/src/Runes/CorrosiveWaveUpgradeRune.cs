using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;

namespace HextechRunes;

public sealed class CorrosiveWaveUpgradeRune : CardUpgradeRuneBase<CorrosiveWave>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<CorrosiveWave>(),
		HoverTipFactory.FromPower<CorrosiveWavePower>(),
		HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
	];

	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);

	// 给牌本身加消耗词条，而不是在结算时改去向：卡面会显示消耗，也会和"遗忘之魂"这类按词条生效的效果正常互动。
	public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
	{
		return Owner != null && GrantsExhaust(card, Owner) && keywords.Add(CardKeyword.Exhaust);
	}

	internal static bool GrantsExhaust(CardModel card, Player owner)
	{
		return card.Owner == owner && card is CorrosiveWave;
	}

	[HarmonyPatch(typeof(CorrosiveWavePower), nameof(CorrosiveWavePower.AfterSideTurnEnd), typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>))]
	[HextechPatch("rune.corrosive-wave", "升级腐蚀波", Rune = typeof(CorrosiveWaveUpgradeRune))]
	private static class CorrosiveWavePatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(CorrosiveWavePower __instance, ref Task __result)
		{
			if (__instance.Owner.Player?.GetRelic<CorrosiveWaveUpgradeRune>() == null)
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}
}
