using Jellyfin.Data.Enums;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Api.Models.PlaylistDtos;

/// <summary>
/// Sort playlist DTO.
/// </summary>
public class SortPlaylistDto
{
    /// <summary>
    /// Gets or sets the field to sort by.
    /// </summary>
    public ItemSortBy SortBy { get; set; } = ItemSortBy.SortName;

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
}
