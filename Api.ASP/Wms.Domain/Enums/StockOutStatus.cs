namespace Wms.Domain.Enums;

public enum StockOutStatus
{
    Draft = 1,
    Picking = 2,
    Packed = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6
}
