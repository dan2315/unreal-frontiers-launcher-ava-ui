using System;
using System.Collections.Generic;
using Avalonia.Controls;
using UnrealFrontiersLauncher;

public class NavigationService
{
    private static MainWindow _mainWindow;

    Stack<object> pagesStack = new();
    public static Dictionary<Type, UserControl> Pages => _pages;
    private static Dictionary<Type, UserControl> _pages = new();

    private void SetWindowContent(object  value)
    {
        _mainWindow.ContentControl.Content =value;
    }
    public NavigationService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void RegisterPage<T>(T page) where T : UserControl
    {
        _pages.Add(typeof(T), page);
    }

    public void NavigateTo(object nextPage)
    {
        pagesStack.Push(nextPage);
        SetWindowContent(nextPage);
    }

    public void NavigateTo<T>() where T : UserControl 
    {
        NavigateTo(Pages[typeof(T)]);
    }

    public void GetBack()
    {
        pagesStack.Pop();
        if (pagesStack.Count > 0) 
        {
            var upperElement = pagesStack.Peek();
            SetWindowContent(upperElement);
        }
        else
        {
            _mainWindow.Close();
        }
    }
}