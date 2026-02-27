namespace Arcus.ClamAV.Services;

public interface IClamAvInfoService
{
    Task<string> GetVersionAsync();
}
