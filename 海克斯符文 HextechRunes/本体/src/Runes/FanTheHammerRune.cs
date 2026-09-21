namespace HextechRunes;

public sealed class FanTheHammerRune : HextechRelicBase
{
	private const decimal DamageMultiplierValue = 0.35m;
	private const decimal DamagePercentValue = DamageMultiplierValue * 100m;

	private bool _triggeredThisTurn;
	private HextechCombatState? _turnStateCombat;
	private int _turnStateRoundNumber = -1;
	private CardModel? _damageReducedCard;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => false;
		set
		{
			// Legacy save compatibility: this is turn-scoped runtime state and must not enter multiplayer checksums.
			_triggeredThisTurn = false;
			_turnStateCombat = null;
			_turnStateRoundNumber = -1;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Replays", 3m),
		new DynamicVar("DamageMultiplier", DamageMultiplierValue),
		new DynamicVar("DamagePercent", DamagePercentValue)
	];

	public override Task BeforeCombatStart()
	{
		ResetTurnState();
		ClearDamageReducedCard();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnState();
		ClearDamageReducedCard();
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

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (card.Owner != Owner)
		{
			return playCount;
		}

		EnsureTurnStateCurrent();
		if (HasTurnProcTriggered(nameof(FanTheHammerRune), _triggeredThisTurn) || !IsOwnedAttack(card))
		{
			return playCount;
		}

		return playCount + GetReplayCount();
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (card.Owner != Owner)
		{
			return Task.CompletedTask;
		}

		EnsureTurnStateCurrent();
		if (!HasTurnProcTriggered(nameof(FanTheHammerRune), _triggeredThisTurn) && IsOwnedAttack(card))
		{
			if (TryConsumeTurnProc(nameof(FanTheHammerRune), ref _triggeredThisTurn))
			{
				UpdateTurnStateIdentity();
				TrackDamageReducedCard(card);
				Flash();
			}
		}

		return Task.CompletedTask;
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| cardSource is not CardModel card
			|| card != _damageReducedCard
			|| card.Owner != Owner
			|| dealer != Owner.Creature
			|| !IsOwnedAttack(card)
			|| target?.Side == CombatSide.Player
			|| (props & ValueProp.Unpowered) != 0)
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}

	private int GetReplayCount()
	{
		return DynamicVars["Replays"].IntValue;
	}

	private void TrackDamageReducedCard(CardModel card)
	{
		ClearDamageReducedCard();
		_damageReducedCard = card;
		card.Played += ClearDamageReducedCard;
	}

	private void ClearDamageReducedCard()
	{
		if (_damageReducedCard is CardModel card)
		{
			card.Played -= ClearDamageReducedCard;
		}

		_damageReducedCard = null;
	}

	private void ResetTurnState(HextechCombatState? combatState = null)
	{
		_triggeredThisTurn = false;
		UpdateTurnStateIdentity(combatState);
	}

	private void EnsureTurnStateCurrent()
	{
		HextechCombatState? combatState = Owner?.Creature.CombatState;
		if (combatState == null)
		{
			_triggeredThisTurn = false;
			_turnStateCombat = null;
			_turnStateRoundNumber = -1;
			return;
		}

		if (!ReferenceEquals(_turnStateCombat, combatState) || _turnStateRoundNumber != combatState.RoundNumber)
		{
			ResetTurnState(combatState);
		}
	}

	private void UpdateTurnStateIdentity(HextechCombatState? combatState = null)
	{
		combatState ??= Owner?.Creature.CombatState;
		_turnStateCombat = combatState;
		_turnStateRoundNumber = combatState?.RoundNumber ?? -1;
	}
}
