namespace IplStore.Domain.Enums;

public enum ProductType
{
    Jersey = 1,
    Cap = 2,
    Flag = 3,
    AutographedPhoto = 4,
    Accessory = 5,
    Memorabilia = 6
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum CouponType
{
    Percentage = 1,
    FixedAmount = 2
}

public enum PaymentMethod
{
    CreditCard = 1,
    DebitCard = 2,
    Upi = 3,
    NetBanking = 4,
    CashOnDelivery = 5
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4
}
