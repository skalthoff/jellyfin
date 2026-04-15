using System;
using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Jellyfin.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--seed-only")
        {
            // Just create the database for manual inspection
            var dbPath = args.Length > 1 ? args[1] : "porcupine-bench.db";
            var sw = Stopwatch.StartNew();
            MusicLibrarySeeder.CreateSeededDatabase(dbPath);
            sw.Stop();
            Console.WriteLine($"\nSeeding completed in {sw.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"Database: {dbPath}");
            return;
        }

        if (args.Length > 0 && args[0] == "--quick")
        {
            // Quick mode: run queries once and print timing (no BenchmarkDotNet overhead)
            RunQuickBenchmark();
            return;
        }

        // Full BenchmarkDotNet run
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static void RunQuickBenchmark()
    {
        Console.WriteLine("=== Porcupine Quick Benchmark ===\n");

        var sw = Stopwatch.StartNew();
        MusicLibrarySeeder.CreateSeededDatabase();
        sw.Stop();
        Console.WriteLine($"\nDB seeded in {sw.Elapsed.TotalSeconds:F1}s\n");

        var benchmarks = new MusicQueryBenchmarks();
        benchmarks.Setup();

        // Warm up
        benchmarks.AlbumListing();

        var queries = new (string Name, Func<int> Run)[]
        {
            ("Album listing (400)", () => benchmarks.AlbumListing()),
            ("Album listing + COUNT", () => benchmarks.AlbumListingWithCount()),
            ("Albums by artist", () => benchmarks.AlbumsByArtist()),
            ("Tracks by artist", () => benchmarks.TracksByArtist()),
            ("Artist listing (400)", () => benchmarks.ArtistListing()),
            ("Recently played", () => benchmarks.RecentlyPlayed()),
            ("Search 'rock'", () => benchmarks.SearchAcrossTypes()),
            ("Tracks in album", () => benchmarks.TracksInAlbum()),
        };

        Console.WriteLine($"{"Query",-35} {"Time",10} {"Results",8}");
        Console.WriteLine(new string('-', 55));

        foreach (var (name, run) in queries)
        {
            // Run 3 times, take median
            var times = new double[3];
            int resultCount = 0;
            for (int i = 0; i < 3; i++)
            {
                sw.Restart();
                resultCount = run();
                sw.Stop();
                times[i] = sw.Elapsed.TotalMilliseconds;
            }

            Array.Sort(times);
            var median = times[1];
            Console.WriteLine($"{name,-35} {median,8:F1}ms {resultCount,8}");
        }

        benchmarks.Cleanup();
        Console.WriteLine("\nDone.");
    }
}
