namespace Core.Defense.Lockout;

public interface ILockoutStore
{
    ValueTask<LockoutStatus> GetStatusAsync(LockoutKey key, CancellationToken cancellationToken = default);
    ValueTask<LockoutStatus> RegisterFailureAsync(LockoutKey key, CancellationToken cancellationToken = default);
    ValueTask ResetAsync(LockoutKey key, CancellationToken cancellationToken = default);
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}