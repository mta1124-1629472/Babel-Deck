using Avalonia;
using Babel.Player.Services;
using System;
using System.Threading.Tasks;

namespace Babel.Player;

sealed class Program
{
    // Avalonia requires [STAThread] for the UI path.
    // The benchmark path is headless and never touches Avalonia, but the
    // attribute is harmless on async Task entry points on .NET 10.
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        // Intercept --benchmark before Avalonia sees the args.
        // All other args are forwarded to Avalonia as normal.
        if (Array.Exists(args, a =>
                string.Equals(a, "--benchmark", StringComparison.OrdinalIgnoreCase)))
        {
            return await BenchmarkCli.RunAsync(args);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
#if DEBUG
            .LogToTrace();
#else
            ;
#endif
}
