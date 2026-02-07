namespace MusicStore.Domain.Entities;

public sealed record Song(
    long SequenceIndex,
    string Title,
    string Artist,
    string Album,
    string Genre,
    int Likes);
