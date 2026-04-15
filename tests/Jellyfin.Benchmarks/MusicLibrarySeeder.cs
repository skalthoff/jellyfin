using System;
using System.IO;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Benchmarks;

/// <summary>
/// Generates a realistic music library database for benchmarking.
/// Target: 500k tracks, 50k albums, 5k artists, 500 genres, 3 users.
/// </summary>
public static class MusicLibrarySeeder
{
    /// <summary>Artist count (default 5,000).</summary>
    public const int ArtistCount = 5_000;

    /// <summary>Albums per artist (default 10 → 50k total).</summary>
    public const int AlbumsPerArtist = 10;

    /// <summary>Tracks per album (default 10 → 500k total).</summary>
    public const int TracksPerAlbum = 10;

    /// <summary>Genre count.</summary>
    public const int GenreCount = 500;

    /// <summary>Simulated user count.</summary>
    public const int UserCount = 3;

    private static readonly Guid LibraryId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly string[] GenreNames = GenerateGenreNames(GenreCount);
    private static readonly Guid[] UserIds = GenerateUserIds();

    /// <summary>
    /// Creates and seeds a SQLite database file, returning a context factory.
    /// </summary>
    /// <param name="dbPath">Optional path. Temp file if null.</param>
    /// <returns>Factory to create db contexts.</returns>
    public static BenchmarkDbContextFactory CreateSeededDatabase(string? dbPath = null)
    {
        dbPath ??= Path.Combine(Path.GetTempPath(), $"porcupine-bench-{Guid.NewGuid():N}.db");

        var factory = new BenchmarkDbContextFactory(dbPath);
        Console.WriteLine($"Creating database at: {dbPath}");

        factory.EnsureCreated();

        SeedUsersAndLibrary(factory);
        SeedArtists(factory);
        SeedAlbumsTracksAndRelations(factory);
        OptimizeDb(factory);

        Console.WriteLine("Database seeded successfully.");
        return factory;
    }

    /// <summary>Gets the library GUID.</summary>
    public static Guid GetLibraryId() => LibraryId;

    /// <summary>Gets a deterministic artist ID.</summary>
    public static Guid GetArtistId(int artistIndex) =>
        Guid.Parse($"30000000-0000-0000-{artistIndex:X4}-000000000000");

    /// <summary>Gets a deterministic album ID.</summary>
    public static Guid GetAlbumId(int artistIndex, int albumIndex) =>
        Guid.Parse($"40000000-0000-{artistIndex:X4}-{albumIndex:X4}-000000000000");

    /// <summary>Gets a deterministic track ID.</summary>
    public static Guid GetTrackId(int artistIndex, int albumIndex, int trackIndex) =>
        Guid.Parse($"50000000-{artistIndex:X4}-{albumIndex:X4}-{trackIndex:X4}-000000000000");

    /// <summary>Gets a user ID.</summary>
    public static Guid GetUserId(int userIndex) => UserIds[userIndex];

    private static void SeedUsersAndLibrary(BenchmarkDbContextFactory factory)
    {
        Console.Write("  Seeding users and library... ");
        using var ctx = factory.CreateDbContext();

        for (int i = 0; i < UserCount; i++)
        {
            ctx.Users.Add(new User($"user{i}", "default", "default") { Id = UserIds[i] });
        }

        ctx.BaseItems.Add(new BaseItemEntity
        {
            Id = LibraryId,
            Type = "CollectionFolder",
            Name = "Music",
            SortName = "music",
            CleanName = "music",
            IsFolder = true,
            DateCreated = DateTime.UtcNow,
            PresentationUniqueKey = LibraryId.ToString("N"),
        });

        ctx.SaveChanges();
        Console.WriteLine("done.");
    }

