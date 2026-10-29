using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CustomDesktop.Core.Native;
using CustomDesktop.Services;
using CustomDesktop.ViewModels;
using CustomDesktop.Views.Base;

namespace CustomDesktop.Views;

public partial class NotchWindow : NonActivatingWindow
{
    private readonly NotchViewModel _viewModel;
    private readonly DispatcherTimer _hoverDebounceTimer;
    private readonly User32.LowLevelMouseProc _mouseProc;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private int _animationSessionId = 0;
    private bool _isAnimating;

    public NotchWindow(NotchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _mouseProc = LowLevelMouseHookCallback;

        _hoverDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _hoverDebounceTimer.Tick += OnHoverDebounceTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseEnter += OnWindowMouseEnter;
        MouseMove += OnWindowMouseMove;
        MouseLeave += OnWindowMouseLeave;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.RequestExpand += OnRequestExpand;
        _viewModel.RequestCollapse += OnRequestCollapse;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Reposition();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hoverDebounceTimer.Stop();
        UninstallMouseHook();
        _viewModel.RequestExpand -= OnRequestExpand;
        _viewModel.RequestCollapse -= OnRequestCollapse;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.WidthCompact) ||
            e.PropertyName == nameof(NotchViewModel.HeightCompact) ||
            e.PropertyName == nameof(NotchViewModel.WidthExpanded) ||
            e.PropertyName == nameof(NotchViewModel.HeightExpanded) ||
            e.PropertyName == nameof(NotchViewModel.MarginFromScreen))
        {
            Dispatcher.InvokeAsync(Reposition);
        }
    }

    private void Reposition()
    {
        if (_isAnimating) return;

        double targetWidth = _viewModel.IsExpanded ? _viewModel.WidthExpanded : _viewModel.WidthCompact;
        double targetHeight = _viewModel.IsExpanded ? _viewModel.HeightExpanded : _viewModel.HeightCompact;
        double targetLeft = Math.Round((SystemParameters.PrimaryScreenWidth - targetWidth) / 2.0);
        double targetTop = _viewModel.MarginFromScreen;

        ApplyInstantDimensions(targetWidth, targetHeight, targetLeft, targetTop, _viewModel.IsExpanded);
    }

    private void OnRootGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsExpanded)
        {
            _viewModel.Expand();
        }
    }

    private void OnCollapseButtonClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Collapse();
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover)
        {
            _hoverDebounceTimer.Stop();
            if (!_viewModel.IsExpanded)
            {
                _viewModel.Expand();
            }
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover)
        {
            _hoverDebounceTimer.Stop();
            if (!_viewModel.IsExpanded)
            {
                _viewModel.Expand();
            }
        }
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover && _viewModel.IsExpanded)
        {
            _hoverDebounceTimer.Stop();
            _hoverDebounceTimer.Start();
        }
    }

    private void OnHoverDebounceTimerTick(object? sender, EventArgs e)
    {
        _hoverDebounceTimer.Stop();

        if (_viewModel.ExpandOnHover && _viewModel.IsExpanded)
        {
            if (User32.GetCursorPos(out var pt))
            {
                try
                {
                    var clientPt = PointFromScreen(new Point(pt.X, pt.Y));
                    bool isInside = clientPt.X >= 0 && clientPt.X <= ActualWidth &&
                                    clientPt.Y >= 0 && clientPt.Y <= ActualHeight;
                    if (!isInside)
                    {
                        _viewModel.Collapse();
                    }
                }
                catch
                {
                    _viewModel.Collapse();
                }
            }
            else if (!IsMouseOver)
            {
                _viewModel.Collapse();
            }
        }
    }

    private void OnRequestExpand()
    {
        ExpandSmooth();
    }

    private void OnRequestCollapse()
    {
        CollapseSmooth();
    }

    private void ExpandSmooth()
    {
        InstallMouseHook();

        int session = ++_animationSessionId;
        _isAnimating = true;

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double targetWidth = _viewModel.WidthExpanded;
        double targetHeight = _viewModel.HeightExpanded;
        double targetLeft = Math.Round((screenWidth - targetWidth) / 2.0);
        double targetTop = _viewModel.MarginFromScreen;

        if (!_viewModel.EnableAnimations)
        {
            ApplyInstantDimensions(targetWidth, targetHeight, targetLeft, targetTop, isExpanded: true);
            _isAnimating = false;
            return;
        }

        double startWidth = ActualWidth > 0 ? ActualWidth : Width;
        double startHeight = ActualHeight > 0 ? ActualHeight : Height;
        double startLeft = Left;

        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CircleEase { EasingMode = EasingMode.EaseOut };

        // 1. Prepare Content Visibility
        ExpandedContentGrid.Visibility = Visibility.Visible;
        CompactContentGrid.Visibility = Visibility.Visible;

        // 2. Simultaneous Width, Left, and Height Animations
        var widthAnim = new DoubleAnimation(startWidth, targetWidth, duration) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(startLeft, targetLeft, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(startHeight, targetHeight, duration) { EasingFunction = ease };

        heightAnim.Completed += (_, _) =>
        {
            if (session != _animationSessionId) return;

            _isAnimating = false;
            ApplyInstantDimensions(targetWidth, targetHeight, targetLeft, targetTop, isExpanded: true);
        };

        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(HeightProperty, heightAnim);

        // 3. Smooth Crossfade Content
        var fadeOutCompact = new DoubleAnimation(CompactContentGrid.Opacity, 0, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
        CompactContentGrid.BeginAnimation(OpacityProperty, fadeOutCompact);

        var fadeInExpanded = new DoubleAnimation(ExpandedContentGrid.Opacity, 1, TimeSpan.FromMilliseconds(190))
        {
            BeginTime = TimeSpan.FromMilliseconds(70),
            EasingFunction = ease
        };
        ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeInExpanded);
    }

    private void CollapseSmooth()
    {
        UninstallMouseHook();

        int session = ++_animationSessionId;
        _isAnimating = true;

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double targetWidth = _viewModel.WidthCompact;
        double targetHeight = _viewModel.HeightCompact;
        double targetLeft = Math.Round((screenWidth - targetWidth) / 2.0);
        double targetTop = _viewModel.MarginFromScreen;

        if (!_viewModel.EnableAnimations)
        {
            ApplyInstantDimensions(targetWidth, targetHeight, targetLeft, targetTop, isExpanded: false);
            _isAnimating = false;
            return;
        }

        double startWidth = ActualWidth > 0 ? ActualWidth : Width;
        double startHeight = ActualHeight > 0 ? ActualHeight : Height;
        double startLeft = Left;

        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CircleEase { EasingMode = EasingMode.EaseOut };

        // 1. Prepare Content Visibility
        CompactContentGrid.Visibility = Visibility.Visible;

        // 2. Simultaneous Width, Left, and Height Animations
        var widthAnim = new DoubleAnimation(startWidth, targetWidth, duration) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(startLeft, targetLeft, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(startHeight, targetHeight, duration) { EasingFunction = ease };

        heightAnim.Completed += (_, _) =>
        {
            if (session != _animationSessionId) return;

            _isAnimating = false;
            ApplyInstantDimensions(targetWidth, targetHeight, targetLeft, targetTop, isExpanded: false);
        };

        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(HeightProperty, heightAnim);

        // 3. Smooth Crossfade Content
        var fadeOutExpanded = new DoubleAnimation(ExpandedContentGrid.Opacity, 0, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
        ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeOutExpanded);

        var fadeInCompact = new DoubleAnimation(CompactContentGrid.Opacity, 1, TimeSpan.FromMilliseconds(170))
        {
            BeginTime = TimeSpan.FromMilliseconds(50),
            EasingFunction = ease
        };
        CompactContentGrid.BeginAnimation(OpacityProperty, fadeInCompact);
    }

    private void ApplyInstantDimensions(double targetWidth, double targetHeight, double targetLeft, double targetTop, bool isExpanded)
    {
        BeginAnimation(WidthProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(HeightProperty, null);

        CompactContentGrid.BeginAnimation(OpacityProperty, null);
        ExpandedContentGrid.BeginAnimation(OpacityProperty, null);

        Width = targetWidth;
        Left = targetLeft;
        Height = targetHeight;
        Top = targetTop;

        if (isExpanded)
        {
            CompactContentGrid.Visibility = Visibility.Collapsed;
            CompactContentGrid.Opacity = 0;
            ExpandedContentGrid.Visibility = Visibility.Visible;
            ExpandedContentGrid.Opacity = 1;
        }
        else
        {
            ExpandedContentGrid.Visibility = Visibility.Collapsed;
            ExpandedContentGrid.Opacity = 0;
            CompactContentGrid.Visibility = Visibility.Visible;
            CompactContentGrid.Opacity = 1;
        }
    }

    private void InstallMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero)
        {
            _mouseHookHandle = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);
        }
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr LowLevelMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == User32.WM_LBUTTONDOWN || msg == User32.WM_RBUTTONDOWN || msg == User32.WM_NCLBUTTONDOWN)
            {
                if (User32.GetCursorPos(out var pt))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (!_viewModel.IsExpanded) return;

                        try
                        {
                            var clientPt = PointFromScreen(new Point(pt.X, pt.Y));
                            bool isInside = clientPt.X >= 0 && clientPt.X <= ActualWidth &&
                                            clientPt.Y >= 0 && clientPt.Y <= ActualHeight;

                            if (!isInside)
                            {
                                _viewModel.Collapse();
                            }
                        }
                        catch
                        {
                            // In case of coordinate transform glitch during window close
                        }
                    });
                }
            }
        }

        return User32.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private void OnSubmitWifiPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WifiNetworkItem item)
        {
            DependencyObject? current = btn;
            while (current != null && current is not Border && current is not Grid)
            {
                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            string password = string.Empty;
            if (current != null)
            {
                var pBox = FindChild<PasswordBox>(current);
                if (pBox != null) password = pBox.Password;
            }

            _viewModel.SubmitWifiPasswordCommand.Execute(Tuple.Create(item, password));
        }
    }

    private void OnWifiPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is PasswordBox pBox && pBox.Tag is WifiNetworkItem item)
        {
            string password = pBox.Password ?? string.Empty;
            _viewModel.SubmitWifiPasswordCommand.Execute(Tuple.Create(item, password));
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is T match) return match;
                if (child is DependencyObject dep)
                {
                    var found = FindChild<T>(dep);
                    if (found != null) return found;
                }
            }
        }
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var found = FindChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
