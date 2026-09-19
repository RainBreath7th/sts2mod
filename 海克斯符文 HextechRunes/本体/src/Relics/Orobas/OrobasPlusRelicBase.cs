using MegaCrit.Sts2.Core.Entities.Relics;

namespace HextechRunes;

// 保留原版起始稀有度，图鉴只枚举角色 StartingRelics 与第一次升级结果，因此默认不展示二次升级。
// 不继承 HextechRelicBase：这些是角色遗物，不应被重铸、海克斯计数或候选池识别为符文。
public abstract class OrobasPlusRelicBase : RelicModel
{
	public sealed override RelicRarity Rarity => RelicRarity.Starter;

	public sealed override bool IsAllowedInShops => false;

	protected abstract RelicModel OriginalRelic { get; }

	protected sealed override string IconBaseName => OriginalRelic.Id.Entry.ToLowerInvariant();
}
