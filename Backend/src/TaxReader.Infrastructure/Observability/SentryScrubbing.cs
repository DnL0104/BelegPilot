using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Sentry;

namespace TaxReader.Infrastructure.Observability;

/// <summary>
/// PII scrubber for outbound Sentry events. Strips request bodies, filters query
/// strings and headers to a small allow-list, masks UUID path segments, and drops
/// user email/username/ip in favour of an opaque hash of the user ID.
/// Default-deny posture per D-14.
/// </summary>
public static partial class SentryScrubbing
{
    private static readonly HashSet<string> AllowedQueryKeys =
        new(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "year", "format" };

    private static readonly HashSet<string> AllowedHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "User-Agent" };

    // D-14 #6 — only these `Extra` keys may carry through to Sentry. Anything else
    // (vendor names, item descriptions, classification reasoning, receipt totals,
    // user emails) is stripped. Keep this list short and obviously non-PII.
    private static readonly HashSet<string> AllowedExtraKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "receipt_id",        // GUID — non-PII identifier
            "processing_run_id", // GUID — non-PII identifier
            "request_id",        // ASP.NET Core request id — non-PII
            "job_id",            // Phase 3 Hangfire job id — forward-compat
            "phase",             // pipeline phase: extracting/parsing/classifying
        };

    public static SentryEvent? Scrub(SentryEvent ev)
    {
        if (ev.Request is not null)
        {
            // D-14 #1 — strip request body entirely
            ev.Request.Data = null;

            // D-14 #2 — query string allow-list
            if (!string.IsNullOrEmpty(ev.Request.QueryString))
            {
                var parsed = QueryHelpers.ParseQuery(ev.Request.QueryString);
                var filtered = parsed
                    .Where(kvp => AllowedQueryKeys.Contains(kvp.Key))
                    .SelectMany(kvp => kvp.Value.Select(v =>
                        $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"));
                ev.Request.QueryString = string.Join("&", filtered);
            }

            // D-14 #3 — header allow-list
            if (ev.Request.Headers is { } headers)
            {
                var keysToRemove = headers.Keys
                    .Where(k => !AllowedHeaders.Contains(k))
                    .ToList();
                foreach (var k in keysToRemove)
                {
                    headers.Remove(k);
                }
            }

            // D-14 #4 — mask UUID path segments
            if (!string.IsNullOrEmpty(ev.Request.Url))
            {
                ev.Request.Url = UuidSegmentRegex().Replace(ev.Request.Url, ":id");
            }
        }

        // D-14 #5 — drop email/username/ip; replace user.Id with hash
        if (ev.User is not null)
        {
            ev.User.Email = null;
            ev.User.Username = null;
            ev.User.IpAddress = null;

            if (!string.IsNullOrEmpty(ev.User.Id))
            {
                ev.User.Other ??= new Dictionary<string, string>();
                ev.User.Other["id_hash"] = HashUserId(ev.User.Id);
                ev.User.Id = null;
            }
        }

        // D-14 #6 — wipe Extra keys that are not in the small allow-list. This
        // is active defence-in-depth on top of the call-site contract ("never push
        // receipt content as Sentry extras"). If a future contributor accidentally
        // adds `Sentry.SetExtra("vendor", receipt.Vendor)`, the scrubber drops it
        // before the event leaves the process.
        // SentryEvent.Extra is an IReadOnlyDictionary by interface but the concrete
        // runtime type implements IDictionary<string, object?> — we cast to mutate.
        if (ev.Extra is { Count: > 0 } && ev.Extra is IDictionary<string, object?> mutableExtra)
        {
            var keysToRemove = mutableExtra.Keys
                .Where(k => !AllowedExtraKeys.Contains(k))
                .ToList();
            foreach (var k in keysToRemove)
            {
                mutableExtra.Remove(k);
            }
        }

        // Tags / Fingerprint / Breadcrumbs are NOT in the call-site usage today.
        // CONVENTIONS.md follow-up (deferred): add a code-review rule that any
        // future `Sentry.SetTag` / `Sentry.AddBreadcrumb` must use only the
        // AllowedExtraKeys whitelist of values.

        return ev;
    }

    public static string HashUserId(string userId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    [GeneratedRegex(
        @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex UuidSegmentRegex();
}
