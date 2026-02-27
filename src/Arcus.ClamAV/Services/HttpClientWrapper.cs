namespace Arcus.ClamAV.Services;

public class HttpClientWrapper : IHttpClientWrapper
{
    private readonly HttpClient _httpClient;

    public HttpClientWrapper()
    {
        _httpClient = new HttpClient();
    }

    public TimeSpan Timeout
    {
        get => _httpClient.Timeout;
        set => _httpClient.Timeout = value;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _httpClient.SendAsync(request, cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        return _httpClient.SendAsync(request, completionOption, cancellationToken);
    }
}
