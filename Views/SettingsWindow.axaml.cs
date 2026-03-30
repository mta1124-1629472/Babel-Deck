using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Babel.Player.ViewModels;

namespace Babel.Player.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.CancelCommand.Execute(null);
            }
        }

        private void OnApplyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.ApplyCommand.Execute(null);
            }
        }

        private void OnOKClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.OKCommand.Execute(null);
            }
        }
    }
}