using MegaCrit.Sts2.Core.Models.Afflictions;

namespace HextechRunes;

/// <summary>
/// 玩家侧「流电」：复刻原版 GalvanicPower 但按 mod 语义调整——
/// 1. 计为 Debuff（可被清负面、会被人工制品抵挡）;
/// 2. 只感染 Owner 自己的能力牌（原版感染 Allies 全体玩家,联机会牵连队友）;
/// 3. 战斗中途施加也会立即感染 Owner 当前所有能力牌（原版只在 BeforeCombatStart 感染,
///    中途获得时已在场的能力牌不会中招）——用 AfterApplied（仅首次施加触发,叠层不重复跑）。
/// 附魔复用原版 Galvanized（文案/hover/图标全现成）。打出流电牌的自损只对 Owner 自己的牌结算,
/// 与同场可能存在的原版 GalvanicPower 互不重复（原版按"任意流电牌"结算属原版语义,不干预）。
/// 联机确定性:感染与结算均由两端一致执行的战斗 hook 驱动,无本地状态。
/// </summary>
public sealed class HextechGalvanicPower : HextechPowerBase
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new StringVar("AfflictionTitle", ModelDb.Affliction<Galvanized>().Title.GetFormattedText())
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromAffliction<Galvanized>(Amount);

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		await AfflictOwnerPowerCards();
	}

	public override async Task BeforeCombatStart()
	{
		await AfflictOwnerPowerCards();
	}

	public override async Task AfterCardEnteredCombat(CardModel card)
	{
		if (card.Owner?.Creature == Owner && card.Affliction == null && card.Type == CardType.Power)
		{
			await CardCmd.Afflict<Galvanized>(card, Amount);
		}
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Affliction is Galvanized)
		{
			await CreatureCmd.Damage(choiceContext, Owner, Amount, ValueProp.Unpowered | ValueProp.Move, null, null);
			try
			{
				VfxCmd.PlayOnCreature(Owner, "vfx/vfx_attack_lightning");
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Galvanic] Lightning visual failed: {ex.Message}");
			}
		}
	}

	private async Task AfflictOwnerPowerCards()
	{
		if (Owner?.Player?.PlayerCombatState is not { } playerCombatState)
		{
			return;
		}

		foreach (CardModel card in playerCombatState.AllCards
			.Where(static card => card.Type == CardType.Power && card.Affliction == null)
			.ToList())
		{
			await CardCmd.Afflict<Galvanized>(card, Amount);
		}
	}
}
