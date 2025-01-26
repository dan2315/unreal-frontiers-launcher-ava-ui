using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using UnrealFrontiersLauncher;

public class NavigationService
{
    private static MainWindow _mainWindow;

    Stack<object> pagesStack = new();
    public static Dictionary<Type, UserControl> Pages => _pages;
    private static Dictionary<Type, UserControl> _pages = new();
    private IPageTransition _transition;
    
    private void SetWindowContent(object value)
    {
        _mainWindow.ContentControl.Content = value;
    }
    public NavigationService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _transition = new PageSlide()
        {
            Duration = TimeSpan.FromMilliseconds(500),
            Orientation = PageSlide.SlideAxis.Vertical,
            SlideInEasing = new BounceEaseIn(),
            SlideOutEasing = new BounceEaseIn(),
        };
    }

    public void RegisterPage<T>(T page) where T : UserControl
    {
        _pages.Add(typeof(T), page);
    }
    
    public void NavigateTo(object nextPage)
    {
        // Animate
        if (pagesStack.Count > 0)
        {
            _transition.Start(pagesStack.Peek() as Visual, nextPage as Visual, true, CancellationToken.None);
        }
        pagesStack.Push(nextPage);
        SetWindowContent(nextPage);
    }

    public void NavigateTo<T>() where T : UserControl 
    {
        NavigateTo(Pages[typeof(T)]);
    }

    public void GetBack()
    {
        var currentPage = pagesStack.Pop();
        if (pagesStack.Count > 0) 
        {
            var previousPage = pagesStack.Peek();
            
            _transition.Start(currentPage as Visual, previousPage as Visual, true, CancellationToken.None);
            
            SetWindowContent(previousPage);
        }
        else
        {
            _mainWindow.Close();
        }
    }
}