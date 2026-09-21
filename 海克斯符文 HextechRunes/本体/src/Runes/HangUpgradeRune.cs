namespace HextechRunes;

public sealed class HangUpgradeRune : CardUpgradeRuneBase<Hang>
{
	protected override bool IsAvailableForCharacter(Player player) => IsNecrobinderPlayer(player);

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Hang>(), HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
		HoverTipFactory.FromPower<HextechHangPower>()
	];

	public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
	{
		return Owner != null && card.Owner == Owner && card is Hang && keywords.Add(CardKeyword.Exhaust);
	}

	internal static int NextIncrease(int current) => Math.Min(Math.Max(2, current), 999999999 - current);

	internal static async Task PlayUpgraded(PlayerChoiceContext context, Hang card, CardPlay play)
	{
		Creature target = play.Target!;
		await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
			.Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
		await PowerCmd.Apply<HextechHangPower>(context, target, NextIncrease(target.GetPowerAmount<HextechHangPower>()), card.Owner.Creature, card);
	}

	[HarmonyPatch(typeof(Hang), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.hang.all-damage", "升级吊杀", Rune = typeof(HangUpgradeRune))]
	private static class HangAllDamagePatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Hang __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (__instance.Owner?.GetRelic<HangUpgradeRune>() == null) return true;
			__result = PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
