using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HextechRunes;

internal sealed class CeremonialBeastEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.CeremonialBeast;
}

internal sealed class CeremonialBeastStrengthIntent(Creature source, int strength) : BuffIntent
{
	internal Creature Source => source;
	internal int Strength => strength;

	public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
	{
		LocString label = new("intents", "HEXTECH_STRENGTH.label");
		label.Add("Amount", strength);
		return label;
	}

	protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
	{
		LocString description = new("intents", "HEXTECH_STRENGTH.description");
		description.Add("Amount", strength);
		return description;
	}
}
