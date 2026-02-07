FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/MusicStore.Api/MusicStore.Api.csproj", "src/MusicStore.Api/"]
COPY ["src/MusicStore.Application/MusicStore.Application.csproj", "src/MusicStore.Application/"]
COPY ["src/MusicStore.Domain/MusicStore.Domain.csproj", "src/MusicStore.Domain/"]
COPY ["src/MusicStore.Infrastructure/MusicStore.Infrastructure.csproj", "src/MusicStore.Infrastructure/"]

RUN dotnet restore "src/MusicStore.Api/MusicStore.Api.csproj"

COPY . .
WORKDIR "/src/src/MusicStore.Api"
RUN dotnet publish "MusicStore.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MusicStore.Api.dll"]
