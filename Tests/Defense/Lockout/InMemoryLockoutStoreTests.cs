using Core.Defense.Lockout;

namespace Tests.Defense.Lockout;

public sealed class InMemoryLockoutStoreTests : LockoutStoreContractTests
{
    protected override ILockoutStore CreateStore(LockoutOptions options, TimeProvider timeProvider) =>
        new InMemoryLockoutStore(options, timeProvider);
}