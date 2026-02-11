namespace Arcus.ClamAV.Api.Client.Security
{
    public interface ITokenAcquisitionService
    {
        Task<string> GetTokenAsync(CancellationToken cancellationToken);
    }
}

