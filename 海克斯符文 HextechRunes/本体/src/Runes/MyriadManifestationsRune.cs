namespace HextechRunes;

public sealed class MyriadManifestationsRune : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => IsDefectPlayer(player);

	public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
		IEnumerable<Creature> participants)
	{
		if (Owner?.PlayerCombatState == null || Owner.Creature.Side != side || Owner.Creature.IsDead
			|| !participants.Contains(Owner.Creature))
		{
			return;
		}

		OrbModel[] orbs = Owner.PlayerCombatState.OrbQueue.Orbs.ToArray();
		int rounds = CountOrbTypes(orbs);
		if (rounds == 0)
		{
			return;
		}

		Flash();
		// 触发开始时冻结种类数和球的顺序。额外被动生成的新球不扩大这次结算。
		for (int round = 0; round < rounds; round++)
		{
			foreach (OrbModel orb in orbs)
			{
				if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead
					|| Owner.PlayerCombatState == null)
				{
					return;
				}
				if (Owner.PlayerCombatState.OrbQueue.Orbs.Contains(orb))
				{
					// 保留原版被动次数修饰及相关回调，兼容增强被动的效果和其他模组充能球。
					await HextechOrbPassiveCompat.TriggerPassive(choiceContext, orb);
				}
			}
		}
	}

	internal static int CountOrbTypes(IEnumerable<OrbModel> orbs) => orbs.Select(orb => orb.Id).Distinct().Count();
}
