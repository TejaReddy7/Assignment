import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import type { OrderDetails, OrderSummary, Paged } from '../types';

export function OrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [selected, setSelected] = useState<OrderDetails | null>(null);

  useEffect(() => {
    api.get<Paged<OrderSummary>>('/orders').then((p) => setOrders(p.items)).catch(() => {});
  }, []);

  const open = async (orderNumber: string) => {
    if (selected?.orderNumber === orderNumber) {
      setSelected(null);
      return;
    }
    setSelected(await api.get<OrderDetails>(`/orders/${orderNumber}`));
  };

  const cancel = async (e: React.MouseEvent, orderNumber: string) => {
    e.stopPropagation();
    await api.post(`/orders/${orderNumber}/cancel`, { reason: 'Cancelled from UI' });
    const refreshed = await api.get<Paged<OrderSummary>>('/orders');
    setOrders(refreshed.items);
    setSelected(null);
  };

  const formatDate = (dateStr: string) => {
    try {
      return new Date(dateStr).toLocaleDateString('en-IN', {
        day: 'numeric', month: 'short', year: 'numeric',
      });
    } catch {
      return dateStr;
    }
  };

  // Empty state
  if (orders.length === 0) {
    return (
      <div className="empty-state" id="empty-orders">
        <div className="empty-state-icon">📦</div>
        <h2>No orders yet</h2>
        <p>Your order history will appear here after your first purchase.</p>
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
    <div className="orders-page" id="orders-page">
      <h1>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-4px', marginRight: '10px', opacity: 0.6 }}>
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
          <polyline points="14 2 14 8 20 8"/>
          <line x1="16" y1="13" x2="8" y2="13"/>
          <line x1="16" y1="17" x2="8" y2="17"/>
        </svg>
        Order History
      </h1>

      <div className="order-cards">
        {orders.map((o) => (
          <div key={o.id}>
            <div
              className="order-card"
              onClick={() => open(o.orderNumber)}
              role="button"
              tabIndex={0}
            >
              <div className="order-card-left">
                <div className="order-card-number">{o.orderNumber}</div>
                <div className="order-card-date">{formatDate(o.placedAtUtc)}</div>
              </div>
              <div className="order-card-center">
                <span className={`status-badge status-${o.statusName}`}>{o.statusName}</span>
              </div>
              <div className="order-card-right">
                <div className="order-card-total">₹{o.total.toFixed(2)}</div>
                <div className="order-card-items">{o.itemCount} item{o.itemCount !== 1 ? 's' : ''}</div>
              </div>
              <div className="order-card-actions">
                {(o.statusName === 'Pending' || o.statusName === 'Confirmed') && (
                  <button className="btn-danger" onClick={(e) => cancel(e, o.orderNumber)}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-2px', marginRight: '4px' }}>
                      <circle cx="12" cy="12" r="10"/>
                      <line x1="15" y1="9" x2="9" y2="15"/>
                      <line x1="9" y1="9" x2="15" y2="15"/>
                    </svg>
                    Cancel
                  </button>
                )}
              </div>
            </div>

            {/* Expanded detail */}
            {selected?.orderNumber === o.orderNumber && (
              <div className="order-detail">
                <h2>
                  {selected.orderNumber}
                  <span className={`status-badge status-${selected.statusName}`} style={{ marginLeft: '12px', verticalAlign: '2px' }}>
                    {selected.statusName}
                  </span>
                </h2>
                <div className="order-detail-items">
                  {selected.items.map((item, idx) => (
                    <div className="order-detail-item" key={idx}>
                      <span className="order-detail-item-name">{item.productName}</span>
                      <span className="order-detail-item-qty">({item.sku}) × {item.quantity}</span>
                      <span className="order-detail-item-price">₹{item.lineTotal.toFixed(0)}</span>
                    </div>
                  ))}
                </div>
                <dl className="order-detail-summary">
                  <dt>Subtotal</dt>
                  <dd>₹{selected.subtotal.toFixed(2)}</dd>
                  <dt>Discount</dt>
                  <dd style={{ color: selected.discountAmount > 0 ? 'var(--accent-green)' : undefined }}>
                    {selected.discountAmount > 0 ? `−₹${selected.discountAmount.toFixed(2)}` : '₹0.00'}
                  </dd>
                  <dt>Shipping</dt>
                  <dd>₹{selected.shippingFee.toFixed(2)}</dd>
                  <dt className="total-label">Total</dt>
                  <dd className="total-value">₹{selected.total.toFixed(2)}</dd>
                </dl>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
