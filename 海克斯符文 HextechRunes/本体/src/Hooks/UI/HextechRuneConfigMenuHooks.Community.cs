using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

// 社区配置面板：精选(人工审核)/热门/最新/我的 四个 tab,上传当前配置、点赞、举报、删除自己的上传。
// 「应用」与导入配置码同路径:填充配置界面 pending 编辑态,「保存并关闭」生效。
internal static partial class HextechRuneConfigMenuHooks
{
	private sealed record CommunityDisplayEntry(
		string? Id,
		string Title,
		string Author,
		string Code,
		int Likes,
		bool IsCommunity,
		bool IsMine,
		bool Hidden);

	private static readonly HashSet<string> SessionLikedIds = [];

	// 按钮文字由 AddCrispButtonText 加的 MegaLabel 子节点承载;直接改 Button.Text 会与其叠字。
	private static void SetActionButtonText(Button button, string text)
	{
		foreach (Node child in button.GetChildren())
		{
			if (child is MegaLabel label)
			{
				label.SetTextAutoSize(text);
				return;
			}
		}
	}

	private static void OpenCommunityConfigsPanel(
		Control overlay,
		Action<HextechConfigShareCodec.ImportPreview> applyPreview,
		Func<string> buildPendingCode,
		bool compactLayout)
	{
		Control blocker = new()
		{
			Name = "HextechCommunityConfigsBlocker",
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ColorRect dim = new()
		{
			Color = new Color(0f, 0f, 0f, 0.55f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		blocker.AddChild(dim);

		PanelContainer panel = new()
		{
			CustomMinimumSize = new Vector2(compactLayout ? 540f : 680f, compactLayout ? 470f : 570f)
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;
		blocker.AddChild(panel);

		MarginContainer margin = new();
		int pad = compactLayout ? 16 : 22;
		margin.AddThemeConstantOverride("margin_left", pad);
		margin.AddThemeConstantOverride("margin_right", pad);
		margin.AddThemeConstantOverride("margin_top", compactLayout ? 12 : 16);
		margin.AddThemeConstantOverride("margin_bottom", compactLayout ? 12 : 16);
		panel.AddChild(margin);

		VBoxContainer body = new();
		body.AddThemeConstantOverride("separation", compactLayout ? 10 : 12);
		margin.AddChild(body);

		Label title = CreateLabel(L("HEXTECH_CONFIG_FEATURED"), compactLayout ? 17 : 19, new Color(0.95f, 0.87f, 0.62f, 1f));
		title.HorizontalAlignment = HorizontalAlignment.Center;
		body.AddChild(title);

		ColorRect hairline = new()
		{
			Color = new Color(0.86f, 0.74f, 0.42f, 0.28f),
			CustomMinimumSize = new Vector2(0f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		body.AddChild(hairline);

		HBoxContainer tabs = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		tabs.AddThemeConstantOverride("separation", compactLayout ? 8 : 12);
		body.AddChild(tabs);

		Label status = CreateLabel(L("HEXTECH_CONFIG_FEATURED_LOADING"), 13, new Color(0.82f, 0.86f, 0.94f, 0.9f));
		status.HorizontalAlignment = HorizontalAlignment.Center;
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(status);

		ScrollContainer scroll = new()
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer list = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		list.AddThemeConstantOverride("separation", compactLayout ? 8 : 10);
		scroll.AddChild(list);
		body.AddChild(scroll);

		HBoxContainer bottom = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		bottom.AddThemeConstantOverride("separation", compactLayout ? 10 : 14);
		bool hasSteam = HextechSteamIdentity.TryGetSteamId(out string mySteamId);
		string currentTab = "featured";
		Action reloadCurrentTab = () => { };

		Button upload = CreateActionButton(L("HEXTECH_COMMUNITY_UPLOAD"), () =>
			OpenCommunityUploadDialog(blocker, mySteamId, buildPendingCode, () => reloadCurrentTab(), compactLayout), compactLayout);
		upload.Disabled = !hasSteam;
		if (!hasSteam)
		{
			upload.TooltipText = L("HEXTECH_COMMUNITY_NEED_STEAM");
		}

		bottom.AddChild(upload);
		Button close = CreateActionButton(L("HEXTECH_CONFIG_CANCEL"), () => blocker.QueueFree(), compactLayout);
		bottom.AddChild(close);
		body.AddChild(bottom);

		List<Button> tabButtons = [];
		Action<string> selectTab = tab =>
		{
			currentTab = tab;
			foreach (Button button in tabButtons)
			{
				button.Disabled = (string)button.GetMeta("communityTab") == tab;
			}

			foreach (Node child in list.GetChildren())
			{
				child.QueueFree();
			}

			status.Visible = true;
			status.Text = L("HEXTECH_CONFIG_FEATURED_LOADING");
			TaskHelper.RunSafely(PopulateCommunityListAsync(blocker, list, status, tab, mySteamId, applyPreview, () => reloadCurrentTab(), compactLayout));
		};
		reloadCurrentTab = () => selectTab(currentTab);

		foreach ((string key, string locKey) in new[]
		{
			("featured", "HEXTECH_COMMUNITY_TAB_FEATURED"),
			("hot", "HEXTECH_COMMUNITY_TAB_HOT"),
			("new", "HEXTECH_COMMUNITY_TAB_NEW"),
			("mine", "HEXTECH_COMMUNITY_TAB_MINE")
		})
		{
			if (key == "mine" && !hasSteam)
			{
				continue;
			}

			Button tabButton = CreateActionButton(L(locKey), () => selectTab(key), compactLayout);
			tabButton.SetMeta("communityTab", key);
			tabButtons.Add(tabButton);
			tabs.AddChild(tabButton);
		}

		overlay.AddChild(blocker);
		HextechControllerOverlay.RegisterModal(blocker, tabButtons.FirstOrDefault());
		selectTab("featured");
	}

	private static async Task PopulateCommunityListAsync(
		Control blocker,
		VBoxContainer list,
		Label status,
		string tab,
		string mySteamId,
		Action<HextechConfigShareCodec.ImportPreview> applyPreview,
		Action reloadTab,
		bool compactLayout)
	{
		List<CommunityDisplayEntry> entries = [];
		bool failed = false;
		if (tab == "featured")
		{
			IReadOnlyList<HextechFeaturedConfigs.FeaturedConfigEntry>? featured = await HextechFeaturedConfigs.FetchAsync().ConfigureAwait(false);
			if (featured == null)
			{
				failed = true;
			}
			else
			{
				entries = featured
					.Select(entry => new CommunityDisplayEntry(
						entry.Id, entry.Name ?? string.Empty, entry.Author ?? string.Empty, entry.Code ?? string.Empty,
						-1, IsCommunity: false, IsMine: false, Hidden: false))
					.ToList();
			}
		}
		else
		{
			IReadOnlyList<HextechFeaturedConfigs.CommunityConfigEntry>? community = tab == "mine"
				? await HextechFeaturedConfigs.FetchMineAsync(mySteamId).ConfigureAwait(false)
				: await HextechFeaturedConfigs.FetchCommunityAsync(tab).ConfigureAwait(false);
			if (community == null)
			{
				failed = true;
			}
			else
			{
				entries = community
					.Select(entry => new CommunityDisplayEntry(
						entry.Id, entry.Title ?? string.Empty, entry.Author ?? string.Empty, entry.Code ?? string.Empty,
						entry.Likes, IsCommunity: true, IsMine: tab == "mine", Hidden: entry.Hidden))
					.ToList();
			}
		}

		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(blocker) || !GodotObject.IsInstanceValid(list) || !GodotObject.IsInstanceValid(status))
			{
				return;
			}

			if (failed)
			{
				status.Text = L("HEXTECH_CONFIG_FEATURED_ERROR");
				return;
			}

			if (entries.Count == 0)
			{
				status.Text = L("HEXTECH_CONFIG_FEATURED_EMPTY");
				return;
			}

			status.Visible = false;
			foreach (CommunityDisplayEntry entry in entries)
			{
				list.AddChild(CreateCommunityConfigCard(blocker, entry, mySteamId, applyPreview, reloadTab, compactLayout));
			}
		}).CallDeferred();
	}

	private static Control CreateCommunityConfigCard(
		Control blocker,
		CommunityDisplayEntry entry,
		string mySteamId,
		Action<HextechConfigShareCodec.ImportPreview> applyPreview,
		Action reloadTab,
		bool compactLayout)
	{
		PanelContainer card = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleBoxFlat cardStyle = CreateButtonStyle(new Color(0.1f, 0.12f, 0.17f, 0.75f), new Color(0.46f, 0.55f, 0.68f, 0.5f));
		cardStyle.ContentMarginLeft = compactLayout ? 12f : 14f;
		cardStyle.ContentMarginRight = compactLayout ? 12f : 14f;
		cardStyle.ContentMarginTop = compactLayout ? 8f : 10f;
		cardStyle.ContentMarginBottom = compactLayout ? 8f : 10f;
		card.AddThemeStyleboxOverride("panel", cardStyle);

		// 布局:左列(标题/作者 + 简介贴左下) | 右侧操作按钮竖排——简介长短不影响按钮位置。
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 10 : 14);
		card.AddChild(row);

		VBoxContainer left = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		left.AddThemeConstantOverride("separation", 4);
		row.AddChild(left);

		HBoxContainer headerRow = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		headerRow.AddThemeConstantOverride("separation", 8);
		Label name = CreateLabel(entry.Title, compactLayout ? 16 : 18, new Color(0.96f, 0.9f, 0.7f, 1f));
		name.VerticalAlignment = VerticalAlignment.Center;
		headerRow.AddChild(name);
		if (entry.Hidden)
		{
			Label hiddenTag = CreateLabel(L("HEXTECH_COMMUNITY_HIDDEN"), 12, new Color(1f, 0.62f, 0.62f, 0.95f));
			hiddenTag.VerticalAlignment = VerticalAlignment.Center;
			headerRow.AddChild(hiddenTag);
		}

		left.AddChild(headerRow);
		if (!string.IsNullOrWhiteSpace(entry.Author))
		{
			Label author = CreateLabel(entry.Author, 12, new Color(0.72f, 0.76f, 0.84f, 0.85f));
			left.AddChild(author);
		}

		// 占位把简介推到左下角
		Control leftSpacer = new()
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		left.AddChild(leftSpacer);

		string summaryText = BuildConfigSummaryText(entry.Code);
		if (!string.IsNullOrEmpty(summaryText))
		{
			Label summaryLabel = CreateLabel(summaryText, 12, new Color(0.85f, 0.88f, 0.94f, 0.92f));
			summaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			summaryLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			summaryLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			left.AddChild(summaryLabel);
		}

		VBoxContainer actions = new()
		{
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		actions.AddThemeConstantOverride("separation", 6);
		row.AddChild(actions);

		bool hasSteam = !string.IsNullOrEmpty(mySteamId);
		if (entry.IsCommunity && entry.Id != null && !entry.IsMine)
		{
			Button like = CreateActionButton($"♥ {Math.Max(0, entry.Likes)}", () => { }, compactLayout);
			like.Disabled = !hasSteam;
			like.Pressed += async () =>
			{
				bool on = !SessionLikedIds.Contains(entry.Id);
				HextechFeaturedConfigs.CommunityApiResult result = await HextechFeaturedConfigs.LikeAsync(mySteamId, entry.Id, on);
				Callable.From(() =>
				{
					if (!GodotObject.IsInstanceValid(like) || !result.Ok)
					{
						return;
					}

					if (on)
					{
						SessionLikedIds.Add(entry.Id);
					}
					else
					{
						SessionLikedIds.Remove(entry.Id);
					}

					SetActionButtonText(like, $"♥ {(result.Likes >= 0 ? result.Likes : entry.Likes)}");
				}).CallDeferred();
			};
			actions.AddChild(like);
		}

		Button apply = CreateActionButton(L("HEXTECH_CONFIG_FEATURED_APPLY"), () =>
		{
			HextechConfigShareCodec.ImportPreview? preview = HextechConfigShareCodec.TryParse(entry.Code);
			if (preview != null)
			{
				applyPreview(preview);
			}

			blocker.QueueFree();
		}, compactLayout);
		actions.AddChild(apply);

		if (entry.IsCommunity && entry.Id != null && entry.IsMine && hasSteam)
		{
			Button remove = CreateActionButton(L("HEXTECH_COMMUNITY_DELETE"), () => { }, compactLayout);
			remove.Pressed += async () =>
			{
				remove.Disabled = true;
				await HextechFeaturedConfigs.DeleteAsync(mySteamId, entry.Id);
				Callable.From(reloadTab).CallDeferred();
			};
			actions.AddChild(remove);
		}
		else if (entry.IsCommunity && entry.Id != null)
		{
			Button report = CreateActionButton(L("HEXTECH_COMMUNITY_REPORT"), () => { }, compactLayout);
			report.Disabled = !hasSteam;
			report.Pressed += async () =>
			{
				// 乐观 UI:点击立即置灰改字(Pressed 在主线程),网络结果不影响展示。
				report.Disabled = true;
				SetActionButtonText(report, L("HEXTECH_COMMUNITY_REPORTED"));
				await HextechFeaturedConfigs.ReportAsync(mySteamId, entry.Id);
			};
			actions.AddChild(report);
		}

		return card;
	}

	private static void OpenCommunityUploadDialog(
		Control blocker,
		string mySteamId,
		Func<string> buildPendingCode,
		Action reloadTab,
		bool compactLayout)
	{
		Control dialogBlocker = new()
		{
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dialogBlocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ColorRect dim = new()
		{
			Color = new Color(0f, 0f, 0f, 0.5f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		dialogBlocker.AddChild(dim);

		PanelContainer panel = new()
		{
			CustomMinimumSize = new Vector2(compactLayout ? 380f : 460f, 0f)
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;
		dialogBlocker.AddChild(panel);

		MarginContainer margin = new();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer body = new();
		body.AddThemeConstantOverride("separation", 10);
		margin.AddChild(body);

		Label title = CreateLabel(L("HEXTECH_COMMUNITY_UPLOAD"), 16, new Color(0.95f, 0.87f, 0.62f, 1f));
		title.HorizontalAlignment = HorizontalAlignment.Center;
		body.AddChild(title);

		Label hint = CreateLabel(L("HEXTECH_COMMUNITY_UPLOAD_HINT"), 12, new Color(0.78f, 0.82f, 0.9f, 0.85f));
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(hint);

		LineEdit titleInput = new()
		{
			PlaceholderText = L("HEXTECH_COMMUNITY_TITLE_PLACEHOLDER"),
			MaxLength = 30
		};
		body.AddChild(titleInput);

		Label feedback = CreateLabel(" ", 12, new Color(1f, 0.6f, 0.6f, 0.95f));
		feedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(feedback);

		HBoxContainer buttons = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttons.AddThemeConstantOverride("separation", 12);
		Button confirm = CreateActionButton(L("HEXTECH_COMMUNITY_UPLOAD_CONFIRM"), () => { }, compactLayout);
		confirm.Pressed += async () =>
		{
			string uploadTitle = titleInput.Text.Trim();
			if (uploadTitle.Length == 0)
			{
				feedback.Text = L("HEXTECH_COMMUNITY_TITLE_EMPTY");
				return;
			}

			confirm.Disabled = true;
			HextechFeaturedConfigs.CommunityApiResult result = await HextechFeaturedConfigs.UploadAsync(
				mySteamId,
				HextechSteamIdentity.GetPersonaName(),
				uploadTitle,
				buildPendingCode());
			Callable.From(() =>
			{
				if (!GodotObject.IsInstanceValid(dialogBlocker))
				{
					return;
				}

				if (result.Ok)
				{
					dialogBlocker.QueueFree();
					reloadTab();
					return;
				}

				confirm.Disabled = false;
				feedback.Text = result.Error switch
				{
					"title_rejected" => L("HEXTECH_COMMUNITY_ERR_TITLE_REJECTED"),
					"quota_exceeded" => L("HEXTECH_COMMUNITY_ERR_QUOTA"),
					"too_frequent" or "daily_limit" => L("HEXTECH_COMMUNITY_ERR_RATE"),
					"banned" => L("HEXTECH_COMMUNITY_ERR_BANNED"),
					_ => L("HEXTECH_CONFIG_FEATURED_ERROR")
				};
			}).CallDeferred();
		};
		Button cancel = CreateActionButton(L("HEXTECH_CONFIG_CANCEL"), () => dialogBlocker.QueueFree(), compactLayout);
		buttons.AddChild(confirm);
		buttons.AddChild(cancel);
		body.AddChild(buttons);

		blocker.AddChild(dialogBlocker);
		HextechControllerOverlay.RegisterModal(dialogBlocker, titleInput);
		titleInput.GrabFocus();
	}

	/// <summary>三行本地化摘要：我方海克斯 启用/总数、敌方海克斯 启用/总数、双方每幕数量。</summary>
	private static string BuildConfigSummaryText(string code)
	{
		HextechConfigShareCodec.ImportPreview? preview = HextechConfigShareCodec.TryParse(code);
		if (preview == null)
		{
			return string.Empty;
		}

		HextechRunConfigurationSnapshot snapshot = preview.Snapshot;
		int playerTotal = HextechCatalog.GetAllConfigurableRuneTypes().Count();
		int enemyTotal = HextechContentRegistry.SilverMonsterHexes.Count
			+ HextechContentRegistry.GoldMonsterHexes.Count
			+ HextechContentRegistry.PrismaticMonsterHexes.Count;
		int playerEnabled = Math.Max(0, playerTotal - snapshot.DisabledPlayerRuneIds.Count);
		int enemyEnabled = Math.Max(0, enemyTotal - snapshot.DisabledMonsterHexIds.Count);
		return string.Format(
			L("HEXTECH_COMMUNITY_SUMMARY"),
			playerEnabled,
			playerTotal,
			enemyEnabled,
			enemyTotal,
			string.Join("-", snapshot.PlayerHexCountsByAct),
			string.Join("-", snapshot.EnemyHexCountsByAct));
	}
}
