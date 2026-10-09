using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnrealFrontiersLauncher;

public class AutoupdatePageViewModel : INotifyPropertyChanged
{
    public float ProgressValue
    {
        get => progressValue;
        private set
        {
            progressValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShouldDisplayDownloadProgress));
        }
    }
    private float progressValue;

    public string StatusText
    {
        get => statusText;
        set
        {
            statusText = value;
            OnPropertyChanged();
        }
    }
    private string statusText = "";

    public bool ShouldDisplayDownloadProgress
    {
        get => progressValue > 0;
        set => OnPropertyChanged(nameof(ProgressValue));
    }

    public AutoupdatePageViewModel()
    {
        Autoupdater.OnProgress += (progress) =>
        {
            ProgressValue = progress;
        };
        Autoupdater.OnSetStatus += (status) =>
        {
            StatusText = status;
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}