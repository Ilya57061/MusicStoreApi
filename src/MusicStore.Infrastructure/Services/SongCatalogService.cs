using System.IO.Compression;
using System.Text;
using Bogus;
using MusicStore.Application;
using MusicStore.Application.Abstractions;
using MusicStore.Domain.Entities;
using MusicStore.Infrastructure.Utils;

namespace MusicStore.Infrastructure.Services;

public sealed class SongCatalogService : ISongCatalogService
{
    private readonly ILocaleDataProvider _locales;
    private readonly ICoverGenerator _covers;
    private readonly IAudioGenerator _audio;

    private const ulong CoverTag = 0x434F564552UL;
    private const ulong AudioTag = 0x415544494FUL;

    public SongCatalogService(ILocaleDataProvider locales, ICoverGenerator covers, IAudioGenerator audio)
    {
        _locales = locales;
        _covers = covers;
        _audio = audio;
    }

    public IReadOnlyList<Song> GetSongs(SongQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var data = _locales.GetLocaleData(query.Locale);
        var faker = new Faker(data.FakerLocale);

        var list = new List<Song>(pageSize);

        for (var i = 0; i < pageSize; i++)
        {
            var sequenceIndex = (long)((page - 1) * pageSize + i + 1);

            var coreSeed = GetCoreSeed(query.Seed, query.Locale, page, sequenceIndex);
            var coreRng = new Random(unchecked((int)(coreSeed ^ (coreSeed >> 32))));

            var title = GenerateTitle(data, coreRng);
            var artist = GenerateArtist(data, faker, coreRng);
            var isSingle = coreRng.NextDouble() < 0.24;
            var album = isSingle ? "Single" : GenerateAlbum(data, coreRng);
            var genre = data.Genres[coreRng.Next(0, data.Genres.Count)];

            var likes = GenerateLikes(query.Seed, query.Locale, page, sequenceIndex, query.LikesAverage);

            list.Add(new Song(sequenceIndex, title, artist, album, genre, likes));
        }

        return list;
    }

    public SongDetails GetSongDetails(SongQuery query, long sequenceIndex)
    {
        var page = Math.Max(1, query.Page);
        var localeData = _locales.GetLocaleData(query.Locale);
        var faker = new Faker(localeData.FakerLocale);

        var coreSeed = GetCoreSeed(query.Seed, query.Locale, page, sequenceIndex);
        var coreRng = new Random(unchecked((int)(coreSeed ^ (coreSeed >> 32))));

        var title = GenerateTitle(localeData, coreRng);
        var artist = GenerateArtist(localeData, faker, coreRng);
        var isSingle = coreRng.NextDouble() < 0.24;
        var album = isSingle ? "Single" : GenerateAlbum(localeData, coreRng);
        var genre = localeData.Genres[coreRng.Next(0, localeData.Genres.Count)];
        var likes = GenerateLikes(query.Seed, query.Locale, page, sequenceIndex, query.LikesAverage);

        var detailSeed = DeterministicHash.Mix(coreSeed, 0xD00D_F00D_BAAD_F00DUL);
        var review = GenerateReview(localeData, detailSeed);

        var baseUrl = "/api/songs/" + sequenceIndex;
        var coverUrl = $"{baseUrl}/cover?locale={Uri.EscapeDataString(query.Locale)}&seed={query.Seed}&page={page}";
        var previewUrl = $"{baseUrl}/preview.mp3?locale={Uri.EscapeDataString(query.Locale)}&seed={query.Seed}&page={page}";

        return new SongDetails(sequenceIndex, title, artist, album, genre, likes, review, coverUrl, previewUrl);
    }

    public byte[] GetCoverPng(SongQuery query, long sequenceIndex)
    {
        var page = Math.Max(1, query.Page);
        var localeData = _locales.GetLocaleData(query.Locale);
        var faker = new Faker(localeData.FakerLocale);

        var coreSeed = GetCoreSeed(query.Seed, query.Locale, page, sequenceIndex);
        var coreRng = new Random(unchecked((int)(coreSeed ^ (coreSeed >> 32))));

        var title = GenerateTitle(localeData, coreRng);
        var artist = GenerateArtist(localeData, faker, coreRng);
        var isSingle = coreRng.NextDouble() < 0.24;

        var coverSeed = DeterministicHash.Mix(coreSeed, CoverTag);

        return _covers.GenerateCoverPng(coverSeed, title, artist, isSingle);
    }

