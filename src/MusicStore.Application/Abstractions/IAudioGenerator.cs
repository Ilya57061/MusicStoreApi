namespace MusicStore.Application.Abstractions;

public interface IAudioGenerator
{
    byte[] GeneratePreviewMp3(ulong seed, TimeSpan duration);
}
