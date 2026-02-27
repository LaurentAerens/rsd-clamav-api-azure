namespace Arcus.ClamAV.Services;

public interface IHttpClientWrapper
{
    TimeSpan Timeout { get; set; }
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken);
}
