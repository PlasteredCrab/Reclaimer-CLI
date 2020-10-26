﻿using Reclaimer.Utilities;
using Studio.Controls;
using System;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Reclaimer.Plugins
{
    public class MapViewerPlugin : Plugin
    {
        const string OpenKey = "MapViewer.OpenMap";
        const string OpenPath = "File\\Open Map";

        internal static MapViewerSettings Settings;

        public override string Name => "Map Viewer";

        public override void Initialise()
        {
            Settings = LoadSettings<MapViewerSettings>();
        }

        public override void Suspend()
        {
            SaveSettings(Settings);
        }

        public override IEnumerable<PluginMenuItem> GetMenuItems()
        {
            yield return new PluginMenuItem(OpenKey, OpenPath, OnMenuItemClick);
        }

        private void OnMenuItemClick(string key)
        {
            if (key != OpenKey) return;

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Halo Map Files|*.map;*.yelo",
                Multiselect = true,
                CheckFileExists = true
            };

            if (!string.IsNullOrEmpty(Settings.MapFolder))
                ofd.InitialDirectory = Settings.MapFolder;

            if (ofd.ShowDialog() != true)
                return;

            foreach (var fileName in ofd.FileNames)
                OpenPhysicalFile(fileName);
        }

        public override bool SupportsFileExtension(string extension)
        {
            return extension.ToLower() == "map" || extension.ToLower() == "yelo";
        }

        public override void OpenPhysicalFile(string fileName)
        {
            var tabId = $"{Key}::{fileName}";
            if (Substrate.ShowTabById(tabId))
                return;

            LogOutput($"Loading map file: {fileName}");

            try
            {
                var mv = new Controls.MapViewer();
                mv.TabModel.ContentId = tabId;
                mv.LoadMap(fileName);
                Substrate.AddTool(mv.TabModel, Substrate.GetHostWindow(), Dock.Left, new GridLength(400));
                Substrate.AddRecentFile(fileName);

                if (Settings.AutoMapFolder)
                    Settings.MapFolder = Path.GetDirectoryName(fileName);

                LogOutput($"Loaded map file: {fileName}");
            }
            catch (Exception ex)
            {
                Substrate.ShowErrorMessage($"Unable to load {Path.GetFileName(fileName)}:{Environment.NewLine}{ex.Message}");
            }
        }
    }

    internal class MapViewerSettings
    {
        [Editor(typeof(BrowseFolderEditor), typeof(PropertyValueEditor))]
        public string MapFolder { get; set; }

        public bool HierarchyView { get; set; }
        public bool AutoMapFolder { get; set; }
    }
}
