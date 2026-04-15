using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration;

/// <summary>
/// itemvalues Configuration.
/// </summary>
public class ItemValuesMapConfiguration : IEntityTypeConfiguration<ItemValueMap>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ItemValueMap> builder)
    {
        builder.HasKey(e => new { e.ItemValueId, e.ItemId });
        builder.HasOne(e => e.Item);
        builder.HasOne(e => e.ItemValue);

        // Porcupine: index for lookups by ItemId (the hot path for artist/genre filtering).
        // The composite key has ItemValueId first, so lookups by ItemId require a full scan.
        builder.HasIndex(e => new { e.ItemId, e.ItemValueId });
    }
}
