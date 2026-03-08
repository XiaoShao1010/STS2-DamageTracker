using Godot;

namespace DamageTracker;

public sealed partial class DamageTrackerOverlay : CanvasLayer
{
    private static DamageTrackerOverlay? _instance;

    private Control? _root;
    private Label? _statusLabel;
    private PanelContainer? _statusBadge;
    private RichTextLabel? _text;
    private bool _isDragging;
    private Vector2 _dragOffset;

    public static void EnsureCreated()
    {
        if (IsInstanceValid(_instance))
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            return;
        }

        _instance = new DamageTrackerOverlay();
        tree.Root.CallDeferred(Node.MethodName.AddChild, _instance);
    }

    public override void _EnterTree()
    {
        Layer = 100;
        Name = nameof(DamageTrackerOverlay);
        RunDamageTrackerService.Changed += OnTrackerChanged;
    }

    public override void _ExitTree()
    {
        RunDamageTrackerService.Changed -= OnTrackerChanged;
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        _root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Pass,
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 0,
            OffsetLeft = 24,
            OffsetTop = 24,
            OffsetRight = 388,
            OffsetBottom = 284,
            CustomMinimumSize = new Vector2(364, 260)
        };

        PanelContainer panel = new()
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            Modulate = Colors.White,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.GuiInput += OnPanelGuiInput;
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());

        MarginContainer margin = new();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        VBoxContainer stack = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 10);

        PanelContainer titleBar = new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 42)
        };
        titleBar.AddThemeStyleboxOverride("panel", CreateTitleBarStyle());

        MarginContainer titleMargin = new();
        titleMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        titleMargin.AddThemeConstantOverride("margin_left", 12);
        titleMargin.AddThemeConstantOverride("margin_top", 8);
        titleMargin.AddThemeConstantOverride("margin_right", 12);
        titleMargin.AddThemeConstantOverride("margin_bottom", 8);

        HBoxContainer titleRow = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        titleRow.AddThemeConstantOverride("separation", 8);

        Label title = new()
        {
            Text = "Damage Tracker",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color("F7FBFF"));

        _statusBadge = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(62, 26)
        };
        _statusBadge.AddThemeStyleboxOverride("panel", CreateStatusStyle(new Color("2F5E47"), new Color("7BE0A8")));

        _statusLabel = new Label
        {
            Text = "IDLE",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        _statusLabel.AddThemeColorOverride("font_color", new Color("E8FFF1"));
        _statusBadge.AddChild(_statusLabel);

        titleRow.AddChild(title);
        titleRow.AddChild(_statusBadge);
        titleMargin.AddChild(titleRow);
        titleBar.AddChild(titleMargin);

        Label hint = new()
        {
            Text = "Drag to move",
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color("90A5BC"));

        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = RunDamageTrackerService.BuildOverlayText()
        };
        _text.AddThemeFontSizeOverride("normal_font_size", 13);
        _text.AddThemeColorOverride("default_color", new Color("EAF2FF"));

        PanelContainer body = new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddThemeStyleboxOverride("panel", CreateBodyStyle());

        MarginContainer bodyMargin = new();
        bodyMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bodyMargin.AddThemeConstantOverride("margin_left", 12);
        bodyMargin.AddThemeConstantOverride("margin_top", 12);
        bodyMargin.AddThemeConstantOverride("margin_right", 12);
        bodyMargin.AddThemeConstantOverride("margin_bottom", 12);

        bodyMargin.AddChild(_text);
        body.AddChild(bodyMargin);

        stack.AddChild(titleBar);
        stack.AddChild(hint);
        stack.AddChild(body);
        margin.AddChild(stack);
        panel.AddChild(margin);
        _root.AddChild(panel);
        AddChild(_root);

        UpdateStatus(RunDamageTrackerService.BuildOverlayText());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_isDragging)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && !mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isDragging = false;
        }
    }

    private void OnTrackerChanged(string text)
    {
        if (!IsInsideTree())
        {
            return;
        }

        Callable.From(() => ApplyText(text)).CallDeferred();
    }

    private void ApplyText(string text)
    {
        if (_text != null)
        {
            _text.Text = text;
        }

        UpdateStatus(text);
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (_root == null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _dragOffset = GetViewport().GetMousePosition() - _root.Position;
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _isDragging = false;
                }
                break;

            case InputEventMouseMotion when _isDragging:
                UpdateRootPosition(GetViewport().GetMousePosition() - _dragOffset);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void UpdateRootPosition(Vector2 desiredPosition)
    {
        if (_root == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 panelSize = _root.Size;
        float maxX = Mathf.Max(0f, viewportSize.X - panelSize.X);
        float maxY = Mathf.Max(0f, viewportSize.Y - panelSize.Y);

        _root.Position = new Vector2(
            Mathf.Clamp(desiredPosition.X, 0f, maxX),
            Mathf.Clamp(desiredPosition.Y, 0f, maxY));
    }

    private void UpdateStatus(string text)
    {
        if (_statusLabel == null || _statusBadge == null)
        {
            return;
        }

        bool isLive = text.Contains("(live)", StringComparison.OrdinalIgnoreCase);
        _statusLabel.Text = isLive ? "LIVE" : "IDLE";
        _statusBadge.AddThemeStyleboxOverride(
            "panel",
            isLive
                ? CreateStatusStyle(new Color("2F5E47"), new Color("7BE0A8"))
                : CreateStatusStyle(new Color("4E4359"), new Color("C7B0FF")));
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("0F1725E8"),
            BorderColor = new Color("38506EFF"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomRight = 16,
            CornerRadiusBottomLeft = 16,
            ShadowColor = new Color("0000006A"),
            ShadowSize = 14,
            ExpandMarginLeft = 2,
            ExpandMarginTop = 2,
            ExpandMarginRight = 2,
            ExpandMarginBottom = 2
        };
    }

    private static StyleBoxFlat CreateTitleBarStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("182335FF"),
            BorderColor = new Color("5476A1FF"),
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
    }

    private static StyleBoxFlat CreateBodyStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("101927CC"),
            BorderColor = new Color("243548FF"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusBottomLeft = 12
        };
    }

    private static StyleBoxFlat CreateStatusStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomRight = 999,
            CornerRadiusBottomLeft = 999,
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
    }
}