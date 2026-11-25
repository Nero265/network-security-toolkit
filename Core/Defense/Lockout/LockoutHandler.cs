namespace Core.Defense.Lockout;

public sealed class LockoutHandler(ILockoutStore store) : IDefenseHandler
{
    public const string Name = "lockout";

    // NOTE: adjust the signature to match your IDefenseHandler.
    public async ValueTask<DefenseResult> HandleAsync(
        DefenseContext context,
        DefenseDelegate next,
        CancellationToken cancellationToken)
    {
        var key = LockoutKey.Create(context.Username, context.IpAddress);
        var status = await store.GetStatusAsync(key, cancellationToken);

        if (status.IsLocked)
        {
            return DefenseResult.Block("Too many failed login attempts.", Name, status.RetryAfter);
        }

        return await next(context, cancellationToken);
    }
}