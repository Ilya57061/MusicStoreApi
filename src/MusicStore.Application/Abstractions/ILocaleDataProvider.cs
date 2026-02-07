namespace MusicStore.Application.Abstractions;

public interface ILocaleDataProvider
{
    IReadOnlyList<LocaleInfo> GetSupportedLocales();
    LocaleData GetLocaleData(string locale);
}

public sealed record LocaleInfo(string Locale, string DisplayName);

public sealed record LocaleData(
    string FakerLocale,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> AlbumQualifiers,
    IReadOnlyList<string> AlbumNouns,
    IReadOnlyList<string> TitleAdjectives,
    IReadOnlyList<string> TitleNouns,
    IReadOnlyList<string> BandNameNouns);
