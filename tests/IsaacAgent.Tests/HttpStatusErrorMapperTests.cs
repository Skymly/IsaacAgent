using System.Net;
using IsaacAgent.LLM;
using Xunit;

namespace IsaacAgent.Tests;

public class HttpStatusErrorMapperTests
{
    [Fact]
    public void EnsureSuccess_Succeeds_WhenStatusIs2xx()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.OK);
        HttpStatusErrorMapper.EnsureSuccessStatusCodeWithDetail(resp);
    }

    [Fact]
    public void EnsureSuccess_ThrowsHttpRequestException_With429Status_WhenTooManyRequests()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        var ex = Assert.Throws<HttpRequestException>(
            () => HttpStatusErrorMapper.EnsureSuccessStatusCodeWithDetail(resp));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("429", ex.Message);
        Assert.Contains("Rate limited", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void EnsureSuccess_ThrowsHttpRequestException_WithAuthStatus_WhenUnauthorizedOrForbidden(
        HttpStatusCode status)
    {
        using var resp = new HttpResponseMessage(status);

        var ex = Assert.Throws<HttpRequestException>(
            () => HttpStatusErrorMapper.EnsureSuccessStatusCodeWithDetail(resp));

        Assert.Equal(status, ex.StatusCode);
        Assert.Contains("Authentication failed", ex.Message);
        Assert.Contains(((int)status).ToString(), ex.Message);
    }

    [Fact]
    public void EnsureSuccess_FallsBackToEnsureSuccessStatusCode_ForOtherErrors()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        Assert.Throws<HttpRequestException>(
            () => HttpStatusErrorMapper.EnsureSuccessStatusCodeWithDetail(resp));
    }
}
