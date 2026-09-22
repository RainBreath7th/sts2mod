using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

/// <summary>
/// 海克斯版精神过载(正面效果):持有「升级：精神过载」时,打出精神过载改为施加本能力而非原版减益。
/// 与原版同名同图标;在你的回合开始时,对所有存活敌人施加等同于层数的灾厄。
/// 作为正面效果,它不再被人工制品拦截、不会被"移除负面效果"清掉,也不参与任何按负面效果触发的联动。
/// </summary>
public sealed class HextechNeurosurgePower : HextechPowerBase
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	// 标题直接复用原版精神过载的本地化条目,九种语言都与卡牌所写名称一致;描述用本模组的 HEXTECH_NEUROSURGE_POWER.*。
	public override LocString Title => ModelDb.Power<NeurosurgePower>().Title;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<DoomPower>()];

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		if (side != CombatSide.Player || Owner.Side != CombatSide.Player || Owner.IsDead || Amount <= 0)
		{
			return;
		}

		IReadOnlyList<Creature> enemies = HextechCombatCreatureHelper.GetAliveEnemies(combatState);
		if (enemies.Count == 0)
		{
			return;
		}

		Flash();
		ThrowingPlayerChoiceContext context = new();
		foreach (Creature enemy in enemies)
		{
			await PowerCmd.Apply<DoomPower>(context, enemy, Amount, Owner, null);
		}
	}
}
