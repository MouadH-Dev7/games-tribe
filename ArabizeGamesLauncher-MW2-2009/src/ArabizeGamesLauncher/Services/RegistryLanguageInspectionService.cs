using System.Security;
using System.Text.Json;
using ArabizeGamesLauncher.Models;
using Microsoft.Win32;

namespace ArabizeGamesLauncher.Services;

public interface IRegistryLanguageReader
{
    RegistryLanguageReadResult ReadApprovedValues();
}

public sealed record RegistryLanguageReadResult(
    RegistryKeyReadStatus KeyStatus,
    RegistryValueInspection Locale,
    RegistryValueInspection LocaleAudio,
    string? Error = null);

/// <summary>Read-only adapter restricted to the LAZR key and its two approved values.</summary>
public sealed class WindowsRegistryLanguageReader : IRegistryLanguageReader
{
    private static readonly object MissingSentinel = new();

    public RegistryValueInspection ReadValue(string name)
    {
        EnsureApprovedName(name);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryLanguageScope.SubKey, writable: false);
            return key is null
                ? new(name, RegistryValueReadStatus.MissingKey, false, null, null, "The LAZR Registry key does not exist.")
                : ReadValue(key, name);
        }
        catch (UnauthorizedAccessException) { return new(name, RegistryValueReadStatus.AccessDenied, false, null, null, "Access to the LAZR Registry key was denied."); }
        catch (SecurityException) { return new(name, RegistryValueReadStatus.AccessDenied, false, null, null, "Access to the LAZR Registry key was denied."); }
        catch (Exception) { return new(name, RegistryValueReadStatus.UnexpectedError, false, null, null, "The approved Registry value could not be read."); }
    }

    public RegistryLanguageReadResult ReadApprovedValues()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryLanguageScope.SubKey, writable: false);
            if (key is null)
                return Failed(RegistryKeyReadStatus.MissingKey, RegistryValueReadStatus.MissingKey, "The LAZR Registry key does not exist.");

            // Do not enumerate values: query only the two names explicitly in scope.
            var locale = ReadValue(key, RegistryLanguageScope.LocaleName);
            var audio = ReadValue(key, RegistryLanguageScope.LocaleAudioName);
            return new(RegistryKeyReadStatus.Success, locale, audio);
        }
        catch (UnauthorizedAccessException) { return Failed(RegistryKeyReadStatus.AccessDenied, RegistryValueReadStatus.AccessDenied, "Access to the LAZR Registry key was denied."); }
        catch (SecurityException) { return Failed(RegistryKeyReadStatus.AccessDenied, RegistryValueReadStatus.AccessDenied, "Access to the LAZR Registry key was denied."); }
        catch (Exception) { return Failed(RegistryKeyReadStatus.UnexpectedError, RegistryValueReadStatus.UnexpectedError, "The LAZR Registry key could not be inspected."); }
    }

    private static RegistryValueInspection ReadValue(RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name, MissingSentinel, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (ReferenceEquals(value, MissingSentinel))
                return new(name, RegistryValueReadStatus.Missing, false, null, null);

            var kind = key.GetValueKind(name);
            var raw = value is null ? JsonSerializer.SerializeToElement<object?>(null) : JsonSerializer.SerializeToElement(value, value.GetType());
            var display = value is string text ? text : raw.GetRawText();
            var status = kind == RegistryValueKind.String && value is string
                ? RegistryValueReadStatus.PresentString
                : RegistryValueReadStatus.UnsupportedType;
            return new(name, status, true, display, kind.ToString(), RawData: raw);
        }
        catch (UnauthorizedAccessException) { return new(name, RegistryValueReadStatus.AccessDenied, false, null, null, "Access to this value was denied."); }
        catch (SecurityException) { return new(name, RegistryValueReadStatus.AccessDenied, false, null, null, "Access to this value was denied."); }
        catch (IOException) { return new(name, RegistryValueReadStatus.UnexpectedError, false, null, null, "The approved Registry value could not be read."); }
    }

    internal static void EnsureApprovedName(string name)
    {
        if (name is not (RegistryLanguageScope.LocaleName or RegistryLanguageScope.LocaleAudioName))
            throw new ArgumentOutOfRangeException(nameof(name), "Only LOCALE and LOCALE_AUDIO are in scope.");
    }

    private static RegistryLanguageReadResult Failed(RegistryKeyReadStatus keyStatus, RegistryValueReadStatus valueStatus, string message)
    {
        var detail = message;
        return new(keyStatus,
            new(RegistryLanguageScope.LocaleName, valueStatus, false, null, null, detail),
            new(RegistryLanguageScope.LocaleAudioName, valueStatus, false, null, null, detail), detail);
    }
}

public sealed class RegistryLanguageInspectionService(IRegistryLanguageReader reader)
{
    public RegistryLanguageInspection Inspect()
    {
        var read = reader.ReadApprovedValues();
        return new(read.KeyStatus, read.Locale, read.LocaleAudio, Classify(read), DateTimeOffset.UtcNow, read.Error);
    }

    public static RegistryLanguageClassification Classify(RegistryLanguageReadResult result)
    {
        if (result.KeyStatus is RegistryKeyReadStatus.AccessDenied or RegistryKeyReadStatus.UnexpectedError ||
            result.Locale.Status is RegistryValueReadStatus.AccessDenied or RegistryValueReadStatus.UnexpectedError ||
            result.LocaleAudio.Status is RegistryValueReadStatus.AccessDenied or RegistryValueReadStatus.UnexpectedError)
            return RegistryLanguageClassification.Unknown;
        if (result.KeyStatus == RegistryKeyReadStatus.MissingKey || !result.Locale.Exists || !result.LocaleAudio.Exists ||
            result.Locale.Status == RegistryValueReadStatus.Missing || result.LocaleAudio.Status == RegistryValueReadStatus.Missing)
            return RegistryLanguageClassification.Missing;
        if (result.Locale.Status == RegistryValueReadStatus.UnsupportedType || result.LocaleAudio.Status == RegistryValueReadStatus.UnsupportedType)
            return RegistryLanguageClassification.Unsupported;
        if (result.Locale.Status != RegistryValueReadStatus.PresentString || result.LocaleAudio.Status != RegistryValueReadStatus.PresentString)
            return RegistryLanguageClassification.Unknown;
        if (result.Locale.Data == RegistryLanguageScope.ArabicLocale && result.LocaleAudio.Data == RegistryLanguageScope.ArabicLocale)
            return RegistryLanguageClassification.ArabicPair;
        if (result.Locale.Data == RegistryLanguageScope.EnglishLocale && result.LocaleAudio.Data == RegistryLanguageScope.EnglishLocale)
            return RegistryLanguageClassification.EnglishPair;
        return RegistryLanguageClassification.Mixed;
    }
}
