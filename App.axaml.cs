using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Babel.Deck.Services;
using Babel.Deck.ViewModels;
using Babel.Deck.Views;

namespace Babel.Deck;

public partial class App : Application
{
    private SessionWorkflowCoordinator? _sessionWorkflowCoordinator;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BabelDeck");
            var appLog = new AppLog(Path.Combine(appDataRoot, "logs", "babel-deck.log"));
            var store = new SessionSnapshotStore(Path.Combine(appDataRoot, "state", "current-session.json"), appLog);
            _sessionWorkflowCoordinator = new SessionWorkflowCoordinator(store, appLog);
            _sessionWorkflowCoordinator.Initialize();
            desktop.Exit += OnDesktopExit;

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_sessionWorkflowCoordinator),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _sessionWorkflowCoordinator?.SaveCurrentSession();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}