using Avalonia.Controls;
using UnrealFrontiersLauncher.ViewModels;

namespace UnrealFrontiersLauncher;
public partial class MainWindow : Window 
{
    public object? CurrentPage
    {
        set
        {
            ContentControl.Content = value;
        }
        get => ContentControl.Content;
    }
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

}