namespace HextechRunes;

internal static partial class HextechRuneConfigMenuHooks
{
	/// <summary>
	/// 配置菜单要写入 <see cref="PendingConfig"/> 的字段分组。四路「重置当前页」、导入分享码
	/// 各自只写自己那一组,语义与拆出 <see cref="PendingConfig"/> 之前逐字段一致。
	/// </summary>
	[Flags]
	private enum PendingFields
	{
		None = 0,

		/// <summary>数量页:各幕海克斯数量、双方重掷上限、混沌符文概率。</summary>
		ActCounts = 1 << 0,

		/// <summary>符文池页:玩家符文与敌方海克斯的禁用集合。</summary>
		RunePools = 1 << 1,

		/// <summary>锻造页:锻造禁用集合。</summary>
		ForgePool = 1 << 2,

		/// <summary>杂项页的数值部分:稀有度权重矩阵、金色重掷概率、锻造售价、随机锻造直给、防连续白银。</summary>
		Details = 1 << 3,

		/// <summary>模组总开关。</summary>
		ModEnabled = 1 << 4,

		/// <summary>三个 UI 偏好(隐藏遗物开关、更新提示、折叠敌方海克斯),不进快照,取值由调用方给出。</summary>
		UiPreferences = 1 << 5,

		/// <summary>导入分享码写入的集合:刻意排除 <see cref="ModEnabled"/> 与 <see cref="UiPreferences"/>。</summary>
		ShareCode = ActCounts | RunePools | ForgePool | Details
	}

	/// <summary>
	/// 配置菜单的编辑态。界面上每个控件读写的都是这里的字段,「保存并关闭」才落盘;「取消」直接丢弃。
	/// 数组与集合字段引用稳定(只就地改写、绝不换实例),因为数值标签、开关和符文格子的绑定
	/// 都持有同一个实例;换引用会让界面停留在旧数据上。
	/// </summary>
	private sealed class PendingConfig
	{
		private PendingConfig(
			int[] playerHexCounts,
			int[] enemyHexCounts,
			HashSet<string> disabledPlayerRuneIds,
			HashSet<string> disabledMonsterHexIds,
			HashSet<string> disabledForgeIds,
			int[][] runeWeightsByAct,
			int[] forgeWeights)
		{
			PlayerHexCounts = playerHexCounts;
			EnemyHexCounts = enemyHexCounts;
			DisabledPlayerRuneIds = disabledPlayerRuneIds;
			DisabledMonsterHexIds = disabledMonsterHexIds;
			DisabledForgeIds = disabledForgeIds;
			RuneWeightsByAct = runeWeightsByAct;
			ForgeWeights = forgeWeights;
		}

		internal readonly int[] PlayerHexCounts;
		internal readonly int[] EnemyHexCounts;
		internal readonly HashSet<string> DisabledPlayerRuneIds;
		internal readonly HashSet<string> DisabledMonsterHexIds;
		internal readonly HashSet<string> DisabledForgeIds;
		internal readonly int[][] RuneWeightsByAct;
		internal readonly int[] ForgeWeights;

		internal int PlayerRuneRerollLimit;
		internal int MonsterHexRerollLimit;
		internal int GoldenRerollChancePercent;
		internal int ChaosRuneChancePercent;
		internal int ForgePrice;
		internal bool RandomForgeDirectGrant;
		internal bool PreventConsecutiveSilverRunes;
		internal bool ModEnabled;

		// 三个 UI 偏好不属于运行配置快照,单独走 HextechRelicVisibilityHooks 的持久化路径。
		internal bool ShowHiddenRelicsToggle;
		internal bool ShowUpdateNotice;
		internal bool CollapseEnemyHexes;

		/// <summary>打开菜单时从当前配置快照 + 当前 UI 偏好建立编辑态(数组与集合一律复制一份)。</summary>
		internal static PendingConfig From(
			HextechRunConfigurationSnapshot snapshot,
			bool showHiddenRelicsToggle,
			bool showUpdateNotice,
			bool collapseEnemyHexes)
		{
			return new PendingConfig(
				snapshot.PlayerHexCountsByAct.ToArray(),
				snapshot.EnemyHexCountsByAct.ToArray(),
				snapshot.DisabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal),
				snapshot.DisabledMonsterHexIds.ToHashSet(StringComparer.Ordinal),
				snapshot.DisabledForgeIds.ToHashSet(StringComparer.Ordinal),
				snapshot.RuneRarityWeightsByAct.Select(ToWeightArray).ToArray(),
				ToWeightArray(snapshot.ForgeRarityWeights))
			{
				PlayerRuneRerollLimit = snapshot.PlayerRuneRerollLimit,
				MonsterHexRerollLimit = snapshot.MonsterHexRerollLimit,
				GoldenRerollChancePercent = snapshot.GoldenRerollChancePercent,
				ChaosRuneChancePercent = snapshot.ChaosRuneChancePercent,
				ForgePrice = snapshot.RandomForgeShopPrice,
				RandomForgeDirectGrant = snapshot.RandomForgeDirectGrant,
				PreventConsecutiveSilverRunes = snapshot.PreventConsecutiveSilverRunes,
				ModEnabled = snapshot.ModEnabled,
				ShowHiddenRelicsToggle = showHiddenRelicsToggle,
				ShowUpdateNotice = showUpdateNotice,
				CollapseEnemyHexes = collapseEnemyHexes
			};
		}

