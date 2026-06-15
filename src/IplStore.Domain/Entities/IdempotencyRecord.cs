using IplStore.Domain.Primitives;

namespace IplStore.Domain.Entities;

/// <summary>
/// Records the outcome of a write operation against an Idempotency-Key,
/// allowing safe replay of network-retried requests.
/// </summary>
public sealed class IdempotencyRecord : Entity<Guid>
{
    private IdempotencyRecord() { } // EF

    public IdempotencyRecord(string key, string requestHash, int statusCode, string responseBody, Guid? userId)
        : base(Guid.NewGuid())
    {
        Key = key;
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        UserId = userId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Key { get; private set; } = default!;
    public string RequestHash { get; private set; } = default!;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = default!;
    public Guid? UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
