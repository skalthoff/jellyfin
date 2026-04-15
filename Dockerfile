# Porcupine: multi-stage build for music-optimized Jellyfin server
# Produces a minimal runtime image with FFmpeg for audio transcoding

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Directory.Build.props Directory.Packages.props SharedVersion.cs BannedSymbols.txt stylecop.json ./
COPY Jellyfin.Server/Jellyfin.Server.csproj Jellyfin.Server/
COPY Jellyfin.Api/Jellyfin.Api.csproj Jellyfin.Api/
COPY Jellyfin.Data/Jellyfin.Data.csproj Jellyfin.Data/
COPY Jellyfin.Server.Implementations/Jellyfin.Server.Implementations.csproj Jellyfin.Server.Implementations/
COPY Emby.Server.Implementations/Emby.Server.Implementations.csproj Emby.Server.Implementations/
COPY Emby.Naming/Emby.Naming.csproj Emby.Naming/
COPY Emby.Photos/Emby.Photos.csproj Emby.Photos/
COPY MediaBrowser.Common/MediaBrowser.Common.csproj MediaBrowser.Common/
COPY MediaBrowser.Controller/MediaBrowser.Controller.csproj MediaBrowser.Controller/
COPY MediaBrowser.LocalMetadata/MediaBrowser.LocalMetadata.csproj MediaBrowser.LocalMetadata/
COPY MediaBrowser.MediaEncoding/MediaBrowser.MediaEncoding.csproj MediaBrowser.MediaEncoding/
COPY MediaBrowser.Model/MediaBrowser.Model.csproj MediaBrowser.Model/
COPY MediaBrowser.Providers/MediaBrowser.Providers.csproj MediaBrowser.Providers/
COPY MediaBrowser.XbmcMetadata/MediaBrowser.XbmcMetadata.csproj MediaBrowser.XbmcMetadata/
COPY src/Jellyfin.Extensions/Jellyfin.Extensions.csproj src/Jellyfin.Extensions/
COPY src/Jellyfin.Database/Jellyfin.Database.Implementations/Jellyfin.Database.Implementations.csproj src/Jellyfin.Database/Jellyfin.Database.Implementations/
COPY src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Jellyfin.Database.Providers.Sqlite.csproj src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/
COPY src/Jellyfin.Drawing/Jellyfin.Drawing.csproj src/Jellyfin.Drawing/
COPY src/Jellyfin.Drawing.Skia/Jellyfin.Drawing.Skia.csproj src/Jellyfin.Drawing.Skia/
COPY src/Jellyfin.LiveTv/Jellyfin.LiveTv.csproj src/Jellyfin.LiveTv/
COPY src/Jellyfin.MediaEncoding.Hls/Jellyfin.MediaEncoding.Hls.csproj src/Jellyfin.MediaEncoding.Hls/
COPY src/Jellyfin.MediaEncoding.Keyframes/Jellyfin.MediaEncoding.Keyframes.csproj src/Jellyfin.MediaEncoding.Keyframes/
COPY src/Jellyfin.Networking/Jellyfin.Networking.csproj src/Jellyfin.Networking/

RUN dotnet restore Jellyfin.Server/Jellyfin.Server.csproj

# Copy everything and build
COPY . .
RUN dotnet publish Jellyfin.Server/Jellyfin.Server.csproj \
    -c Release \
    --no-restore \
    -o /app \
    -p:DebugSymbols=false \
    -p:DebugType=none

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install FFmpeg for audio transcoding fallback
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*

# Install font packages for Skia (image processing)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libfontconfig1 \
        libfreetype6 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Jellyfin default ports
EXPOSE 8096 8920

# Data directories
VOLUME ["/config", "/cache", "/media"]

ENV JELLYFIN_DATA_DIR=/config \
    JELLYFIN_CACHE_DIR=/cache \
    JELLYFIN_CONFIG_DIR=/config/config \
    JELLYFIN_LOG_DIR=/config/log \
    JELLYFIN_WEB_DIR=/app/jellyfin-web \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "jellyfin.dll", \
    "--datadir", "/config", \
    "--cachedir", "/cache", \
    "--nowebclient"]
