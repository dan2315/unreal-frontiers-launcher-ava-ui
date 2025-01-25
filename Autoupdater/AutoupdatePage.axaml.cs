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
    public AutoupdatePage()
    {
        InitializeComponent();

        var gradient =  new LinearGradientBrush();
        gradient.GradientStops.Add(new GradientStop() { Offset = 0, Color = Colors.Red });
        gradient.GradientStops.Add(new GradientStop() { Offset = 0.33, Color = Colors.Red });
        gradient.GradientStops.Add(new GradientStop() { Offset = 0.66, Color = Colors.Red });
        gradient.GradientStops.Add(new GradientStop() { Offset = 1, Color = Colors.Red });
        
        
        AnimatedText.Foreground = gradient;
        AnimateOpacity(AnimatedText);
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