// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace Files.App.Data.Items
{
	public sealed class FavoriteAppShortcut
	{
		public string Path { get; }

		public string Name { get; }

		public bool IsFolder { get; }

		public Symbol Icon => IsFolder ? Symbol.Folder : Symbol.AllApps;

		public FavoriteAppShortcut(string path, bool isFolder = false)
		{
			Path = path;
			IsFolder = isFolder;
			Name = isFolder
				? SystemIO.Path.GetFileName(SystemIO.Path.TrimEndingDirectorySeparator(path))
				: SystemIO.Path.GetFileNameWithoutExtension(path);

			if (string.IsNullOrEmpty(Name))
				Name = path;
		}
	}
}
