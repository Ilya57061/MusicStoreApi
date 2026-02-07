namespace MusicStore.Application.Abstractions;

public interface ICoverGenerator
{
    byte[] GenerateCoverPng(ulong seed, string title, string artist, bool isSingle);
}
