using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPulse.Application.Import;

public interface IImportService
{
    Task<ImportResult> ImportItemsAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
    Task<ImportResult> ImportBomsAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
    Task<ImportResult> ImportInventoryAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
    
    Task<byte[]> GenerateItemTemplateAsync(string format);
    Task<byte[]> GenerateBomTemplateAsync(string format);
    Task<byte[]> GenerateInventoryTemplateAsync(string format);
}
