﻿using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Reclaimer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Halo Map Files|*.map",
                Multiselect = true,
                CheckFileExists = true
            };

            if (ofd.ShowDialog() != true)
                return;

            await Task.Run(async () =>
            {
                foreach (var fileName in ofd.FileNames)
                {
                    if (!File.Exists(fileName))
                        continue;

                    await Storage.ImportCacheFile(fileName);
                }

                MessageBox.Show("all done");
            });

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            var tc = MainPanel.GetElementAtPath(Dock.Left) as Studio.Controls.UtilityTabControl;

            if (tc == null) tc = new Studio.Controls.UtilityTabControl();
            tc.Items.Add(new Controls.TagViewer());

            if (!MainPanel.GetChildren().Contains(tc))
                MainPanel.AddElement(tc, null, Dock.Left, new GridLength(400));
        }
    }
}
