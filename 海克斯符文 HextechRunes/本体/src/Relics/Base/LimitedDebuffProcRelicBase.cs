using MegaCrit.Sts2.Core.Entities.Relics;

namespace HextechRunes;

public abstract class LimitedDebuffProcRelicBase : HextechRelicBase
{
	private int _procsThisTurn;

	// 无上限的子类不再写入这个计数，但属性本身保留：它在 SavedProperty 清单里，删掉会改变保存与联机布局。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedProcsThisTurn
	{
		get
		{
			EnsureTurnScopedStateCurrent(ResetProcs);
			return GetTurnProcCount(GetProcKey(), _procsThisTurn);
		}
		set
		{
			_procsThisTurn = Math.Max(0, value);
			UpdateDisplay();
			UpdateTurnScopedStateIdentity();
		}
	}

	protected virtual int MaxProcsPerTurn => 3;

	/// <summary>false = 不限每回合次数，也不显示剩余次数。</summary>
	protected virtual bool HasTurnLimit => true;

	/// <summary>true = 监听持有者自己收到的负面效果（来源不限）；false = 监听持有者给敌人施加的负面效果。</summary>
	protected virtual bool ListensToOwnerDebuffs => false;

	public override bool ShowCounter => HasTurnLimit && CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => HasTurnLimit && !IsCanonical ? Math.Max(0, MaxProcsPerTurn - GetTurnProcCount(GetProcKey(), _procsThisTurn)) : 0;

	public override Task BeforeCombatStart()
	{
		ResetProcs(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetProcs(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetProcs(combatState);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (HasTurnLimit)
		{
			EnsureTurnScopedStateCurrent(ResetProcs);
		}

		Creature? target;
		bool matched = ListensToOwnerDebuffs
			? TryGetOwnerReceivedDebuff(power, amount, out target)
			: TryGetOwnedEnemyDebuffTarget(power, amount, applier, out target);
		if (!matched)
		{
			return;
		}

		if (HasTurnLimit)
		{
			string procKey = GetProcKey();
			if (HasTurnProcReachedLimit(procKey, _procsThisTurn, MaxProcsPerTurn)
				|| !TryConsumeTurnProc(procKey, ref _procsThisTurn, MaxProcsPerTurn))
			{
				return;
			}

			UpdateDisplay();
		}

		Flash(target == null ? Array.Empty<Creature>() : [target]);
		await OnEnemyDebuffApplied(target!);
	}

	/// <summary>触发回调。监听敌方时 target 是收到负面效果的敌人；监听自身时 target 是持有者。</summary>
	protected abstract Task OnEnemyDebuffApplied(Creature target);

	private void ResetProcs()
	{
		ResetProcs(null);
	}

	private void ResetProcs(HextechCombatState? combatState)
	{
		_procsThisTurn = 0;
		UpdateDisplay();
		UpdateTurnScopedStateIdentity(combatState);
	}

	private void UpdateDisplay()
	{
		Status = HasTurnLimit && GetTurnProcCount(GetProcKey(), _procsThisTurn) == MaxProcsPerTurn - 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}

	private string GetProcKey()
	{
		return GetStableTurnProcKey();
	}
}
