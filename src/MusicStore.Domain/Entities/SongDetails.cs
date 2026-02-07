namespace MusicStore.Domain.Entities;

public sealed record SongDetails(
    long SequenceIndex,
    string Title,
    string Artist,
    string Album,
    string Genre,
    int Likes,
    string Review,
    string CoverUrl,
    string PreviewUrl);