    public byte[] GetPreviewMp3(SongQuery query, long sequenceIndex)
    {
        var page = Math.Max(1, query.Page);
        var coreSeed = GetCoreSeed(query.Seed, query.Locale, page, sequenceIndex);

        var audioSeed = DeterministicHash.Mix(coreSeed, AudioTag);

        return _audio.GeneratePreviewMp3(audioSeed, TimeSpan.FromSeconds(8));
    }

    public byte[] ExportMp3Zip(SongQuery query, int page, int pageSize)
    {
        var q = query with { Page = page, PageSize = pageSize };
        var songs = GetSongs(q);

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var song in songs)
            {
                var mp3 = GetPreviewMp3(q, song.SequenceIndex);
                var fileName = SanitizeFileName($"{song.Title} - {song.Album} - {song.Artist}.mp3");

                var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(mp3, 0, mp3.Length);
            }
        }

        return ms.ToArray();
    }

    private static ulong GetCoreSeed(ulong userSeed, string locale, int page, long sequenceIndex)
    {
        var hash = DeterministicHash.ToUInt64(locale);
        var seed = DeterministicHash.Mix(userSeed, hash);
        seed = DeterministicHash.Mix(seed, (ulong)page);
        seed = DeterministicHash.Mix(seed, (ulong)sequenceIndex);

        return seed;
    }

    private static int GenerateLikes(ulong userSeed, string locale, int page, long sequenceIndex, double avgLikes)
    {
        avgLikes = Math.Clamp(avgLikes, 0.0, 10.0);

        if (Math.Abs(avgLikes - 10.0) < 0.00001) return 10;
        if (avgLikes <= 0.00001) return 0;

        var likeSeed = DeterministicHash.ToUInt64("likes", locale, userSeed.ToString(), page.ToString(), sequenceIndex.ToString());
        var rng = new Random(unchecked((int)(likeSeed ^ (likeSeed >> 32))));

        var baseLikes = (int)Math.Floor(avgLikes);
        var frac = avgLikes - baseLikes;

        var extra = rng.NextDouble() < frac ? 1 : 0;
        var likes = baseLikes + extra;

        return Math.Clamp(likes, 0, 10);
    }

    private static string GenerateTitle(LocaleData locale, Random rng)
    {
        var adj = locale.TitleAdjectives[rng.Next(locale.TitleAdjectives.Count)];
        var noun = locale.TitleNouns[rng.Next(locale.TitleNouns.Count)];

        if (rng.NextDouble() < 0.28)
        {
            var noun2 = locale.TitleNouns[rng.Next(locale.TitleNouns.Count)];

            return $"{adj} {noun} {noun2}";
        }

        return $"{adj} {noun}";
    }

    private static string GenerateAlbum(LocaleData locale, Random rng)
    {
        var q = locale.AlbumQualifiers[rng.Next(locale.AlbumQualifiers.Count)];
        var n = locale.AlbumNouns[rng.Next(locale.AlbumNouns.Count)];
        if (rng.NextDouble() < 0.25)
        {
            var n2 = locale.AlbumNouns[rng.Next(locale.AlbumNouns.Count)];

            return $"{q} {n} {n2}";
        }

        return $"{q} {n}";
    }

    private static string GenerateArtist(LocaleData locale, Faker faker, Random rng)
    {
        var asBand = rng.NextDouble() < 0.55;

        if (!asBand)
        {
            var name = faker.Name.FullName();

            return name;
        }

        var noun = locale.BandNameNouns[rng.Next(locale.BandNameNouns.Count)];
        if (rng.NextDouble() < 0.5)
        {
            return $"The {noun}";
        }

        var extra = locale.BandNameNouns[rng.Next(locale.BandNameNouns.Count)];
        {
            return $"{noun} {extra}";
        }
    }

    private static string GenerateReview(LocaleData locale, ulong seed)
    {
        var rng = new Random(unchecked((int)(seed ^ (seed >> 32))));

        string Phrase()
        {
            var a = locale.TitleAdjectives[rng.Next(locale.TitleAdjectives.Count)];
            var n = locale.TitleNouns[rng.Next(locale.TitleNouns.Count)];
            var g = locale.Genres[rng.Next(locale.Genres.Count)];

            return $"{a} {n} — {g}.";
        }

        var sb = new StringBuilder();
        sb.Append(Phrase());
        sb.Append(' ');
        sb.Append(Phrase());
        if (rng.NextDouble() < 0.6)
        {
            sb.Append(' ');
            sb.Append(Phrase());
        }

        return sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }

        return sb.ToString();
    }
}