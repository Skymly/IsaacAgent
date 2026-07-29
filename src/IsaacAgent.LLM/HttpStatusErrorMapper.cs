using System.Net;

namespace IsaacAgent.LLM;

/// <summary>
/// Maps common LLM HTTP error statuses to descriptive <see cref="HttpRequestException"/>s
/// so OpenAI-compatible and Ollama providers share one policy.
/// </summary>
internal static class HttpStatusErrorMapper
{
    /// <summary>
    /// Throws an <see cref="HttpRequestException"/> with a descriptive message
    /// for 429 / 401 / 403, falling back to
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> for others.
    /// </summary>
    public static void EnsureSuccessStatusCodeWithDetail(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;

        var status = resp.StatusCode;
        if (status == HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException(
                "Rate limited by LLM provider (429 Too ManyRequests). Request will be retried after backoff.",
                null, status);
        }

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException(
                $"Authentication failed ({(int)status} {status}). Check API key and permissions.",
                null, status);
        }

        resp.EnsureSuccessStatusCode();
    }
}
