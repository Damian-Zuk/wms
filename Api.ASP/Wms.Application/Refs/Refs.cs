using Wms.Domain.Enums;

namespace Wms.Application.Refs;

public sealed record ProductRef(Guid Id, string Sku, string Name, decimal UnitPrice);

public sealed record LocationRef(Guid Id, string Code, string Address);

public sealed record LotRef(Guid Id, string Number, DateOnly? ExpirationDate);

public sealed record HandlingUnitRef(Guid Id, string Code, HandlingUnitType Type);
