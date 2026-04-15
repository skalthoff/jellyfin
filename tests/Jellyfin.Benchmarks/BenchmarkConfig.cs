using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace Jellyfin.Benchmarks;

/// <summary>
/// BenchmarkDotNet configuration for music query benchmarks.
/// </summary>
internal sealed class BenchmarkConfig : ManualConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkConfig"/> class.
    /// </summary>
    public BenchmarkConfig()
    {
        WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(50));
        AddColumn(StatisticColumn.P95);
    }
}
