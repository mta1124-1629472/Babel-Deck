using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Babel.Player.Models;
using Babel.Player.Services;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace BabelPlayer.Tests;

public sealed class FakeDiarizationProvider : IDiarizationProvider
{
    private readonly Func<DiarizationRequest, DiarizationResult> _resultFactory;

    public FakeDiarizationProvider(
        Func<DiarizationRequest, DiarizationResult>? resultFactory = null,
        ProviderReadiness? readiness = null)
    {
        _resultFactory = resultFactory ?? (_ => new DiarizationResult(true, [], 0, null));
        Readiness = readiness ?? new ProviderReadiness(true, null);
    }

    public ProviderReadiness Readiness { get; set; }

    public DiarizationRequest? LastRequest { get; private set; }

    public ProviderReadiness CheckReadiness(AppSettings settings, ApiKeyStore? keyStore) => Readiness;

    public Task<bool> EnsureReadyAsync(AppSettings settings, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(1.0);
        return Task.FromResult(Readiness.IsReady);
    }

    public Task<DiarizationResult> DiarizeAsync(DiarizationRequest request, CancellationToken ct = default)
    {
        LastRequest = request;
        return Task.FromResult(_resultFactory(request));
    }
}

public sealed class FakeDiarizationRegistry(
    params (string ProviderId, string DisplayName, IDiarizationProvider Provider)[] providers)
    : IDiarizationRegistry
{
    private readonly IReadOnlyDictionary<string, (ProviderDescriptor Descriptor, IDiarizationProvider Provider)> _providers =
        providers.ToDictionary(
            entry => entry.ProviderId,
            entry => (
                new ProviderDescriptor(
                    entry.ProviderId,
                    entry.DisplayName,
                    RequiresApiKey: false,
                    CredentialKey: null,
                    SupportedModels: [],
                    SupportedRuntimes: [InferenceRuntime.Containerized],
                    DefaultRuntime: InferenceRuntime.Containerized,
                    IsImplemented: true,
                    Notes: null),
                entry.Provider),
            StringComparer.Ordinal);

    public IReadOnlyList<ProviderDescriptor> GetAvailableProviders() =>
        [.. _providers.Values.Select(entry => entry.Descriptor)];

    public IDiarizationProvider CreateProvider(string providerId, AppSettings settings, ApiKeyStore? keyStore = null)
    {
        if (_providers.TryGetValue(providerId, out var entry))
            return entry.Provider;

        throw new PipelineProviderException($"Unknown diarization provider '{providerId}'.");
    }

    public ProviderReadiness CheckReadiness(string providerId, AppSettings settings, ApiKeyStore? keyStore)
    {
        if (_providers.TryGetValue(providerId, out var entry))
            return entry.Provider.CheckReadiness(settings, keyStore);

        return new ProviderReadiness(false, $"Unknown diarization provider '{providerId}'.");
    }
}
