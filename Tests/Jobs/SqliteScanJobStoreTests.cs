using Core.Jobs;
using Data;

namespace Tests.Jobs;

public sealed class SqliteScanJobStoreTests : ScanJobStoreContractTests, IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    protected override IScanJobStore CreateStore() => new SqliteScanJobStore(_database.Factory);
    
    public void Dispose() => _database.Dispose();
}