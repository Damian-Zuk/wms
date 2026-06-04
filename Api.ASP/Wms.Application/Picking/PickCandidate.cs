namespace Wms.Application.Picking;

public sealed record PickCandidate(Guid LocationId, Guid? LotId, int Available, DateTime? ReceivedAt);
