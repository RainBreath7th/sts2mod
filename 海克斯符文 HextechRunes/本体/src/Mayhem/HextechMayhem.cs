namespace HextechRunes;

internal sealed partial class HextechMayhemModifier : HextechModifierBase
{
	internal static HextechMayhemModifier? FindIn(IRunState? runState)
	{
		return runState?.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
	}

	internal static bool IsEnabledForRun(IRunState? runState)
	{
		if (FindIn(runState) is HextechMayhemModifier modifier)
		{
			return modifier.IsModActiveForRun;
		}

		// 联机缺少本局快照时不能用各端本地菜单值决定共享模型写入。
		return !HextechPlayerContextHelper.IsNetworkMultiplayerRun() && HextechRuneConfiguration.GetModEnabled();
	}
}
