using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CmlLib.Core;
using UnrealFrontiersLauncher.ViewModels;
using UnrealFrontiersLauncher.Windows;

namespace UnrealFrontiersLauncher;

public partial class MainPage : UserControl
{
	private PopupService _popUpService;
	private InstanceManager _instanceManager;
	private GameLauncher _gameLauncher;
	private PlayerProfileProvider _playerProfileProvider;
	private MainPageViewModel _viewModel;
	public PlayerProfile PlayerProfile => _playerProfileProvider.GetProfile();

	public MainPage(PopupService popUpService, GameLauncher gameLauncher, InstanceManager instanceManager, PlayerProfileProvider playerProfileProvider)
	{
		_playerProfileProvider = playerProfileProvider;
		_popUpService = popUpService;
		_gameLauncher = gameLauncher;
		_instanceManager = instanceManager;
		_viewModel = new MainPageViewModel(this);
		DataContext = _viewModel;
		
		InitializeComponent();


		Loaded += (sender, eventArgs) => {
			InitSettingsPanel();
			UpdateSettingsPanel();
		};
	}

	public void InitSettingsPanel() 
	{
		RamSlider.ValueChanged += UpdateRamThroughSlider;
		RamEntry.TextChanged += UpdateRamThroughEntry;
	}
	public void UpdateSettingsPanel() 
	{
		var configuration = _instanceManager.selectedInstance.GetLaunchConfiguration();
		RamSlider.Maximum = SystemInfo.GetRamInfo().value / 1024 / 1024;
		RamSlider.Value = configuration.Ram;
		RamEntry.Text = configuration.Ram.ToString();
	}

	private void UpdateRamThroughSlider(object? sender, RangeBaseValueChangedEventArgs  args) 
	{
		RamEntry.Text = ((int)args.NewValue).ToString();
		_instanceManager.selectedInstance.ApplyNewRamValue((int)args.NewValue);
	}

	private void UpdateRamThroughEntry(object? sender, TextChangedEventArgs  args) 
	{
		var entry = sender as TextBox;
		if (entry == null) return;

		string newText = string.Concat(entry.Text.Where(char.IsDigit));
		newText = newText == "" ? "0" : newText;

		if (entry.Text != newText)
		{
			entry.Text = newText;
		}

		int value =int.Parse(newText);
		if (value > RamSlider.Maximum) 
		{
			value = (int)RamSlider.Maximum;
			entry.Text = value.ToString();
		}
		RamSlider.Value = value;
		_instanceManager.selectedInstance.ApplyNewRamValue(value);
	}

    private async void OnLaunchButtonClicked(object sender, RoutedEventArgs  e)
	{
		Console.WriteLine("LaunchGame");
		bool answer = await _popUpService.ShowYesNoDialogAsync("Launching...", "Вы намерены запустить майнкрафт?");
		if (!answer)
		{
			LaunchButton.Unlock();
			return;
		}
		_gameLauncher.LaunchMinecraft(_instanceManager.selectedInstance, PlayerProfile, OnDownloadProgress, OnDownloadFileChanged, OnDownloadSpeedChanged, LaunchButton.Unlock);
	}

    private void OnDownloadSpeedChanged(string speedText)
    {
        DownloadSpeed.Text = speedText;
    }
	
private async void OnInstanceSelected(object? sender, SelectionChangedEventArgs e)
{
    // Use index access to get the first item
    if (e.AddedItems.Count > 0 && e.AddedItems[0] is Modpack currentModpack)
    {
        if (!currentModpack.IsEnabled) return;

        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is Modpack previousModpack)
        {
            previousModpack.IsSelected = false;
        }

        currentModpack.IsSelected = true;
        string selectedValue = currentModpack.Value;
        Console.WriteLine($"Selected Server: {currentModpack.Name}");

        await _instanceManager.SelectInstance(selectedValue);
    }
    else
    {
        // If nothing is selected, reset to the first modpack
        // ModpackCollectionView.SelectedItem = _viewModel.Modpacks.FirstOrDefault();
    }
}

	private void OnDownloadProgress(float progress)
	{
		LaunchProgressBar.Value = progress * 100; 
		// DownloadProgress.Text = $"{progress * 100:F2} %";
		// if (progress == 1f)
		// {
		// 	DownloadProgress.Text = $"Downloaded!";
		// }
	}

	private void OnDownloadFileChanged(string file) 
	{
		TaskName.Text = file;
	}

	private void OnShowFolderClicked(object sender, EventArgs e) 
	{
		var installFolder = Path.Combine(AppSettings.LauncherBasePath, "Install");
		MinecraftPath minecraftPath = new MinecraftPath()
		{
			BasePath = GameLauncher.EnsurePathExists(_instanceManager.selectedInstance.GetResourcePath()),
			Resource = GameLauncher.EnsurePathExists(_instanceManager.selectedInstance.GetResourcePath()),
			Versions = GameLauncher.EnsurePathExists(Path.Combine(installFolder, "versions")),
			Library = GameLauncher.EnsurePathExists(Path.Combine(installFolder, "libraries")),
			Runtime = GameLauncher.EnsurePathExists(Path.Combine(installFolder, "runtime")),
			Assets = GameLauncher.EnsurePathExists(Path.Combine(installFolder, "assets"))
		};

		Process.Start(new ProcessStartInfo
        {
            FileName = _instanceManager.selectedInstance.GetResourcePath(),
            UseShellExecute = true,
            Verb = "open"
        });
	}

	public void Modpack_OnHovered() 
	{
		
	}

	public void Modpack_OnUnhovered() 
	{
		
	}
}
