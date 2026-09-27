using Core.Jobs;

namespace Core;

public interface IScannerFactory
{
    IPortScanner GetScanner(ScanType scanType);
}
