// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls
{
	public sealed partial class FavoriteAppsBar : UserControl
	{
		private readonly IGeneralSettingsService settings = Ioc.Default.GetRequiredService<IGeneralSettingsService>();
		private IShellPanesPage? paneHolder;
		private bool isSecondaryPane;

		[GeneratedDependencyProperty]
		public partial BaseShellPage? ShellPage { get; set; }

		public ObservableCollection<FavoriteAppShortcut> Shortcuts { get; } = [];

		public FavoriteAppsBar()
		{
			InitializeComponent();
		}

		private bool IsSecondaryPane => paneHolder?.GetPanes().Skip(1).Any(page => ReferenceEquals(page, ShellPage)) ?? false;

		private List<string> GetPaths(bool folders = false) =>
			(folders
				? IsSecondaryPane ? settings.RightPaneFavoriteFolders : settings.LeftPaneFavoriteFolders
				: IsSecondaryPane ? settings.RightPaneFavoriteApps : settings.LeftPaneFavoriteApps) ?? [];

		private void SavePaths(List<string> paths, bool folders = false)
		{
			if (folders)
			{
				if (IsSecondaryPane)
					settings.RightPaneFavoriteFolders = paths;
				else
					settings.LeftPaneFavoriteFolders = paths;
			}
			else if (IsSecondaryPane)
				settings.RightPaneFavoriteApps = paths;
			else
				settings.LeftPaneFavoriteApps = paths;
		}

		private void Bar_Loaded(object sender, RoutedEventArgs e)
		{
			settings.PropertyChanged += Settings_PropertyChanged;
			paneHolder = ShellPage?.PaneHolder;
			if (paneHolder is not null)
				paneHolder.PropertyChanged += PaneHolder_PropertyChanged;
			ReloadShortcuts();
		}

		private void Bar_Unloaded(object sender, RoutedEventArgs e)
		{
			settings.PropertyChanged -= Settings_PropertyChanged;
			if (paneHolder is not null)
				paneHolder.PropertyChanged -= PaneHolder_PropertyChanged;
			paneHolder = null;
		}

		private void PaneHolder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (isSecondaryPane != IsSecondaryPane)
				ReloadShortcuts();
		}

		private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(IGeneralSettingsService.LeftPaneFavoriteApps) or nameof(IGeneralSettingsService.RightPaneFavoriteApps)
				or nameof(IGeneralSettingsService.LeftPaneFavoriteFolders) or nameof(IGeneralSettingsService.RightPaneFavoriteFolders))
			{
				DispatcherQueue.TryEnqueue(() =>
				{
					if (IsLoaded)
						ReloadShortcuts();
				});
			}
		}

		private void ReloadShortcuts()
		{
			isSecondaryPane = IsSecondaryPane;
			Shortcuts.Clear();
			foreach (var path in GetPaths().Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
				Shortcuts.Add(new FavoriteAppShortcut(path));
			foreach (var path in GetPaths(folders: true).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
				Shortcuts.Add(new FavoriteAppShortcut(path, isFolder: true));
		}

		private void AddApp_Click(object sender, RoutedEventArgs e)
		{
			var dialogService = Ioc.Default.GetRequiredService<ICommonDialogService>();
			string[] filters = [Strings.FavoriteAppsFileFilter.GetLocalizedResource(), "*.exe;*.com;*.lnk;*.appref-ms"];
			if (!dialogService.Open_FileOpenDialog(MainWindow.Instance.WindowHandle, false, filters, Environment.SpecialFolder.Desktop, out var path))
				return;

			var paths = GetPaths();
			if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
				SavePaths([.. paths, path]);
		}

		private void AddFolder_Click(object sender, RoutedEventArgs e)
		{
			var dialogService = Ioc.Default.GetRequiredService<ICommonDialogService>();
			if (!dialogService.Open_FileOpenDialog(MainWindow.Instance.WindowHandle, true, [], Environment.SpecialFolder.Desktop, out var path))
				return;

			var paths = GetPaths(folders: true);
			if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
				SavePaths([.. paths, path], folders: true);
		}

		private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuFlyoutItem { Tag: FavoriteAppShortcut shortcut })
				SavePaths(GetPaths(shortcut.IsFolder).Where(item => !string.Equals(item, shortcut.Path, StringComparison.OrdinalIgnoreCase)).ToList(), shortcut.IsFolder);
		}

		private async void OpenShortcut_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button { Tag: FavoriteAppShortcut shortcut })
				return;

			try
			{
				if (shortcut.IsFolder)
				{
					if (ShellPage is not { } shellPage)
						return;

					shellPage.NavigateToPath(shortcut.Path);
					return;
				}

				if (await Win32Helper.InvokeWin32ComponentAsync(shortcut.Path, ShellPage))
					return;
			}
			catch (Exception ex)
			{
				App.Logger.LogWarning(ex, "Could not open favorite shortcut {Path}", shortcut.Path);
			}

			await DialogDisplayHelper.ShowDialogAsync(
				Strings.FavoriteAppLaunchErrorTitle.GetLocalizedResource(),
				Strings.FavoriteAppLaunchErrorMessage.GetLocalizedResource());
		}
	}
}
