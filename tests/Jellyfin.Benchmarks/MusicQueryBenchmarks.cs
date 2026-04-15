using System;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Benchmarks;

/// <summary>
/// Benchmarks the critical music query paths in BaseItemRepository.
/// Run with: dotnet run -c Release -- --filter '*'
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class MusicQueryBenchmarks
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _libraryId;
    private Guid _artistId;
    private Guid _albumId;
    private Guid _userId;

    [GlobalSetup]
    public void Setup()
    {
        _factory = MusicLibrarySeeder.CreateSeededDatabase();
        _libraryId = MusicLibrarySeeder.GetLibraryId();
        _artistId = MusicLibrarySeeder.GetArtistId(42); // Arbitrary artist
        _albumId = MusicLibrarySeeder.GetAlbumId(42, 3); // Arbitrary album
        _userId = MusicLibrarySeeder.GetUserId(0);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
    }

    /// <summary>
    /// Simulates: GET /Items?IncludeItemTypes=MusicAlbum&amp;ParentId={libraryId}&amp;Recursive=true&amp;Limit=400&amp;SortBy=SortName
    /// The most common Jellify query: paginated album listing.
    /// </summary>
    [Benchmark(Description = "Album listing (400 items, sorted by name)")]
    public int AlbumListing()
    {
        using var ctx = _factory.CreateDbContext();
        var libId = _libraryId;
        var uid = _userId;
        var query = ctx.BaseItems.AsNoTracking().AsSplitQuery()
            .Where(e => e.Type == "MusicAlbum")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .Include(e => e.Images)
            .Include(e => e.UserData!.Where(ud => ud.UserId.Equals(uid)))
            .OrderBy(e => e.SortName)
            .Take(400);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Same query but with COUNT(*) first (the current Jellyfin behavior).
    /// </summary>
    [Benchmark(Description = "Album listing with COUNT (Jellyfin default)")]
    public int AlbumListingWithCount()
    {
        using var ctx = _factory.CreateDbContext();
        var libId = _libraryId;
        var uid = _userId;
        var baseQuery = ctx.BaseItems.AsNoTracking().AsSplitQuery()
            .Where(e => e.Type == "MusicAlbum")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId));

        // Count first (current Jellyfin behavior)
        var count = baseQuery.Count();

        var results = baseQuery
            .Include(e => e.Images)
            .Include(e => e.UserData!.Where(ud => ud.UserId.Equals(uid)))
            .OrderBy(e => e.SortName)
            .Take(400)
            .ToList();

        return count + results.Count;
    }

    /// <summary>
    /// Simulates: GET /Items?IncludeItemTypes=MusicAlbum&amp;AlbumArtistIds={artistId}&amp;Recursive=true
    /// Artist detail page: "Albums by this artist" — hits the ItemValues join.
    /// </summary>
    [Benchmark(Description = "Albums by artist (fully materialized)")]
    public int AlbumsByArtist()
    {
        using var ctx = _factory.CreateDbContext();
        var artId = _artistId;
        var libId = _libraryId;

        // Porcupine Phase 3: fully materialize the lookup chain.
        // Step 1: resolve artist → ItemValueIds (instant, ~1-2 results)
        var matchingValueIds = ctx.ItemValues
            .Where(iv => iv.Type == ItemValueType.AlbumArtist)
            .Where(iv => ctx.BaseItems
                .Where(b => b.Id.Equals(artId))
                .Any(b => b.CleanName == iv.CleanValue))
            .Select(iv => iv.ItemValueId)
            .ToList();

        // Step 2: resolve ItemValueIds → matching item IDs (uses ItemValueMap index)
        var matchingItemIds = ctx.ItemValuesMap
            .Where(m => matchingValueIds.Contains(m.ItemValueId))
            .Select(m => m.ItemId)
            .Distinct()
            .ToList();

        // Step 3: simple IN clause on BaseItems — no correlated subquery
        var query = ctx.BaseItems.AsNoTracking().AsSplitQuery()
            .Where(e => e.Type == "MusicAlbum")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .Where(e => matchingItemIds.Contains(e.Id))
            .Include(e => e.Images)
            .OrderBy(e => e.SortName);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Simulates: GET /Items?IncludeItemTypes=Audio&amp;ArtistIds={artistId}&amp;Recursive=true
    /// All tracks by an artist — heavy ItemValues join at scale.
    /// </summary>
    [Benchmark(Description = "Tracks by artist (fully materialized)")]
    public int TracksByArtist()
    {
        using var ctx = _factory.CreateDbContext();
        var artId = _artistId;
        var libId = _libraryId;
        var uid = _userId;

        // Porcupine Phase 3: fully materialize for both Artist and AlbumArtist types
        var matchingValueIds = ctx.ItemValues
            .Where(iv => iv.Type == ItemValueType.Artist || iv.Type == ItemValueType.AlbumArtist)
            .Where(iv => ctx.BaseItems
                .Where(b => b.Id.Equals(artId))
                .Any(b => b.CleanName == iv.CleanValue))
            .Select(iv => iv.ItemValueId)
            .ToList();

        var matchingItemIds = ctx.ItemValuesMap
            .Where(m => matchingValueIds.Contains(m.ItemValueId))
            .Select(m => m.ItemId)
            .Distinct()
            .ToList();

        var query = ctx.BaseItems.AsNoTracking().AsSplitQuery()
            .Where(e => e.Type == "Audio")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .Where(e => matchingItemIds.Contains(e.Id))
            .Include(e => e.Images)
            .Include(e => e.UserData!.Where(ud => ud.UserId.Equals(uid)))
            .OrderBy(e => e.Album).ThenBy(e => e.IndexNumber)
            .Take(400);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Simulates: GET /Artists/AlbumArtists?ParentId={libraryId}&amp;Limit=400&amp;SortBy=SortName
    /// Artist listing — queries "by name" items.
    /// </summary>
    [Benchmark(Description = "Artist listing (400 items)")]
    public int ArtistListing()
    {
        using var ctx = _factory.CreateDbContext();
        var libId = _libraryId;
        var query = ctx.BaseItems.AsNoTracking()
            .Where(e => e.Type == "MusicArtist")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .OrderBy(e => e.SortName)
            .Take(400);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Simulates: GET /Items?IncludeItemTypes=Audio&amp;SortBy=DatePlayed&amp;SortOrder=Descending
    /// Recently played — requires UserData join.
    /// </summary>
    [Benchmark(Description = "Recently played tracks (UserData join)")]
    public int RecentlyPlayed()
    {
        using var ctx = _factory.CreateDbContext();
        var libId = _libraryId;
        var uid = _userId;

        // Porcupine: use AsSingleQuery for queries with UserData WHERE clauses
        var query = ctx.BaseItems.AsNoTracking().AsSingleQuery()
            .Where(e => e.Type == "Audio")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .Where(e => e.UserData!.Any(ud => ud.UserId.Equals(uid) && ud.Played))
            .Include(e => e.UserData!.Where(ud => ud.UserId.Equals(uid)))
            .Include(e => e.Images)
            .OrderByDescending(e => e.UserData!
                .Where(ud => ud.UserId.Equals(uid))
                .Max(ud => ud.LastPlayedDate))
            .Take(50);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Simulates: GET /Items?SearchTerm=rock&amp;IncludeItemTypes=MusicArtist,Audio,MusicAlbum
    /// Search across all music types.
    /// </summary>
    [Benchmark(Description = "Search 'artist001' across all music types")]
    public int SearchAcrossTypes()
    {
        using var ctx = _factory.CreateDbContext();
        var libId = _libraryId;

        // Search for "artist001" which matches artist names in the seeded data
        var searchTerm = "artist001";

        var query = ctx.BaseItems.AsNoTracking()
            .Where(e => e.Type == "Audio" || e.Type == "MusicAlbum" || e.Type == "MusicArtist")
            .Where(e => e.TopParentId.HasValue && e.TopParentId.Value.Equals(libId))
            .Where(e => e.CleanName != null && EF.Functions.Like(e.CleanName, $"%{searchTerm}%"))
            .OrderBy(e => e.SortName)
            .Take(50);

        var results = query.ToList();
        return results.Count;
    }

    /// <summary>
    /// Tracks in a specific album — the simplest music query.
    /// </summary>
    [Benchmark(Description = "Tracks in album (by ParentId)")]
    public int TracksInAlbum()
    {
        using var ctx = _factory.CreateDbContext();
        var albId = _albumId;
        var uid = _userId;
        var query = ctx.BaseItems.AsNoTracking().AsSplitQuery()
            .Where(e => e.Type == "Audio")
            .Where(e => e.ParentId.HasValue && e.ParentId.Value.Equals(albId))
            .Include(e => e.UserData!.Where(ud => ud.UserId.Equals(uid)))
            .Include(e => e.Images)
            .OrderBy(e => e.IndexNumber);

        var results = query.ToList();
        return results.Count;
    }
}
