using System;
using System.IO;

namespace Babel.Player.Services;

public static class ManagedRuntimeLayout
{
    private const string ManagedGpuRuntimeFolderName = "managed-gpu";
    private const string ManagedCpuRuntimeFolderName = "managed-cpu";

    public static string GetRuntimeRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BabelPlayer",
            "runtime",
            ManagedGpuRuntimeFolderName);

    public static string GetCpuRuntimeRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BabelPlayer",
            "runtime",
            ManagedCpuRuntimeFolderName);

    public static string GetVenvDirectory() => Path.Combine(GetRuntimeRoot(), ".venv");

    public static string GetCpuVenvDirectory() => Path.Combine(GetCpuRuntimeRoot(), ".venv");

    public static string GetManagedPythonPath() =>
        Path.Combine(GetVenvDirectory(), "Scripts", "python.exe");

    public static string GetCpuPythonPath() =>
        Path.Combine(GetCpuVenvDirectory(), "Scripts", "python.exe");

    public static string GetBootstrapMarkerPath() =>
        Path.Combine(GetRuntimeRoot(), ".bootstrap-version");

    public static string GetHostPidPath() =>
        Path.Combine(GetRuntimeRoot(), "managed-host.pid");
}
