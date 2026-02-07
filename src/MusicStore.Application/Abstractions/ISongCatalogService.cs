using MusicStore.Domain.Entities;

namespace MusicStore.Application.Abstractions;

public interface ISongCatalogService
{
    IReadOnlyList<Song> GetSongs(SongQuery query);
    SongDetails GetSongDetails(SongQuery query, long sequenceIndex);
    byte[] GetCoverPng(SongQuery query, long sequenceIndex);
    byte[] GetPreviewMp3(SongQuery query, long sequenceIndex);
    byte[] ExportMp3Zip(SongQuery query, int page, int pageSize);
}
