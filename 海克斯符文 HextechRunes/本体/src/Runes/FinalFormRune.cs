namespace HextechRunes;

public sealed class FinalFormRune : HextechRelicBase
{
	private const decimal PlatingPercentValue = 0.10m;
	private const decimal PlatingDisplayPercentValue = PlatingPercentValue * 100m;

	private bool _triggeredThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => false;
		set
		{
			// Legacy save compatibility: this is turn-scoped runtime state and must not enter multiplayer checksums.
			_triggeredThisTurn = false;
			UpdateTurnScopedStateIdentity(null);
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MinCost", 2m),
		new DynamicVar("PlatingPercent", PlatingPercentValue),
		new CardsVar(2),
		new DynamicVar("PlatingDisplayPercent", PlatingDisplayPercentValue)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PlatingPower>()
	];

	public override Task BeforeCombatStart()
	{
		ResetTurnState(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnState(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetTurnState(combatState);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		EnsureTurnScopedStateCurrent(ResetTurnState);
		if (HasTurnProcTriggered(nameof(FinalFormRune), _triggeredThisTurn) || Owner == null || !IsOwnedCardWithEffectiveCostAtLeast(cardPlay.Card, DynamicVars["MinCost"].BaseValue))
		{
			return;
		}

		if (!TryConsumeTurnProc(nameof(FinalFormRune), ref _triggeredThisTurn))
		{
			return;
		}

		int plating = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * DynamicVars["PlatingPercent"].BaseValue));
		Flash();
		await PowerCmd.Apply<PlatingPower>(Owner.Creature, plating, Owner.Creature, cardPlay.Card);
		await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner, fromHandDraw: false);
	}

	private void ResetTurnState()
	{
		ResetTurnState(null);
	}

	private void ResetTurnState(HextechCombatState? combatState)
	{
		_triggeredThisTurn = false;
		UpdateTurnScopedStateIdentity(combatState);
	}
}
