namespace Sun2Sat.Services;

/// <summary>The small slice of a user that other users get to see in lists.
/// Pages get this instead of ApplicationUser so we never pass around password
/// hashes, emails or phone numbers by accident.</summary>
public record UserSummary(string Id, string UserName, string? DisplayName)
{
    public string NameToShow => string.IsNullOrWhiteSpace(DisplayName) ? UserName : DisplayName;
    public bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayName);
}

/// <summary>How the current user relates to another user. Drives which button a page shows.</summary>
public enum RelationshipState
{
    Self,
    None,
    Connected,
    /// <summary>I sent a request that hasn't been accepted. Also shown when the other
    /// person ignored it: the requester is never told about an ignore.</summary>
    RequestSent,
    /// <summary>They sent me a request I haven't answered.</summary>
    RequestReceived
}

public record UserWithRelationship(UserSummary User, RelationshipState State);

/// <summary>A pending request plus the other person, for the Requests lists.</summary>
public record ConnectionRequestView(int ConnectionId, UserSummary OtherUser, DateTime CreatedAtUtc);
