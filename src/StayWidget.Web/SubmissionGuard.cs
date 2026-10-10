using System.Security.Cryptography;
using System.Text;
namespace StayWidget.Web;

public sealed class SubmissionLimited(int retrySeconds) : Exception("Too many reservation attempts. No new request was created. Use your saved status link, retry later or contact the property.")
{
    public int RetrySeconds { get; } = Math.Max(1, retrySeconds);
}

// Short-lived in-memory HMAC buckets, no raw network address storage or visitor tracking.
public sealed class SubmissionGuard(TimeProvider clock)
{
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, int> attempts = new();
    private readonly object gate = new();
    private long window = -1, minute = -1;
    private int total;
    public string Identity(string address) => Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(address)));
    public void Acquire(string address)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var hash = Identity(address);
        lock (gate)
        {
            if (window != now / 600) { attempts.Clear(); window = now / 600; }
            if (minute != now / 60) { minute = now / 60; total = 0; }
            if (total >= 100 || attempts.Count >= 10000 && !attempts.ContainsKey(hash)) throw new SubmissionLimited((int)(60 - now % 60));
            total++;
            var count = attempts.GetValueOrDefault(hash);
            if (count >= 5) throw new SubmissionLimited((int)(600 - now % 600));
            attempts[hash] = count + 1;
        }
    }
}
