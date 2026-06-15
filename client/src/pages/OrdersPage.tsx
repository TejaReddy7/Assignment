import { useEffect, useState } from 'react';
import { api } from '../api';
import type { OrderDetails, OrderSummary, Paged } from '../types';

export function OrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [selected, setSelected] = useState<OrderDetails | null>(null);

  useEffect(() => {
    api.get<Paged<OrderSummary>>('/orders').then((p) => setOrders(p.items)).catch(() => {});
  }, []);

  const open = async (orderNumber: string) => {
    setSelected(await api.get<OrderDetails>(`/orders/${orderNumber}`));
  };

  const cancel = async (orderNumber: string) => {
    await api.post(`/orders/${orderNumber}/cancel`, { reason: 'Cancelled from UI' });
    const refreshed = await api.get<Paged<OrderSummary>>('/orders');
    setOrders(refreshed.items);
    setSelected(null);
  };

  return (
    <div>
      <h1>Order History</h1>
      {orders.length === 0 ? (
        <p>No orders yet.</p>
      ) : (
        <table className="table">
          <thead>
            <tr><th>Order</th><th>Status</th><th>Items</th><th>Total</th><th></th></tr>
          </thead>
          <tbody>
            {orders.map((o) => (
              <tr key={o.id}>
                <td><button className="link-btn" onClick={() => open(o.orderNumber)}>{o.orderNumber}</button></td>
                <td>{o.statusName}</td>
                <td>{o.itemCount}</td>
                <td>₹{o.total.toFixed(2)}</td>
                <td>
                  {(o.statusName === 'Pending' || o.statusName === 'Confirmed') && (
                    <button className="link-btn" onClick={() => cancel(o.orderNumber)}>Cancel</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected && (
        <div className="order-detail">
          <h2>{selected.orderNumber} — {selected.statusName}</h2>
          <ul>
            {selected.items.map((i, idx) => (
              <li key={idx}>{i.productName} ({i.sku}) × {i.quantity} = ₹{i.lineTotal.toFixed(0)}</li>
            ))}
          </ul>
          <p>
            Subtotal ₹{selected.subtotal.toFixed(2)} · Discount ₹{selected.discountAmount.toFixed(2)} ·
            Shipping ₹{selected.shippingFee.toFixed(2)} · <strong>Total ₹{selected.total.toFixed(2)}</strong>
          </p>
        </div>
      )}
    </div>
  );
}
