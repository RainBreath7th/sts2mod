#if STS2_110_OR_NEWER
namespace HextechRunes;

internal static class HextechOrbPassiveCompat
{
	internal static Task TriggerPassive(PlayerChoiceContext context, OrbModel orb) => orb.TriggerPassive(context, null);
}
#endif
