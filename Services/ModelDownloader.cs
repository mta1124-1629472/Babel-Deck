using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Babel.Player.Services;

public sealed class ModelDownloader
{
    private readonly AppLog _log;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(30) };

    public ModelDownloader(AppLog log)
    {
        _log = log;
    }

    public async Task<bool> DownloadFasterWhisperAsync(string model)
    {
        string repoId = $"Systran/faster-whisper-{model}";
        return await DownloadHuggingFaceModelAsync(repoId);
    }

    public async Task<bool> DownloadNllbAsync(string model)
    {
        string repoId = $"facebook/{model}";
        return await DownloadHuggingFaceModelAsync(repoId);
    }

    private async Task<bool> DownloadHuggingFaceModelAsync(string repoId)
    {
        string? pythonPath = DependencyLocator.FindPython();
        if (pythonPath == null)
            throw new InvalidOperationException("Python required for downloading HuggingFace models.");

        string script = @"
import sys
import os

try:
    from huggingface_hub import snapshot_download
except ImportError:
    print('Installing huggingface_hub...')
    os.system(f'""{sys.executable}"" -m pip install huggingface_hub')
    from huggingface_hub import snapshot_download

repo_id = sys.argv[1]
print(f'Downloading {repo_id}...', flush=True)
try:
    snapshot_download(repo_id=repo_id)
    print('Download complete.', flush=True)
except Exception as e:
    print(f'Error downloading: {e}', file=sys.stderr)
    sys.exit(1)
";
        string scriptPath = Path.Combine(Path.GetTempPath(), $"hf_dl_{Guid.NewGuid():N}.py");
        await File.WriteAllTextAsync(scriptPath, script);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" \"{repoId}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            _log.Info($"Started model download for {repoId}. This may take a few minutes...");
            await proc.WaitForExitAsync();
            
            string stderr = await proc.StandardError.ReadToEndAsync();
            if (proc.ExitCode != 0)
            {
                 _log.Error($"Download failed for {repoId}: {stderr}", new Exception(stderr));
                 return false;
            }
            
            _log.Info($"Download succeeded for {repoId}.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Exception during model download: {ex.Message}", ex);
            return false;
        }
        finally
        {
             if (File.Exists(scriptPath)) File.Delete(scriptPath);
        }
    }

    public async Task<bool> DownloadPiperVoiceAsync(string voiceName, string? piperDir)
    {
        if (string.IsNullOrEmpty(piperDir))
        {
            _log.Error("PiperModelDir is not configured.", new InvalidOperationException("PiperModelDir is null"));
            return false;
        }

        Directory.CreateDirectory(piperDir);
        string onnxPath = Path.Combine(piperDir, $"{voiceName}.onnx");
        string jsonPath = Path.Combine(piperDir, $"{voiceName}.onnx.json");

        // Piper voice string format: {language}_{region}-{name}-{quality}
        // Ex: en_US-lessac-medium
        string[] parts = voiceName.Split('-');
        if (parts.Length < 3)
        {
            _log.Error($"Invalid piper voice name format: {voiceName}", new ArgumentException("Invalid format"));
            return false;
        }
        
        string langRegion = parts[0]; // en_US
        string language = langRegion.Split('_')[0]; // en
        string name = parts[1];
        string quality = parts[2];

        // We build the rhasspy huggingface repository path
        string baseUrl = $"https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/{language}/{langRegion}/{name}/{quality}/{voiceName}";

        _log.Info($"Downloading Piper voice {voiceName}...");

        try
        {
            bool onnxOk = await DownloadFileAsync($"{baseUrl}.onnx", onnxPath);
            bool jsonOk = await DownloadFileAsync($"{baseUrl}.onnx.json", jsonPath);

            if (onnxOk && jsonOk)
            {
                _log.Info($"Piper voice {voiceName} downloaded successfully.");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
             _log.Error($"Exception downloading Piper voice {voiceName}: {ex.Message}", ex);
             // Cleanup partial downloads
             if (File.Exists(onnxPath)) File.Delete(onnxPath);
             if (File.Exists(jsonPath)) File.Delete(jsonPath);
             return false;
        }
    }

    private async Task<bool> DownloadFileAsync(string url, string destinationPath)
    {
        string tmpPath = destinationPath + ".tmp";
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(fs);
            fs.Close();

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tmpPath, destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to download {url}: {ex.Message}", ex);
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            return false;
        }
    }
}
