using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Babel.Player.Models;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace Babel.Player.Services;

/// <summary>
/// Probes the local environment for required external tools (Python, ffmpeg).
/// Returns the working executable path, or null if the tool cannot be found.
/// </summary>
public static class DependencyLocator
{
    private const int ProbeTimeoutMs = 500;

    /// <summary>Returns a working Python executable path, or null if not found.</summary>
    public static string? FindPython()
    {
        var appDir = AppContext.BaseDirectory;
        
        // Check managed CPU runtime first (preferred for CPU-only operations)
        var managedCpuPython = ManagedRuntimeLayout.GetCpuPythonPath();
        if (ProbePythonCandidate(managedCpuPython, requirePip: true))
            return managedCpuPython;
        
        // Fall back to managed GPU runtime
        var managedGpuPython = ManagedRuntimeLayout.GetManagedPythonPath();
        if (ProbePythonCandidate(managedGpuPython, requirePip: true))
            return managedGpuPython;


        var candidates = new[]
        {
            Path.Combine(appDir, "python.exe"),
            Path.Combine(appDir, "python", "python.exe"),
            "python",
            "python3",
        };

        return ProbePython(candidates, requirePip: true)
            ?? ProbePython(candidates, requirePip: false);
    }

    /// <summary>Returns a working uv executable path, or null if not found.</summary>
    public static string? FindUv()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "uv.exe"),
            Path.Combine(appDir, "tools", "uv.exe"),
            Path.Combine(appDir, "tools", "win-x64", "uv.exe"),
            "uv",
        };
        return Probe(candidates, "--version");
    }

    /// <summary>Returns a working ffmpeg executable path, or null if not found.</summary>
    public static string? FindFfmpeg()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "ffmpeg.exe"),
            Path.Combine(appDir, "tools", "ffmpeg.exe"),
            "ffmpeg",
        };
        return Probe(candidates, "-version");
    }

    /// <summary>Returns a working piper executable path, or null if not found.</summary>
    public static string? FindPiper()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, $"{ProviderNames.Piper}.exe"),
            Path.Combine(appDir, ProviderNames.Piper, $"{ProviderNames.Piper}.exe"),
            ProviderNames.Piper,
        };
        return Probe(candidates, "--version");
    }

    /// <summary>Returns a working docker executable path, or null if not found.</summary>
    public static string? FindDocker()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "docker.exe"),
            Path.Combine(appDir, "tools", "docker.exe"),
            "docker",
        };
        return Probe(candidates, "--version");
    }

    private static string? Probe(string[] candidates, string versionArg)
    {
        foreach (var path in candidates)
        {
            var resolved = ResolveExecutable(path);
            if (resolved is null)
                continue;

            if (ProbeExecutable(resolved, versionArg))
                return resolved;
        }
        return null;
    }

    private static string? ProbePython(string[] candidates, bool requirePip)
    {
        foreach (var candidate in candidates)
        {
            var resolved = ResolveExecutable(candidate);
            if (resolved is null)
                continue;

            if (ProbePythonCandidate(resolved, requirePip))
                return resolved;
        }

        return null;
    }

    private static bool ProbePythonCandidate(string candidate, bool requirePip)
    {
        var resolved = ResolveExecutable(candidate);
        if (resolved is null || !ProbeExecutable(resolved, "--version"))
            return false;

        return !requirePip || ProbeExecutable(resolved, "-m pip --version");
    }

    private static bool ProbeExecutable(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;

            if (proc.WaitForExit(ProbeTimeoutMs) && proc.ExitCode == 0)
                return true;

            try { proc.Kill(); } catch { /* best-effort cleanup */ }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveExecutable(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        // Absolute or relative explicit path
        if (candidate.Contains(Path.DirectorySeparatorChar) ||
            candidate.Contains(Path.AltDirectorySeparatorChar) ||
            Path.IsPathRooted(candidate))
        {
            return File.Exists(candidate) ? candidate : null;
        }

        // Command name: resolve against PATH (and PATHEXT on Windows) before spawning.
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        var extensions = GetExecutableExtensions();
        var dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in dirs)
        {
            var trimmedDir = dir.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmedDir))
                continue;

            foreach (var ext in extensions)
            {
                var full = Path.Combine(trimmedDir, candidate + ext);
                if (File.Exists(full))
                    return full;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the list of executable filename extensions to consider when resolving command names on the current platform.
    /// </summary>
    /// <returns>
    /// An array of extensions to try when locating executables: on non-Windows platforms a single empty string; on Windows the trimmed entries from the PATHEXT environment variable with an empty string included, or the default {".exe", ".cmd", ".bat", ""} if PATHEXT is missing or empty.
    /// </returns>
    private static string[] GetExecutableExtensions()
    {
        if (!OperatingSystem.IsWindows())
            return [string.Empty];

        var pathext = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathext))
            return [".exe", ".cmd", ".bat", string.Empty];

        var parsed = pathext
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(ext => ext.Trim())
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .ToList();

        if (!parsed.Contains(string.Empty))
            parsed.Add(string.Empty);

        return [.. parsed];
    }

    /// <summary>
    /// Bootstraps the inference services, registries, and the session coordinator.
    /// Handles fallback creation if the primary initialization fails (e.g., due to corrupt state files).
    /// <summary>
    /// Constructs and initializes a SessionWorkflowCoordinator by wiring containerized and managed host managers, registries, and a session snapshot store rooted at the application's data directory.
    /// </summary>
    /// <param name="appDataRoot">Root directory for application data; the session snapshot is stored at {appDataRoot}/state/current-session.json.</param>
    /// <param name="startupLog">Optional log used to record startup errors encountered during initialization.</param>
    /// <param name="primaryGpuManager">Outputs the managed virtual-environment host manager selected as the primary GPU-capable host manager, or null if none was created.</param>
    /// <returns>The initialized SessionWorkflowCoordinator ready for use; if primary initialization fails, a coordinator constructed from a fallback (empty) session store is returned.</returns>
    public static SessionWorkflowCoordinator CreateSessionCoordinator(
        AppLog appLog,
        AppSettings appSettings,
        PerSessionSnapshotStore perSessionStore,
        RecentSessionsStore recentStore,
        ApiKeyStore apiKeyStore,
        IMediaTransportManager transportManager,
        string appDataRoot,
        AppLog? startupLog,
        out ManagedVenvHostManager? primaryGpuManager)
    {
        SessionWorkflowCoordinator? coordinator = null;
        primaryGpuManager = null;

        try
        {
            appLog.Info("App startup: initializing session coordinator.");
            var containerizedProbe = new ContainerizedServiceProbe(appLog);
            var managedHostManager = new ManagedVenvHostManager(appLog, containerizedProbe);
            primaryGpuManager = managedHostManager;
            var dockerHostManager = new ContainerizedInferenceManager(appLog, containerizedProbe);
            var containerizedManager = new CompositeInferenceHostManager(managedHostManager, dockerHostManager);
            
            var transcriptionRegistry = new TranscriptionRegistry(appLog, containerizedProbe);
            var translationRegistry = new TranslationRegistry(appLog, containerizedProbe);
            var ttsRegistry = new TtsRegistry(appLog, containerizedProbe);
            
            var store = new SessionSnapshotStore(Path.Combine(appDataRoot, "state", "current-session.json"), appLog);
            
            coordinator = new SessionWorkflowCoordinator(
                store, appLog, appSettings, perSessionStore, recentStore, 
                transcriptionRegistry, translationRegistry, ttsRegistry, 
                transportManager: transportManager, keyStore: apiKeyStore, 
                containerizedProbe: containerizedProbe, containerizedInferenceManager: containerizedManager);
            
            coordinator.Initialize();
            containerizedManager.RequestEnsureStarted(appSettings, ContainerizedStartupTrigger.AppStartup);
            appLog.Info("App startup: session coordinator ready.");
        }
        catch (Exception ex)
        {
            startupLog?.Error("App startup: session initialization failed. Continuing with empty session.", ex);
            
            // Fallback initialization
            var containerizedProbe = new ContainerizedServiceProbe(appLog);
            var managedHostManager = new ManagedVenvHostManager(appLog, containerizedProbe);
            primaryGpuManager = managedHostManager;
            var dockerHostManager = new ContainerizedInferenceManager(appLog, containerizedProbe);
            var containerizedManager = new CompositeInferenceHostManager(managedHostManager, dockerHostManager);
            
            var transcriptionRegistry = new TranscriptionRegistry(appLog, containerizedProbe);
            var translationRegistry = new TranslationRegistry(appLog, containerizedProbe);
            var ttsRegistry = new TtsRegistry(appLog, containerizedProbe);
            
            var fallbackStore = new SessionSnapshotStore(
                Path.Combine(appDataRoot, "state", "current-session.json"), appLog);
            
            coordinator = new SessionWorkflowCoordinator(
                fallbackStore, appLog, appSettings, perSessionStore, recentStore, 
                transcriptionRegistry, translationRegistry, ttsRegistry, 
                transportManager: transportManager, keyStore: apiKeyStore, 
                containerizedProbe: containerizedProbe, containerizedInferenceManager: containerizedManager);
            
            containerizedManager.RequestEnsureStarted(appSettings, ContainerizedStartupTrigger.AppStartup);
        }

        return coordinator;
    }
}
