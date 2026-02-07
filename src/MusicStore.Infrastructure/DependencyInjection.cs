using Microsoft.Extensions.DependencyInjection;
using MusicStore.Application.Abstractions;
using MusicStore.Infrastructure.Audio;
using MusicStore.Infrastructure.Covers;
using MusicStore.Infrastructure.Localization;
using MusicStore.Infrastructure.Services;

namespace MusicStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ILocaleDataProvider, JsonLocaleDataProvider>();
        services.AddSingleton<ICoverGenerator, ImageSharpCoverGenerator>();
        services.AddSingleton<ISongCatalogService, SongCatalogService>();
        services.AddSingleton<IAudioGenerator, ProceduralAudioGenerator>();

        return services;
    }
}
