namespace HextechRunes;

public sealed class ScapegoatRune : HextechRelicBase
{
	private int _transferCount;

	public override Task BeforeCombatStart()
	{
		_transferCount = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room) => BeforeCombatStart();

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (Owner == null || player != Owner || Owner.Creature.IsDead
			|| Owner.Creature.CombatState == null || CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		PowerModel[] debuffs = SnapshotDebuffs(Owner.Creature.Powers);
		if (debuffs.Length == 0) return;
		int ordinal = ConsumeCombatProcOrdinal(nameof(ScapegoatRune), ref _transferCount);
		Creature? target = HextechRuneTargeting.PickRandomHittableEnemy(Owner,
			Owner.Creature.CombatState, "scapegoat-target", ordinal.ToString());
		if (target == null) return;

		Flash();
		foreach (PowerModel debuff in debuffs)
		{
			if (CombatManager.Instance.IsOverOrEnding || target.IsDead || Owner.Creature.IsDead) break;
			if (!Owner.Creature.Powers.Contains(debuff) || !IsDebuff(debuff)) continue;

			int amount = debuff.Amount;
			PowerModel transferred = (PowerModel)debuff.ClonePreservingMutability();
			// 玩家侧“跳过下一次持续时间递减”不能带到敌人侧，否则会凭空延长弱化等效果。
			transferred.SkipNextDurationTick = false;
			await PowerCmd.Remove(debuff);
			await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply(choiceContext, transferred,
				target, amount, Owner.Creature, null);
		}
	}

	internal static PowerModel[] SnapshotDebuffs(IEnumerable<PowerModel> powers) =>
		powers.Where(IsDebuff).ToArray();

	private static bool IsDebuff(PowerModel power) =>
		power.GetTypeForAmount(power.Amount) == PowerType.Debuff;
}
