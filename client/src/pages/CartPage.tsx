import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError } from '../api';
import { useAuth } from '../auth';
import { useCart } from '../cart';
import type { CouponValidationResult, OrderDetails } from '../types';

export function CartPage() {
  const { isAuthenticated } = useAuth();
  const { cart, updateQty, removeItem, refresh } = useCart();
  const navigate = useNavigate();

  const [coupon, setCoupon] = useState('');
  const [couponInfo, setCouponInfo] = useState<CouponValidationResult | null>(null);
  const [placedOrder, setPlacedOrder] = useState<OrderDetails | null>(null);
  const [message, setMessage] = useState('');
  const [placing, setPlacing] = useState(false);

  // Editable shipping address (sensible defaults so checkout works out of the box).
  const [address, setAddress] = useState({
    line1: '123 Fan Street',
    city: 'Mumbai',
    state: 'Maharashtra',
    postalCode: '400001',
    country: 'India',
  });

  const setField = (field: keyof typeof address, value: string) =>
    setAddress((a) => ({ ...a, [field]: value }));

  const changeQty = async (itemId: string, quantity: number) => {
    setMessage('');
    try {
      await updateQty(itemId, quantity);
      setCouponInfo(null); // totals changed → invalidate the preview
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Update failed');
    }
  };

  const remove = async (itemId: string) => {
    await removeItem(itemId);
    setCouponInfo(null);
  };

  const applyCoupon = async () => {
    if (!coupon) return;
    setMessage('');
    try {
      const res = await api.post<CouponValidationResult>('/coupons/validate', {
        code: coupon,
        cartTotal: cart.subtotal,
      });
      setCouponInfo(res);
      if (!res.isValid) setMessage(res.reason ?? 'Coupon is not valid.');
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Could not validate coupon');
    }
  };

  const placeOrder = async () => {
    // Guests are sent to login; the cart merges automatically on return.
    if (!isAuthenticated) {
      navigate('/login?returnTo=/cart');
      return;
    }
    setMessage('');
    setPlacing(true);
    try {
      const order = await api.post<OrderDetails>(
        '/orders',
        {
          shippingAddress: { ...address, line2: null },
          paymentMethod: 3,
          couponCode: coupon || null,
        },
        { 'Idempotency-Key': crypto.randomUUID() }
      );
      setPlacedOrder(order);
      await refresh();
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Checkout failed');
    } finally {
      setPlacing(false);
    }
  };

  // Order placed success state
  if (placedOrder) {
    return (
      <div className="order-success" id="order-success">
        <div className="order-success-icon">🎉</div>
        <h1>Order Placed!</h1>
        <p>
          Order <span className="order-number">{placedOrder.orderNumber}</span> — {placedOrder.statusName}
        </p>
        <p>
          Total: <strong>₹{placedOrder.total.toFixed(2)} {placedOrder.currency}</strong>
          {placedOrder.discountAmount > 0 && (
            <span className="savings"> · Saved ₹{placedOrder.discountAmount.toFixed(2)}</span>
          )}
        </p>
        <Link to="/orders">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-2px', marginRight: '6px' }}>
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
            <polyline points="14 2 14 8 20 8"/>
            <line x1="16" y1="13" x2="8" y2="13"/>
            <line x1="16" y1="17" x2="8" y2="17"/>
          </svg>
          View order history
        </Link>
      </div>
    );
  }

  // Empty cart state
  if (cart.items.length === 0) {
    return (
      <div className="empty-state" id="empty-cart">
        <div className="empty-state-icon">🛒</div>
        <h2>Your cart is empty</h2>
        <p>Browse our collection and add some cricket merch!</p>
        <Link to="/">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-2px', marginRight: '6px' }}>
            <line x1="19" y1="12" x2="5" y2="12"/>
            <polyline points="12 19 5 12 12 5"/>
          </svg>
          Browse Products
        </Link>
      </div>
    );
  }

  return (
    <div className="cart-page" id="cart-page">
      <h1>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-4px', marginRight: '10px', opacity: 0.6 }}>
          <circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/>
          <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"/>
        </svg>
        Your Cart
      </h1>

      {/* Cart Items */}
      <div className="cart-items">
        {cart.items.map((i) => (
          <div className="cart-item" key={i.id}>
            <div className="cart-item-image">🏏</div>
            <div className="cart-item-details">
              <div className="cart-item-name">{i.productName}</div>
              <div className="cart-item-price-unit">₹{i.unitPrice.toFixed(0)} each</div>
            </div>
            <div className="cart-item-qty">
              <button
                type="button"
                onClick={() => changeQty(i.id, Math.max(1, i.quantity - 1))}
                disabled={i.quantity <= 1}
              >−</button>
              <span className="qty-value">{i.quantity}</span>
              <button
                type="button"
                onClick={() => changeQty(i.id, Math.min(10, i.quantity + 1))}
                disabled={i.quantity >= 10}
              >+</button>
            </div>
            <div className="cart-item-line">₹{i.lineTotal.toFixed(0)}</div>
            <button
              className="cart-item-remove"
              onClick={() => remove(i.id)}
              title="Remove item"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="3 6 5 6 21 6"/>
                <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>
                <path d="M10 11v6"/><path d="M14 11v6"/>
                <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>
              </svg>
            </button>
          </div>
        ))}
      </div>

      {/* Checkout Panel */}
      <div className="checkout" id="checkout-panel">
        <div className="subtotal">
          Subtotal: <span className="currency">₹</span>{cart.subtotal.toFixed(2)}
        </div>

        {couponInfo?.isValid && (
          <>
            <div className="discount-line">
              Coupon {couponInfo.code}: <span>−₹{couponInfo.discount.toFixed(2)}</span>
            </div>
            <div className="subtotal" style={{ fontSize: '1.05rem' }}>
              After discount: <span className="currency">₹</span>{couponInfo.newTotal.toFixed(2)}
            </div>
          </>
        )}

        <div className="coupon-row">
          <input
            placeholder="Coupon code (e.g. WELCOME10)"
            value={coupon}
            onChange={(e) => { setCoupon(e.target.value.toUpperCase()); setCouponInfo(null); }}
          />
          <button className="btn-secondary" type="button" onClick={applyCoupon}>Apply</button>
        </div>
        <div className="coupon-hint">
          💡 Try <strong>WELCOME10</strong> (10% off) or <strong>IPL200</strong> (₹200 off over ₹1500)
        </div>

        {/* Shipping address */}
        <div className="address-form">
          <div className="address-form-title">Shipping address</div>
          <div className="address-grid">
            <input placeholder="Address line 1" value={address.line1} onChange={(e) => setField('line1', e.target.value)} />
            <input placeholder="City" value={address.city} onChange={(e) => setField('city', e.target.value)} />
            <input placeholder="State" value={address.state} onChange={(e) => setField('state', e.target.value)} />
            <input placeholder="Postal code" value={address.postalCode} onChange={(e) => setField('postalCode', e.target.value)} />
            <input placeholder="Country" value={address.country} onChange={(e) => setField('country', e.target.value)} />
          </div>
        </div>

        {!isAuthenticated && (
          <div className="guest-checkout-note">
            🔒 You can keep shopping as a guest — we'll ask you to log in only at checkout, and your cart will move with you.
          </div>
        )}

        <button className="checkout-btn" onClick={placeOrder} disabled={placing} id="place-order-btn">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-3px', marginRight: '8px' }}>
            <rect x="1" y="4" width="22" height="16" rx="2" ry="2"/>
            <line x1="1" y1="10" x2="23" y2="10"/>
          </svg>
          {placing ? 'Placing…' : isAuthenticated ? 'Place Order' : 'Login & Checkout'}
        </button>
      </div>
      {message && <p className="message error" style={{ marginTop: '16px' }}>{message}</p>}
    </div>
  );
}
