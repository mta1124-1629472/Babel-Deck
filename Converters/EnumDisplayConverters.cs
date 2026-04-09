using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Babel.Player.Models;

namespace Babel.Player.Converters;

public sealed class ComputeProfileDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ComputeProfile.Cpu => "CPU",
        ComputeProfile.Gpu => "GPU",
        ComputeProfile.Cloud => "Cloud",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class GpuHostBackendDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        GpuHostBackend.ManagedVenv => "Managed local GPU",
        GpuHostBackend.DockerHost => "Docker GPU host",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DiarizationProviderDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => "Off",
        string providerId when string.IsNullOrWhiteSpace(providerId) => "Off",
        ProviderNames.NemoLocal => "NeMo",
        ProviderNames.WeSpeakerLocal => "WeSpeaker",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SpeakerIdToShortLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string speakerId || string.IsNullOrWhiteSpace(speakerId))
            return string.Empty;

        const string prefix = "spk_";
        if (!speakerId.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(speakerId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return string.Empty;
        }

        return $"S{index}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SpeakerIdToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush FallbackBrush = new(Color.Parse("#374151"));

    private static readonly IReadOnlyList<SolidColorBrush> Palette =
    [
        new(Color.Parse("#2563EB")),
        new(Color.Parse("#059669")),
        new(Color.Parse("#D97706")),
        new(Color.Parse("#DC2626")),
        new(Color.Parse("#7C3AED")),
        new(Color.Parse("#0891B2")),
        new(Color.Parse("#EA580C")),
        new(Color.Parse("#DB2777")),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string speakerId || string.IsNullOrWhiteSpace(speakerId))
            return FallbackBrush;

        const string prefix = "spk_";
        if (!speakerId.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(speakerId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return FallbackBrush;
        }

        return Palette[index % Palette.Count];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
