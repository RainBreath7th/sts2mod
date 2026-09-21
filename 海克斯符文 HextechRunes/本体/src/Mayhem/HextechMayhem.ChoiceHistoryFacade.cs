namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public IReadOnlyList<HextechTelemetry.RuneChoiceRecord> GetTelemetryChoiceRecords()
	{
		return _choiceHistory.GetTelemetryChoiceRecords();
	}

	public void RecordTelemetryChoice(HextechTelemetry.RuneChoiceRecord record)
	{
		_choiceHistory.RecordTelemetryChoice(record);
	}

	public HashSet<ModelId> GetSeenPlayerRuneIds(Player player)
	{
		return _choiceHistory.GetSeenPlayerRuneIds(player, RunState);
	}

	public void RecordSeenPlayerRunes(Player player, IEnumerable<RelicModel> relics)
	{
		_choiceHistory.RecordSeenPlayerRunes(player, relics, RunState);
	}

	private string DescribePlayerHexCounts()
	{
		return HextechMayhemActRecovery.DescribePlayerHexCounts(RunState);
	}

	private string DescribeTelemetryChoiceCounts()
	{
		return HextechMayhemActRecovery.DescribeTelemetryChoiceCounts(_choiceHistory);
	}
}