		/// <summary>把编辑态打包成运行配置快照:「保存并关闭」与「导出分享码」共用同一份实参。</summary>
		internal HextechRunConfigurationSnapshot ToSnapshot()
		{
			return new HextechRunConfigurationSnapshot(
				PlayerHexCounts,
				EnemyHexCounts,
				PlayerRuneRerollLimit,
				MonsterHexRerollLimit,
				DisabledPlayerRuneIds,
				DisabledMonsterHexIds,
				DisabledForgeIds,
				ToRarityWeightsByAct(RuneWeightsByAct),
				PreventConsecutiveSilverRunes,
				GoldenRerollChancePercent,
				ToForgeRarityWeights(ForgeWeights),
				ForgePrice,
				RandomForgeDirectGrant,
				ModEnabled,
				ChaosRuneChancePercent);
		}

		/// <summary>
		/// 用快照覆盖 <paramref name="fields"/> 指定的那几组字段,其余字段原样保留。
		/// 三个 UI 偏好不在快照里,只有带 <see cref="PendingFields.UiPreferences"/> 时才用入参覆盖。
		/// </summary>
		internal void LoadFrom(
			HextechRunConfigurationSnapshot snapshot,
			PendingFields fields,
			bool showHiddenRelicsToggle = false,
			bool showUpdateNotice = false,
			bool collapseEnemyHexes = false)
		{
			if ((fields & PendingFields.ActCounts) != 0)
			{
				CopyArray(snapshot.PlayerHexCountsByAct, PlayerHexCounts);
				CopyArray(snapshot.EnemyHexCountsByAct, EnemyHexCounts);
				PlayerRuneRerollLimit = snapshot.PlayerRuneRerollLimit;
				MonsterHexRerollLimit = snapshot.MonsterHexRerollLimit;
				ChaosRuneChancePercent = snapshot.ChaosRuneChancePercent;
			}

			if ((fields & PendingFields.RunePools) != 0)
			{
				ReplaceIds(DisabledPlayerRuneIds, snapshot.DisabledPlayerRuneIds);
				ReplaceIds(DisabledMonsterHexIds, snapshot.DisabledMonsterHexIds);
			}

			if ((fields & PendingFields.ForgePool) != 0)
			{
				ReplaceIds(DisabledForgeIds, snapshot.DisabledForgeIds);
			}

			if ((fields & PendingFields.Details) != 0)
			{
				for (int actIndex = 0; actIndex < RuneWeightsByAct.Length; actIndex++)
				{
					CopyArray(ToWeightArray(snapshot.RuneRarityWeightsByAct[actIndex]), RuneWeightsByAct[actIndex]);
				}

				CopyArray(ToWeightArray(snapshot.ForgeRarityWeights), ForgeWeights);
				GoldenRerollChancePercent = snapshot.GoldenRerollChancePercent;
				ForgePrice = snapshot.RandomForgeShopPrice;
				RandomForgeDirectGrant = snapshot.RandomForgeDirectGrant;
				PreventConsecutiveSilverRunes = snapshot.PreventConsecutiveSilverRunes;
			}

			if ((fields & PendingFields.ModEnabled) != 0)
			{
				ModEnabled = snapshot.ModEnabled;
			}

			if ((fields & PendingFields.UiPreferences) != 0)
			{
				ShowHiddenRelicsToggle = showHiddenRelicsToggle;
				ShowUpdateNotice = showUpdateNotice;
				CollapseEnemyHexes = collapseEnemyHexes;
			}
		}

		// 就地 Clear + UnionWith:绑定持有同一个集合实例,换引用界面不会刷新。
		private static void ReplaceIds(HashSet<string> target, IEnumerable<string> source)
		{
			target.Clear();
			target.UnionWith(source);
		}

		// 就地逐元素赋值,按较短的一方截断,与拆出本类之前的行为一致。
		private static void CopyArray(IReadOnlyList<int> source, int[] target)
		{
			for (int i = 0; i < Math.Min(source.Count, target.Length); i++)
			{
				target[i] = source[i];
			}
		}
	}
}
