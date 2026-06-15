namespace Wms.Application.Picking;

public sealed record PickCandidate(
    Guid LocationId,
    Guid? LotId,
    Guid? HandlingUnitId,
    int Available,
    DateTime? ReceivedAt);
