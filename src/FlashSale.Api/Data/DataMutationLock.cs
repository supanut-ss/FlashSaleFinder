namespace FlashSale.Api.Data;

// Serializes local demo writes; database uniqueness constraints remain the cross-instance guard.
public sealed class DataMutationLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}
