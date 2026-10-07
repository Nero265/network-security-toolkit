using Core.Jobs;

namespace Tests.Jobs;

public sealed class InMemoryScanJobStoreTests : ScanJobStoreContractTests
{
    protected override IScanJobStore CreateStore() =>  new InMemoryScanJobStore();
}