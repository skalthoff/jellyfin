using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using Jellyfin.Database.Implementations.Interfaces;
using Jellyfin.Database.Implementations.Locking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Benchmarks;

/// <summary>
/// Creates JellyfinDbContext instances for benchmarking with minimal dependencies.
/// </summary>
public sealed class BenchmarkDbContextFactory
{
    private readonly DbContextOptions<JellyfinDbContext> _options;
    private readonly ILogger<JellyfinDbContext> _logger;
    private readonly IJellyfinDatabaseProvider _provider;
    private readonly NoLockBehavior _locking;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkDbContextFactory"/> class.
    /// </summary>
    /// <param name="dbPath">Path to the SQLite database file.</param>
    public BenchmarkDbContextFactory(string dbPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Default,
            Pooling = true,
        }.ToString();

        _options = new DbContextOptionsBuilder<JellyfinDbContext>()
            .UseSqlite(connectionString, o => o.MigrationsAssembly(typeof(Jellyfin.Database.Providers.Sqlite.SqliteDatabaseProvider).Assembly))
            .ConfigureWarnings(w =>
            {
                w.Ignore(RelationalEventId.NonTransactionalMigrationOperationWarning);
                w.Ignore(RelationalEventId.PendingModelChangesWarning);
            })
            .Options;

        _logger = NullLogger<JellyfinDbContext>.Instance;
        _provider = new NoOpDatabaseProvider();
        _locking = new NoLockBehavior(NullLogger<NoLockBehavior>.Instance);
    }

    /// <summary>
    /// Creates a new JellyfinDbContext instance.
    /// </summary>
    /// <returns>A configured JellyfinDbContext.</returns>
    public JellyfinDbContext CreateDbContext()
    {
        return new JellyfinDbContext(_options, _logger, _provider, _locking);
    }

    /// <summary>
    /// Applies migrations to create the database schema.
    /// </summary>
    /// <summary>
    /// Creates the database schema from the current model (including all indexes).
    /// Uses EnsureCreated instead of Migrate to reflect index changes without generating migrations.
    /// </summary>
    public void EnsureCreated()
    {
        using var ctx = CreateDbContext();
        ctx.Database.EnsureCreated();

        // Porcupine: apply aggressive SQLite tuning for read-heavy music workloads
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");
        ctx.Database.ExecuteSqlRaw("PRAGMA cache_size=-65536"); // 64MB cache (negative = KB)
        ctx.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL");
        ctx.Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY");
        ctx.Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456"); // 256MB mmap
        ctx.Database.ExecuteSqlRaw("PRAGMA page_size=8192"); // larger pages for bulk reads
    }

    /// <summary>
    /// Minimal IJellyfinDatabaseProvider that does nothing beyond what EF Core needs.
    /// </summary>
    private sealed class NoOpDatabaseProvider : IJellyfinDatabaseProvider
    {
        public IDbContextFactory<JellyfinDbContext>? DbContextFactory { get; set; }

        public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
        {
        }

        public void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
        }

        public Task RunScheduledOptimisation(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RunShutdownTask(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> MigrationBackupFast(CancellationToken cancellationToken) => Task.FromResult(string.Empty);

        public Task RestoreBackupFast(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteBackup(string key) => Task.CompletedTask;

        public Task PurgeDatabase(JellyfinDbContext dbContext, IEnumerable<string>? tableNames) => Task.CompletedTask;
    }
}
