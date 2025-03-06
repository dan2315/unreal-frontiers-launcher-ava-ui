using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DynamicData;

namespace UnrealFrontiersLauncher;

public partial class AutoupdatePage : UserControl
{
    NavigationService _navigationService;
    public AutoupdatePage(NavigationService navigationService)
    {
        _navigationService = navigationService;
        DataContext = new AutoupdatePageViewModel();        
        InitializeComponent();
    }

    public void TryAutoupdateOrOtherwise(Action? updateIsNotRequiredFallback = null)
    {
        Autoupdater.AutopdateOrOtherwise(updateIsNotRequiredFallback);
    }
    
    public async void AnimateOpacity(StyledElement target)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            Easing = new SineEaseInOut(),
            IterationCount = IterationCount.Parse("10")
        };
        
        // Add keyframes
        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(0),
            Setters = { new Setter(OpacityProperty, 0) }
        });
        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(1),
            Setters = { new Setter(OpacityProperty, 1) }
        });

        
        
        // Run the animation
        await animation.RunAsync(target);
    }
}