    private static void SeedArtists(BenchmarkDbContextFactory factory)
    {
        Console.Write($"  Seeding {ArtistCount} artists... ");
        const int batchSize = 500;

        for (int batch = 0; batch < ArtistCount; batch += batchSize)
        {
            using var ctx = factory.CreateDbContext();
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            var end = Math.Min(batch + batchSize, ArtistCount);

            for (int i = batch; i < end; i++)
            {
                var id = GetArtistId(i);
                var name = $"Artist {i:D5}";
                ctx.BaseItems.Add(new BaseItemEntity
                {
                    Id = id,
                    Type = "MusicArtist",
                    Name = name,
                    SortName = name.ToLowerInvariant(),
                    CleanName = name.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal),
                    IsFolder = true,
                    TopParentId = LibraryId,
                    ParentId = LibraryId,
                    DateCreated = DateTime.UtcNow,
                    PresentationUniqueKey = id.ToString("N"),
                    Path = $"/music/{name}",
                });
            }

            ctx.SaveChanges();
        }

        Console.WriteLine("done.");
    }

    private static void SeedAlbumsTracksAndRelations(BenchmarkDbContextFactory factory)
    {
        var totalAlbums = ArtistCount * AlbumsPerArtist;
        var totalTracks = totalAlbums * TracksPerAlbum;
        Console.Write($"  Seeding {totalAlbums} albums, {totalTracks} tracks, values, user data, images... ");

        var random = new Random(42);
        const int artistBatchSize = 25; // Keep memory manageable

        // Pre-create ItemValues for artists and genres
        SeedItemValues(factory, random);

        for (int artistBatch = 0; artistBatch < ArtistCount; artistBatch += artistBatchSize)
        {
            using var ctx = factory.CreateDbContext();
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;

            var artistEnd = Math.Min(artistBatch + artistBatchSize, ArtistCount);

            for (int a = artistBatch; a < artistEnd; a++)
            {
                var artistId = GetArtistId(a);
                var artistName = $"Artist {a:D5}";
                var artistValueId = Guid.Parse($"70000000-0000-0000-{a:X4}-000000000001");
                var albumArtistValueId = Guid.Parse($"70000000-0000-0000-{a:X4}-000000000002");
                var genreIndex = random.Next(GenreCount);
                var genreValueId = Guid.Parse($"80000000-0000-0000-{genreIndex:X4}-000000000001");

                // Attach stub entities for navigation properties
                var artistValueStub = GetOrAttachItemValue(ctx, artistValueId, ItemValueType.Artist, artistName);
                var albumArtistValueStub = GetOrAttachItemValue(ctx, albumArtistValueId, ItemValueType.AlbumArtist, artistName);
                var genreValueStub = GetOrAttachItemValue(ctx, genreValueId, ItemValueType.Genre, GenreNames[genreIndex]);

                for (int al = 0; al < AlbumsPerArtist; al++)
                {
                    var albumId = GetAlbumId(a, al);
                    var albumName = $"Album {al:D3} by {artistName}";
                    var year = 1960 + random.Next(0, 65);
                    var genre = GenreNames[genreIndex];

                    var albumEntity = new BaseItemEntity
                    {
                        Id = albumId,
                        Type = "MusicAlbum",
                        Name = albumName,
                        SortName = albumName.ToLowerInvariant(),
                        CleanName = albumName.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal),
                        IsFolder = true,
                        TopParentId = LibraryId,
                        ParentId = artistId,
                        DateCreated = DateTime.UtcNow,
                        PresentationUniqueKey = albumId.ToString("N"),
                        AlbumArtists = artistName,
                        ProductionYear = year,
                        Genres = genre,
                        Path = $"/music/{artistName}/{albumName}",
                    };
                    ctx.BaseItems.Add(albumEntity);

                    ctx.AncestorIds.Add(new AncestorId { ItemId = albumId, ParentItemId = LibraryId, Item = albumEntity, ParentItem = GetOrAttachStub(ctx, LibraryId) });

                    // Album → AlbumArtist, Album → Genre
                    ctx.ItemValuesMap.Add(new ItemValueMap { ItemId = albumId, ItemValueId = albumArtistValueId, Item = albumEntity, ItemValue = albumArtistValueStub });
                    ctx.ItemValuesMap.Add(new ItemValueMap { ItemId = albumId, ItemValueId = genreValueId, Item = albumEntity, ItemValue = genreValueStub });

                    // Album image
                    ctx.BaseItemImageInfos.Add(new BaseItemImageInfo
                    {
                        Id = Guid.NewGuid(),
                        ItemId = albumId,
                        Item = albumEntity,
                        ImageType = 0,
                        Path = $"/cache/images/{albumId:N}.jpg",
                        Width = 500,
                        Height = 500,
                    });

                    for (int t = 0; t < TracksPerAlbum; t++)
                    {
                        var trackId = GetTrackId(a, al, t);
                        var trackName = $"Track {t + 1:D2} - {albumName}";
                        var duration = TimeSpan.FromSeconds(120 + random.Next(0, 360));

                        var trackEntity = new BaseItemEntity
                        {
                            Id = trackId,
                            Type = "Audio",
                            Name = trackName,
                            SortName = trackName.ToLowerInvariant(),
                            CleanName = trackName.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal),
                            IsFolder = false,
                            TopParentId = LibraryId,
                            ParentId = albumId,
                            DateCreated = DateTime.UtcNow,
                            PresentationUniqueKey = trackId.ToString("N"),
                            Album = albumName,
                            AlbumArtists = artistName,
                            Artists = artistName,
                            IndexNumber = t + 1,
                            ParentIndexNumber = 1,
                            RunTimeTicks = duration.Ticks,
                            MediaType = "Audio",
                            ProductionYear = year,
                            Genres = genre,
                            Path = $"/music/{artistName}/{albumName}/{t + 1:D2}.flac",
                            Size = random.Next(10_000_000, 80_000_000),
                            TotalBitrate = random.Next(800_000, 4_000_000),
                        };
                        ctx.BaseItems.Add(trackEntity);

                        ctx.AncestorIds.Add(new AncestorId { ItemId = trackId, ParentItemId = LibraryId, Item = trackEntity, ParentItem = GetOrAttachStub(ctx, LibraryId) });
                        ctx.AncestorIds.Add(new AncestorId { ItemId = trackId, ParentItemId = albumId, Item = trackEntity, ParentItem = albumEntity });

                        // Track → Artist, AlbumArtist, Genre
                        ctx.ItemValuesMap.Add(new ItemValueMap { ItemId = trackId, ItemValueId = artistValueId, Item = trackEntity, ItemValue = artistValueStub });
                        ctx.ItemValuesMap.Add(new ItemValueMap { ItemId = trackId, ItemValueId = albumArtistValueId, Item = trackEntity, ItemValue = albumArtistValueStub });
                        ctx.ItemValuesMap.Add(new ItemValueMap { ItemId = trackId, ItemValueId = genreValueId, Item = trackEntity, ItemValue = genreValueStub });

                        // UserData (~20% of tracks)
                        for (int u = 0; u < UserCount; u++)
                        {
                            if (random.NextDouble() > 0.20)
                            {
                                continue;
                            }

                            var userStub = ctx.Users.Local.FirstOrDefault(ue => ue.Id.Equals(UserIds[u]));
                            if (userStub is null)
                            {
                                userStub = new User($"user{u}", "default", "default") { Id = UserIds[u] };
                                ctx.Attach(userStub);
                            }

                            ctx.UserData.Add(new Jellyfin.Database.Implementations.Entities.UserData
                            {
                                ItemId = trackId,
                                Item = trackEntity,
                                UserId = UserIds[u],
                                User = userStub,
                                CustomDataKey = trackId.ToString("N"),
                                Played = random.NextDouble() > 0.3,
                                PlayCount = random.Next(0, 50),
                                IsFavorite = random.NextDouble() > 0.85,
                                LastPlayedDate = DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                                PlaybackPositionTicks = 0,
                            });
                        }
                    }
                }
            }

            ctx.SaveChanges();
        }

        Console.WriteLine("done.");
    }

    private static void SeedItemValues(BenchmarkDbContextFactory factory, Random random)
    {
        using var ctx = factory.CreateDbContext();
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;

        for (int i = 0; i < ArtistCount; i++)
        {
            var name = $"Artist {i:D5}";
            var cleanName = name.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);

            ctx.ItemValues.Add(new ItemValue
            {
                ItemValueId = Guid.Parse($"70000000-0000-0000-{i:X4}-000000000001"),
                Type = ItemValueType.Artist,
                Value = name,
                CleanValue = cleanName,
            });

            ctx.ItemValues.Add(new ItemValue
            {
                ItemValueId = Guid.Parse($"70000000-0000-0000-{i:X4}-000000000002"),
                Type = ItemValueType.AlbumArtist,
                Value = name,
                CleanValue = cleanName,
            });
        }

        for (int i = 0; i < GenreCount; i++)
        {
            ctx.ItemValues.Add(new ItemValue
            {
                ItemValueId = Guid.Parse($"80000000-0000-0000-{i:X4}-000000000001"),
                Type = ItemValueType.Genre,
                Value = GenreNames[i],
                CleanValue = GenreNames[i].ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal),
            });
        }

        ctx.SaveChanges();
    }

    private static void OptimizeDb(BenchmarkDbContextFactory factory)
    {
        using var ctx = factory.CreateDbContext();
        ctx.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE)");
        ctx.Database.ExecuteSqlRaw("ANALYZE");
    }

    private static ItemValue GetOrAttachItemValue(DbContext ctx, Guid id, ItemValueType type, string value)
    {
        var existing = ctx.Set<ItemValue>().Local.FirstOrDefault(e => e.ItemValueId.Equals(id));
        if (existing is not null)
        {
            return existing;
        }

        var stub = new ItemValue
        {
            ItemValueId = id,
            Type = type,
            Value = value,
            CleanValue = value.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal),
        };
        ctx.Attach(stub);
        return stub;
    }

    private static BaseItemEntity GetOrAttachStub(DbContext ctx, Guid id)
    {
        var existing = ctx.Set<BaseItemEntity>().Local.FirstOrDefault(e => e.Id.Equals(id));
        if (existing is not null)
        {
            return existing;
        }

        var stub = new BaseItemEntity { Id = id, Type = "Stub" };
        ctx.Attach(stub);
        return stub;
    }

    private static void TryAttach<T>(DbContext ctx, T entity)
        where T : class
    {
        try
        {
            ctx.Attach(entity);
        }
        catch (InvalidOperationException)
        {
            // Already tracked — ignore
        }
    }

    private static Guid[] GenerateUserIds()
    {
        var ids = new Guid[UserCount];
        for (int i = 0; i < UserCount; i++)
        {
            ids[i] = Guid.Parse($"20000000-0000-0000-0000-{i:D12}");
        }

        return ids;
    }

    private static string[] GenerateGenreNames(int count)
    {
        var baseGenres = new[]
        {
            "Rock", "Pop", "Jazz", "Blues", "Classical", "Electronic", "Hip-Hop", "R&B",
            "Country", "Folk", "Metal", "Punk", "Reggae", "Soul", "Funk", "Disco",
            "Ambient", "Industrial", "New Wave", "Grunge", "Alternative", "Indie",
            "Techno", "House", "Trance", "Drum and Bass", "Dubstep", "Lo-Fi",
            "Psychedelic", "Progressive", "Shoegaze", "Post-Rock", "Math Rock",
            "Emo", "Hardcore", "Ska", "Latin", "Bossa Nova", "Afrobeat", "World",
        };

        var genres = new string[count];
        for (int i = 0; i < count; i++)
        {
            genres[i] = i < baseGenres.Length
                ? baseGenres[i]
                : $"{baseGenres[i % baseGenres.Length]} {(i / baseGenres.Length) + 1}";
        }

        return genres;
    }
}
