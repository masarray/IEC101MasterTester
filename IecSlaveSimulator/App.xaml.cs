using System;
using System.IO;
using System.Windows;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.Services;
using IecSlaveSimulator.ViewModels;
using IecSlaveSimulator.Views;

namespace IecSlaveSimulator
{
    public partial class App : Application
    {
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            SlaveConnectionSettingsStore settingsStore = new SlaveConnectionSettingsStore();
            MainViewModel viewModel = new MainViewModel();

            try
            {
                SlaveConnectionSettings settings = await settingsStore.LoadAsync();
                viewModel.ApplyConnectionSettings(settings);

                string projectPath = NormalizeDialogPath(viewModel.CurrentFilePath);
                if (File.Exists(projectPath))
                {
                    await viewModel.LoadProjectAsync(projectPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "IecSlaveSimulator", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            NucSlaveWindow window = new NucSlaveWindow(viewModel, settingsStore, null);
            MainWindow = window;
            window.Show();
            window.Activate();
        }

        private static string NormalizeDialogPath(string currentPath)
        {
            string path = string.IsNullOrWhiteSpace(currentPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IecSlaveSimulator", "slave-project.json")
                : currentPath;

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IecSlaveSimulator");
                path = Path.Combine(directory, Path.GetFileName(path));
            }

            Directory.CreateDirectory(directory);
            return path;
        }
    }
}
