import { useEffect, useState } from 'react';
import { api, ApiError } from '../api';
import type { Cart, OrderDetails } from '../types';

export function CartPage() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [coupon, setCoupon] = useState('');
  const [placedOrder, setPlacedOrder] = useState<OrderDetails | null>(null);
  const [message, setMessage] = useState('');

  const load = () => api.get<Cart>('/cart').then(setCart).catch(() => {});

  useEffect(() => { load(); }, []);

  const updateQty = async (itemId: string, quantity: number) => {
    try {
      setCart(await api.patch<Cart>(`/cart/items/${itemId}`, { quantity }));
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Update failed');
    }
  };

  const removeItem = async (itemId: string) => {
    setCart(await api.del<Cart>(`/cart/items/${itemId}`));
  };

  const placeOrder = async () => {
    setMessage('');
    try {
      const order = await api.post<OrderDetails>(
        '/orders',
        {
          shippingAddress: {
            line1: '123 Fan Street', city: 'Mumbai', state: 'Maharashtra',
            postalCode: '400001', country: 'India',
          },
          paymentMethod: 3,
          couponCode: coupon || null,
        },
        { 'Idempotency-Key': crypto.randomUUID() }
      );
      setPlacedOrder(order);
      await load();
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Checkout failed');
    }
  };

  if (placedOrder) {
    return (
      <div>
        <h1>Order placed ✓</h1>
        <p>Order <strong>{placedOrder.orderNumber}</strong> — {placedOrder.statusName}</p>
        <p>Total paid: ₹{placedOrder.total.toFixed(2)} {placedOrder.currency}
          {placedOrder.discountAmount > 0 && <> (saved ₹{placedOrder.discountAmount.toFixed(2)})</>}
        </p>
        <a href="/orders">View order history →</a>
      </div>
    );
  }

  if (!cart || cart.items.length === 0) return <div><h1>Your Cart</h1><p>Cart is empty.</p></div>;

  return (
    <div>
      <h1>Your Cart</h1>
      <table className="table">
        <thead>
          <tr><th>Product</th><th>Unit</th><th>Qty</th><th>Line</th><th></th></tr>
        </thead>
        <tbody>
          {cart.items.map((i) => (
            <tr key={i.id}>
              <td>{i.productName}</td>
              <td>₹{i.unitPrice.toFixed(0)}</td>
              <td>
                <input type="number" min={1} max={10} value={i.quantity}
                  onChange={(e) => updateQty(i.id, Math.max(1, Number(e.target.value)))} style={{ width: 56 }} />
              </td>
              <td>₹{i.lineTotal.toFixed(0)}</td>
              <td><button className="link-btn" onClick={() => removeItem(i.id)}>Remove</button></td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="checkout">
        <div className="subtotal">Subtotal: ₹{cart.subtotal.toFixed(2)}</div>
        <div className="coupon-row">
          <input placeholder="Coupon (e.g. WELCOME10)" value={coupon}
            onChange={(e) => setCoupon(e.target.value.toUpperCase())} />
          <button onClick={placeOrder}>Place order</button>
        </div>
        <p className="muted">Try coupons WELCOME10 (10% off) or IPL200 (₹200 off over ₹1500).</p>
      </div>
      {message && <p className="message error">{message}</p>}
    </div>
  );
}
