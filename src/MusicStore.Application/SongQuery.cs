namespace MusicStore.Application;

public sealed record SongQuery(
    string Locale,
    ulong Seed,
    double LikesAverage,
    int Page,
    int PageSize);
