using Core.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Core;

public sealed class ScannerFactory(IServiceProvider serviceProvider) : IScannerFactory
{
    public IPortScanner GetScanner(ScanType scanType)
    {
        if (!Enum.IsDefined(scanType))
        {
            throw new ArgumentOutOfRangeException(nameof(scanType), scanType, $"Unsupported scan type: {scanType}");
        }

        return serviceProvider.GetRequiredKeyedService<IPortScanner>(scanType);
    }
}