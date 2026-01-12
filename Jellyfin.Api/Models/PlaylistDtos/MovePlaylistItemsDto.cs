using System.Collections.Generic;

namespace Jellyfin.Api.Models.PlaylistDtos;

/// <summary>
/// Move playlist items DTO.
/// </summary>
public class MovePlaylistItemsDto
{
    /// <summary>
    /// Gets or sets the list of items to move.
    /// </summary>
    public required IReadOnlyList<MovePlaylistItemRequest> Items { get; set; }
}

/// <summary>
/// Request to move a single playlist item.
/// </summary>
public class MovePlaylistItemRequest
{
    /// <summary>
    /// Gets or sets the playlist item id.
    /// </summary>
    public required string PlaylistItemId { get; set; }

    /// <summary>
    /// Gets or sets the new index.
    /// </summary>
    public required int NewIndex { get; set; }
}
