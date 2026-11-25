namespace Core.Defense.Lockout;

public sealed class LockoutOutcomeSink(ILockoutStore store) : IDefenseOutcomeSink
{
    public async ValueTask RecordFailureAsync(DefenseContext context, CancellationToken cancellationToken = default)
    {
        await store.RegisterFailureAsync(
            LockoutKey.Create(context.Username, context.IpAddress), cancellationToken);
    }

    public async ValueTask RecordSuccessAsync(DefenseContext context, CancellationToken cancellationToken = default)
    {
        await store.ResetAsync(
            LockoutKey.Create(context.Username, context.IpAddress), cancellationToken);
    }
}