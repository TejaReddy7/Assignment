// Wire-contract types mirrored from the backend DTOs (kept in sync manually).

export interface Money {
  amount: number;
  currency: string;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProductListItem {
  id: string;
  name: string;
  slug: string;
  typeName: string;
  basePrice: Money;
  imageUrl: string | null;
  averageRating: number;
  reviewCount: number;
  inStock: boolean;
  franchiseId: string;
  franchiseName: string;
  franchiseShortCode: string;
  defaultVariantId: string | null;
  variants: ProductVariant[];
}

export interface ProductVariant {
  id: string;
  sku: string;
  size: string | null;
  color: string | null;
  stockQuantity: number;
  inStock: boolean;
  price: Money;
}

export interface Franchise {
  id: string;
  name: string;
  shortCode: string;
  city: string;
  primaryColor: string;
  foundedYear: number;
  logoUrl: string | null;
}

export interface ProductDetails {
  id: string;
  name: string;
  slug: string;
  description: string;
  typeName: string;
  basePrice: Money;
  imageUrl: string | null;
  averageRating: number;
  reviewCount: number;
  franchise: Franchise;
  variants: ProductVariant[];
}

export interface FacetCount {
  value: string;
  label: string;
  count: number;
}

export interface PriceBucket {
  label: string;
  min: number;
  max: number | null;
  count: number;
}

export interface SearchResult {
  items: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  facets: {
    franchises: FacetCount[];
    types: FacetCount[];
    priceBuckets: PriceBucket[];
  };
}

export interface FeaturedProduct {
  badge: string;
  reason: string;
  demandScore: number;
  product: ProductListItem;
}

export interface AuthResponse {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
}

export interface CartItem {
  id: string;
  productId: string;
  productVariantId: string;
  productName: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface Cart {
  id: string;
  items: CartItem[];
  totalItems: number;
  subtotal: number;
  currency: string;
}

export interface OrderSummary {
  id: string;
  orderNumber: string;
  statusName: string;
  total: number;
  currency: string;
  itemCount: number;
  placedAtUtc: string;
  customerEmail?: string | null;
  customerName?: string | null;
}

export interface OrderItem {
  productName: string;
  sku: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderDetails {
  id: string;
  orderNumber: string;
  statusName: string;
  paymentStatus: number;
  items: OrderItem[];
  subtotal: number;
  discountAmount: number;
  shippingFee: number;
  total: number;
  currency: string;
  couponCode: string | null;
  placedAtUtc: string;
}

export interface CouponValidationResult {
  isValid: boolean;
  code: string;
  discount: number;
  newTotal: number;
  reason: string | null;
}

export interface CreatedProduct {
  id: string;
  name: string;
  slug: string;
}

export interface ReviewItem {
  id: string;
  productId: string;
  customerDisplayName: string;
  rating: number;
  title: string;
  body: string;
  createdAtUtc: string;
}

export interface ProductReviews {
  productId: string;
  averageRating: number;
  reviewCount: number;
  reviews: ReviewItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface WishlistItem {
  productId: string;
  name: string;
  slug: string;
  price: number;
  currency: string;
  imageUrl: string | null;
  franchiseShortCode: string;
  inStock: boolean;
  addedAtUtc: string;
}
