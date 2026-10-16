using System.Threading;
using System.Threading.Tasks;

namespace AcxiomCRM.Services;

public interface ICrmSeedService
{
    /// <summary>Ensures required Identity roles and an explicitly configured initial administrator exist.</summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
