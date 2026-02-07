using System.Text.Json;
using MusicStore.Application.Abstractions;

namespace MusicStore.Infrastructure.Localization;

public sealed class JsonLocaleDataProvider : ILocaleDataProvider
{
    private readonly IReadOnlyDictionary<string, (LocaleInfo Info, LocaleData Data)> _locales;

    public JsonLocaleDataProvider()
    {
        var baseDir = AppContext.BaseDirectory;
        var local = Path.Combine(baseDir, "Resources", "Locales");

        var files = new Dictionary<string, (LocaleInfo, LocaleData)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(local, "*.json", SearchOption.TopDirectoryOnly))
        {
            var json = File.ReadAllText(file);
            var dto = JsonSerializer.Deserialize<LocaleJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto is null || string.IsNullOrWhiteSpace(dto.Locale))
                continue;

            var info = new LocaleInfo(dto.Locale, dto.DisplayName ?? dto.Locale);
            var data = new LocaleData(
                dto.FakerLocale ?? "en",
                dto.Genres ?? Array.Empty<string>(),
                dto.AlbumQualifiers ?? Array.Empty<string>(),
                dto.AlbumNouns ?? Array.Empty<string>(),
                dto.TitleAdjectives ?? Array.Empty<string>(),
                dto.TitleNouns ?? Array.Empty<string>(),
                dto.BandNameNouns ?? Array.Empty<string>());

            files[dto.Locale] = (info, data);
        }

        if (files.Count == 0)
            throw new InvalidOperationException("No locale JSON files were found under Resources/Locales.");

        _locales = files;
    }

    public IReadOnlyList<LocaleInfo> GetSupportedLocales()
    {
      return _locales.Values.Select(x => x.Info).OrderBy(x => x.DisplayName).ToList();
    }

    public LocaleData GetLocaleData(string locale)
    {
        if (_locales.TryGetValue(locale, out var v))
            return v.Data;

        return _locales.TryGetValue("en-US", out var en) ? en.Data : _locales.Values.First().Data;
    }

    private sealed class LocaleJson
    {
        public string? Locale { get; init; }
        public string? DisplayName { get; init; }
        public string? FakerLocale { get; init; }
        public string[]? Genres { get; init; }
        public string[]? TitleAdjectives { get; init; }
        public string[]? TitleNouns { get; init; }
        public string[]? BandNameNouns { get; init; }
        public string[]? AlbumQualifiers { get; init; }
        public string[]? AlbumNouns { get; init; }
    }
}
