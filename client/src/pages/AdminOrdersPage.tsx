import { useEffect, useState } from 'react';
import { api, ApiError } from '../api';
import type { OrderDetails, OrderSummary, Paged } from '../types';

const STATUS_FILTERS = ['All', 'Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled'] as const;
type StatusFilter = typeof STATUS_FILTERS[number];

export function AdminOrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [selected, setSelected] = useState<OrderDetails | null>(null);
  const [filter, setFilter] = useState<StatusFilter>('All');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  const load = async (status: StatusFilter) => {
    const query = status === 'All' ? '' : `&status=${status}`;
    const page = await api.get<Paged<OrderSummary>>(`/orders?page=1&pageSize=50${query}`);
    setOrders(page.items);
  };

  useEffect(() => {
    load(filter).catch((e) => setError(e instanceof ApiError ? e.message : 'Failed to load orders'));
  }, [filter]);

  const flash = (msg: string) => {
    setMessage(msg);
    setError('');
    window.setTimeout(() => setMessage((m) => (m === msg ? '' : m)), 2500);
  };

  const open = async (orderNumber: string) => {
    if (selected?.orderNumber === orderNumber) {
      setSelected(null);
      return;
    }
    setSelected(await api.get<OrderDetails>(`/orders/${orderNumber}`));
  };

  const transition = async (
    e: React.MouseEvent,
    orderNumber: string,
    action: 'ship' | 'deliver' | 'cancel',
  ) => {
    e.stopPropagation();
    setBusy(orderNumber + ':' + action);
    setError('');
    try {
      if (action === 'cancel') {
        await api.post(`/orders/${orderNumber}/cancel`, { reason: 'Cancelled by admin' });
      } else {
        await api.post(`/orders/${orderNumber}/${action}`);
      }
      flash(`Order ${orderNumber} → ${action === 'ship' ? 'Shipped' : action === 'deliver' ? 'Delivered' : 'Cancelled'} ✓`);
      await load(filter);
      if (selected?.orderNumber === orderNumber) {
        setSelected(await api.get<OrderDetails>(`/orders/${orderNumber}`));
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Status update failed');
    } finally {
      setBusy(null);
    }
  };

  const formatDate = (s: string) => {
    try {
      return new Date(s).toLocaleString('en-IN', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch {
      return s;
    }
  };

  const renderActions = (o: OrderSummary) => {
    const key = (action: string) => `${o.orderNumber}:${action}`;
    return (
      <div className="order-card-actions">
        {o.statusName === 'Confirmed' && (
          <button className="btn-secondary" disabled={busy === key('ship')} onClick={(e) => transition(e, o.orderNumber, 'ship')}>
            {busy === key('ship') ? '…' : 'Ship'}
          </button>
        )}
        {o.statusName === 'Shipped' && (
          <button className="btn-secondary" disabled={busy === key('deliver')} onClick={(e) => transition(e, o.orderNumber, 'deliver')}>
            {busy === key('deliver') ? '…' : 'Mark delivered'}
          </button>
        )}
        {(o.statusName === 'Pending' || o.statusName === 'Confirmed') && (
          <button className="btn-danger" disabled={busy === key('cancel')} onClick={(e) => transition(e, o.orderNumber, 'cancel')}>
            {busy === key('cancel') ? '…' : 'Cancel'}
          </button>
        )}
      </div>
    );
  };

  return (
    <div className="admin-page">
      <div className="admin-header">
        <h1>⚙ Order Management</h1>
      </div>
      <p className="muted">{orders.length} order{orders.length === 1 ? '' : 's'} shown ({filter}).</p>

      <div className="admin-filter-bar">
        {STATUS_FILTERS.map((s) => (
          <button
            key={s}
            className={s === filter ? 'filter-chip active' : 'filter-chip'}
            onClick={() => { setFilter(s); setSelected(null); }}
          >
            {s}
          </button>
        ))}
      </div>

      {message && <div className="message">{message}</div>}
      {error && <div className="message error">{error}</div>}

      {orders.length === 0 ? (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <div className="empty-state-icon">📦</div>
          <h2>No orders match this filter</h2>
        </div>
      ) : (
        <div className="order-cards">
          {orders.map((o) => (
            <div key={o.id}>
              <div className="order-card" onClick={() => open(o.orderNumber)} role="button" tabIndex={0}>
                <div className="order-card-left">
                  <div className="order-card-number">{o.orderNumber}</div>
                  <div className="order-card-date">{formatDate(o.placedAtUtc)}</div>
                  {o.customerEmail && (
                    <div className="order-card-customer">{o.customerName ?? '—'} · {o.customerEmail}</div>
                  )}
                </div>
                <div className="order-card-center">
                  <span className={`status-badge status-${o.statusName}`}>{o.statusName}</span>
                </div>
                <div className="order-card-right">
                  <div className="order-card-total">₹{o.total.toFixed(2)}</div>
                  <div className="order-card-items">{o.itemCount} item{o.itemCount !== 1 ? 's' : ''}</div>
                </div>
                {renderActions(o)}
              </div>

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
                    <dd>{selected.discountAmount > 0 ? `−₹${selected.discountAmount.toFixed(2)}` : '₹0.00'}</dd>
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
      )}
    </div>
  );
}
