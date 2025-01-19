using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace UnrealFrontiersLauncher;

public class Modpack : INotifyPropertyChanged
{
    public string Name { get; set; }
    public Bitmap Icon { get; set; }
    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Icon = new Bitmap(AssetLoader.Open(new Uri($"avares://UnrealFrontiersLauncher/Assets/Images/ModpackIcons/{_value}_icon.png")));
                OnPropertyChanged(nameof(Icon));
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
            }
        }
    }
    public IBrush BackgroundColor => !IsEnabled ?  new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)) : IsSelected ? 
    new SolidColorBrush(Color.FromRgb(0x6D, 0x20, 0x8F)) : 
    new SolidColorBrush(Color.Parse("Transparent"));

    private bool _isHovered;
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                OnPropertyChanged(nameof(IsHovered));
                OnPropertyChanged(nameof(TextColor));

            }
        }
    }

    public IBrush TextColor
    {
        get
        {
            if (IsSelected || IsHovered)
            {
                return new SolidColorBrush(Color.Parse("#ffffff"));
            }
            return new SolidColorBrush(Color.Parse("#606060"));
        }
    }

    private bool _isLastItem;

    public bool IsLastItem
    {
        get => !_isLastItem;
        set
        {
            if (_isLastItem != value)
            {
                _isLastItem = value;
                OnPropertyChanged(nameof(IsLastItem));
            }
        }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                 _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}