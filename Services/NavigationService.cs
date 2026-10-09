using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using UnrealFrontiersLauncher;

public class NavigationService
{
    private static MainWindow _mainWindow = null!;

    private readonly Stack<UserControl> _pagesStack = new();
    private readonly PageSlide _transition;
    private CancellationTokenSource? _runningTransition;

    public static Dictionary<Type, UserControl> Pages => _pages;
    private static readonly Dictionary<Type, UserControl> _pages = new();

    private Panel Host => _mainWindow.PageHost;
    public object? CurrentContent => Host.Children.LastOrDefault();

    public NavigationService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _transition = new PageSlide
        {
            Duration = TimeSpan.FromMilliseconds(500),
            Orientation = PageSlide.SlideAxis.Vertical,
            SlideInEasing = new CubicEaseOut(),
            SlideOutEasing = new CubicEaseIn(),
        };
    }

    public void RegisterPage<T>(T page) where T : UserControl
    {
        _pages.Add(typeof(T), page);
    }

    public void NavigateTo(object nextPage)
    {
        if (nextPage is not UserControl next) return;

        FinishRunningTransition();
        var previous = Host.Children.LastOrDefault();
        if (ReferenceEquals(previous, next)) return;

        _pagesStack.Push(next);
        Host.Children.Add(next);

        if (previous != null)
        {
            _ = RunTransitionAsync(slidingPage: next, slideIn: true, pageToRemove: previous);
        }
    }

    public void NavigateTo<T>() where T : UserControl
    {
        NavigateTo(Pages[typeof(T)]);
    }

    public void GetBack()
    {
        FinishRunningTransition();
        var current = _pagesStack.Pop();

        if (_pagesStack.Count == 0)
        {
            _mainWindow.Close();
            return;
        }

        var previous = _pagesStack.Peek();
        if (!Host.Children.Contains(previous))
        {
            Host.Children.Insert(0, previous);
        }

        _ = RunTransitionAsync(slidingPage: current, slideIn: false, pageToRemove: current);
    }

    private async Task RunTransitionAsync(UserControl slidingPage, bool slideIn, Control pageToRemove)
    {
        var cts = new CancellationTokenSource();
        _runningTransition = cts;

        try
        {
            if (slideIn)
                await _transition.Start(null, slidingPage, true, cts.Token);
            else
                await _transition.Start(slidingPage, null, false, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_runningTransition, cts))
            {
                _runningTransition = null;
                RemoveFromHost(pageToRemove);
            }
            cts.Dispose();
        }
    }

    private void FinishRunningTransition()
    {
        if (_runningTransition == null) return;

        _runningTransition.Cancel();
        _runningTransition = null;

        while (Host.Children.Count > 1)
        {
            RemoveFromHost(Host.Children[0]);
        }
    }

    private void RemoveFromHost(Control page)
    {
        Host.Children.Remove(page);
        page.IsVisible = true;
        page.RenderTransform = null;
    }
}
