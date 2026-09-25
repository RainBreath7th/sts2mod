using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private static readonly Color ConfirmTextColor = new(0.99f, 0.93f, 0.78f, 1f);
	private static readonly Color ConfirmTextDisabledColor = new(0.62f, 0.65f, 0.7f, 0.75f);

	/// <summary>
	/// 选择界面底部的文字按钮(确认/取消),主按钮外观与重随/移除按钮的铜金色面板一致,次按钮为同形的钢灰色。
	/// 文字不用 Button 自带的 Text:Button 走主题默认字体,中日韩等语言会落到系统回退字体而发虚;
	/// 改由居中的 MegaLabel 绘制,它在 _Ready 时按当前语言替换字体,与界面其他文字同一套渲染。
	/// </summary>
	private static Button CreateConfirmButton(string name, Vector2 size, out MegaLabel label, bool secondary = false)
	{
		Button button = new()
		{
			Name = name,
			Text = string.Empty,
			CustomMinimumSize = size,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		if (secondary)
		{
			button.AddThemeStyleboxOverride("normal", CreateConfirmStyle(new Color(0.12f, 0.14f, 0.18f, 0.96f), new Color(0.5f, 0.56f, 0.64f, 0.9f), 6));
			button.AddThemeStyleboxOverride("hover", CreateConfirmStyle(new Color(0.17f, 0.2f, 0.25f, 0.98f), new Color(0.72f, 0.78f, 0.86f, 1f), 10));
			button.AddThemeStyleboxOverride("pressed", CreateConfirmStyle(new Color(0.08f, 0.09f, 0.12f, 0.98f), new Color(0.6f, 0.66f, 0.74f, 1f), 2));
			button.AddThemeStyleboxOverride("hover_pressed", CreateConfirmStyle(new Color(0.08f, 0.09f, 0.12f, 0.98f), new Color(0.6f, 0.66f, 0.74f, 1f), 2));
		}
		else
		{
			button.AddThemeStyleboxOverride("normal", CreateConfirmStyle(new Color(0.2f, 0.15f, 0.08f, 0.96f), new Color(0.78f, 0.62f, 0.34f, 1f), 6));
			button.AddThemeStyleboxOverride("hover", CreateConfirmStyle(new Color(0.28f, 0.21f, 0.1f, 0.98f), new Color(0.98f, 0.82f, 0.46f, 1f), 10));
			button.AddThemeStyleboxOverride("pressed", CreateConfirmStyle(new Color(0.13f, 0.1f, 0.05f, 0.98f), new Color(0.9f, 0.72f, 0.4f, 1f), 2));
			button.AddThemeStyleboxOverride("hover_pressed", CreateConfirmStyle(new Color(0.13f, 0.1f, 0.05f, 0.98f), new Color(0.9f, 0.72f, 0.4f, 1f), 2));
		}
		button.AddThemeStyleboxOverride("disabled", CreateConfirmStyle(new Color(0.1f, 0.11f, 0.14f, 0.88f), new Color(0.36f, 0.4f, 0.47f, 0.7f), 0));
		button.AddThemeStyleboxOverride("focus", HextechControllerInput.CreateFocusRing());

		MegaLabel text = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			MaxFontSize = 26,
			MinFontSize = 16
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(text);
		text.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.55f));
		text.AddThemeConstantOverride("shadow_offset_x", 0);
		text.AddThemeConstantOverride("shadow_offset_y", 2);
		text.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		text.OffsetLeft = 18f;
		text.OffsetRight = -18f;
		text.OffsetTop = 6f;
		text.OffsetBottom = -6f;
		button.AddChild(text);

		// 切换 Disabled 会让按钮重绘,借此同步文字颜色,调用方只需改 Disabled。
		button.Draw += () => text.Modulate = button.Disabled ? ConfirmTextDisabledColor : ConfirmTextColor;
		text.Modulate = ConfirmTextColor;
		label = text;
		return button;
	}

	private static StyleBoxFlat CreateConfirmStyle(Color background, Color border, int shadowSize)
	{
		StyleBoxFlat style = new()
		{
			BgColor = background,
			BorderColor = border,
			ShadowColor = new Color(0f, 0f, 0f, 0.45f),
			ShadowSize = shadowSize,
			ShadowOffset = new Vector2(0f, 2f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		return style;
	}
}
