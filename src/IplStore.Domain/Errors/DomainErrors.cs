using IplStore.Shared;

namespace IplStore.Domain.Errors;

/// <summary>
/// Central registry of domain-level errors. Codes are stable identifiers used in HTTP
/// responses and client logic; descriptions are human-readable defaults.
/// </summary>
public static class DomainErrors
{
    public static class Product
    {
        public static readonly Error NotFound = Error.NotFound("product.not_found", "Product not found.");
        public static readonly Error SlugTaken = Error.Conflict("product.slug_taken", "A product with this slug already exists.");
        public static readonly Error Inactive = Error.Validation("product.inactive", "Product is not available.");
    }

    public static class Variant
    {
        public static readonly Error NotFound = Error.NotFound("variant.not_found", "Product variant not found.");
        public static readonly Error InsufficientStock = Error.Conflict("variant.insufficient_stock", "Not enough stock available.");
        public static readonly Error StockChanged = Error.Conflict("variant.stock_changed", "Stock changed since you last viewed. Please refresh.");
        public static Error SkuTaken(string sku) => Error.Conflict("variant.sku_taken", $"SKU '{sku}' is already in use.");
    }

    public static class Franchise
    {
        public static readonly Error NotFound = Error.NotFound("franchise.not_found", "Franchise not found.");
        public static Error ShortCodeTaken(string code) => Error.Conflict("franchise.code_taken", $"Franchise short code '{code}' is already in use.");
    }

    public static class Cart
    {
        public static readonly Error NotFound = Error.NotFound("cart.not_found", "Cart not found.");
        public static readonly Error Empty = Error.Validation("cart.empty", "Cart is empty.");
        public static readonly Error ItemNotFound = Error.NotFound("cart.item_not_found", "Item not in cart.");
        public static readonly Error QtyExceedsLimit = Error.Validation("cart.qty_exceeds_limit", "Quantity exceeds the per-line limit (10).");
        public static readonly Error InvalidQuantity = Error.Validation("cart.invalid_qty", "Quantity must be positive.");
    }

    public static class Order
    {
        public static readonly Error NotFound = Error.NotFound("order.not_found", "Order not found.");
        public static readonly Error CannotCancel = Error.Validation("order.cannot_cancel", "Order is no longer cancellable.");
        public static readonly Error AlreadyPlaced = Error.Conflict("order.already_placed", "This order was already placed (idempotency replay).");
        public static readonly Error PaymentFailed = Error.Failure("order.payment_failed", "Payment was declined.");
        public static readonly Error InvalidStateTransition = Error.Validation("order.invalid_state", "Invalid order state transition.");
    }

    public static class Coupon
    {
        public static readonly Error NotFound = Error.NotFound("coupon.not_found", "Coupon code is invalid.");
        public static readonly Error Expired = Error.Validation("coupon.expired", "Coupon has expired.");
        public static readonly Error UsageExceeded = Error.Validation("coupon.usage_exceeded", "Coupon usage limit reached.");
        public static readonly Error MinOrderValueNotMet = Error.Validation("coupon.min_order_not_met", "Order does not meet the minimum value for this coupon.");
    }

    public static class Review
    {
        public static readonly Error AlreadyReviewed = Error.Conflict("review.already_reviewed", "You have already reviewed this product.");
        public static readonly Error NotPurchased = Error.Forbidden("review.not_purchased", "You can only review products you have purchased.");
        public static readonly Error RatingOutOfRange = Error.Validation("review.rating_out_of_range", "Rating must be between 1 and 5.");
        public static readonly Error NotFound = Error.NotFound("review.not_found", "Review not found.");
    }

    public static class Auth
    {
        public static readonly Error InvalidCredentials = Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");
        public static readonly Error EmailTaken = Error.Conflict("auth.email_taken", "Email is already registered.");
        public static readonly Error InvalidRefreshToken = Error.Unauthorized("auth.invalid_refresh", "Invalid or expired refresh token.");
        public static readonly Error Forbidden = Error.Forbidden("auth.forbidden", "You don't have permission to perform this action.");
    }
}
