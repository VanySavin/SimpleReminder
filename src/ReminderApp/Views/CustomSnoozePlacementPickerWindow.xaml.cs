using System.Windows;
using System.Windows.Input;
using ReminderApp.Helpers;

namespace ReminderApp.Views;

public partial class CustomSnoozePlacementPickerWindow : Window
{
    private const double EdgeMargin = 12;

    public CustomSnoozePlacementPickerWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public double SelectedLeft => Left;
    public double SelectedTop => Top;
    public string? SelectedScreenDeviceName { get; private set; }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        try
        {
            DragMove();
            EnsureVisiblePlacement();
        }
        catch
        {
            // ignore
        }
    }

    private void UseButton_OnClick(object sender, RoutedEventArgs e)
    {
        EnsureVisiblePlacement();
        try
        {
            SelectedScreenDeviceName = ScreenPlacementHelper.ResolveScreenDeviceNameByWindowRect(
                Left,
                Top,
                ResolveWindowWidth(),
                ResolveWindowHeight());
        }
        catch
        {
            SelectedScreenDeviceName = null;
        }

        DialogResult = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        EnsureVisiblePlacement();
    }

    private void EnsureVisiblePlacement()
    {
        var width = ResolveWindowWidth();
        var height = ResolveWindowHeight();
        var left = Left;
        var top = Top;

        if (!ScreenPlacementHelper.IsValidPoint(left, top) ||
            ScreenPlacementHelper.IsFullyOutsideAllScreens(left, top, width, height))
        {
            var primary = ScreenPlacementHelper.ResolvePlacementContext(null);
            left = primary.WorkAreaDip.Left + 16;
            top = primary.WorkAreaDip.Top + 48;
            SelectedScreenDeviceName = primary.DeviceName;
            Left = left;
            Top = top;
            return;
        }

        var targetScreenName = ScreenPlacementHelper.ResolveScreenDeviceNameByWindowRect(left, top, width, height);
        var targetContext = ScreenPlacementHelper.ResolvePlacementContext(targetScreenName, left, top);
        (left, top) = ScreenPlacementHelper.ClampToWorkArea(left, top, width, height, targetContext.WorkAreaDip, EdgeMargin);
        SelectedScreenDeviceName = targetContext.DeviceName;
        Left = left;
        Top = top;
    }

    private double ResolveWindowWidth() => ActualWidth > 0 ? ActualWidth : Math.Max(Width, MinWidth);

    private double ResolveWindowHeight() => ActualHeight > 0 ? ActualHeight : Math.Max(Height, MinHeight);
}
