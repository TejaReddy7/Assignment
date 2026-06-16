import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../api';
import type { WishlistItem } from '../types';
import { resolveImage } from '../imageResolver';

export function WishlistPage() {
  const [items, setItems] = useState<WishlistItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setItems(await api.get<WishlistItem[]>('/wishlist'));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to load wishlist');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const remove = async (productId: string) => {
    try {
      await api.del(`/wishlist/${productId}`);
      setItems((cur) => cur.filter((i) => i.productId !== productId));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to remove');
    }
  };

  if (loading) return <div className="loading">Loading wishlist…</div>;

  if (items.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state-icon">♡</div>
        <h2>Your wishlist is empty</h2>
        <p>Browse products and tap the heart to save them here.</p>
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
    <div className="wishlist-page">
      <h1>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-4px', marginRight: '10px', opacity: 0.6 }}>
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
        </svg>
        My Wishlist
      </h1>
      {error && <div className="message error">{error}</div>}

      <div className="wishlist-grid">
        {items.map((item) => {
          const img = resolveImage(item.name, '', item.franchiseShortCode);
          return (
            <div className="wishlist-card" key={item.productId}>
              <Link to={`/products/${item.slug}`} className="wishlist-card-image">
                {img ? <img src={img} alt={item.name} /> : <div className="wishlist-card-placeholder">🏏</div>}
              </Link>
              <div className="wishlist-card-body">
                <span className="badge">{item.franchiseShortCode}</span>
                <Link to={`/products/${item.slug}`} className="wishlist-card-name">{item.name}</Link>
                <div className="wishlist-card-price">₹{item.price.toFixed(0)}</div>
                <div className={item.inStock ? 'in-stock' : 'out-stock'}>
                  {item.inStock ? 'In stock' : 'Out of stock'}
                </div>
                <div className="wishlist-card-actions">
                  <Link className="btn-secondary" to={`/products/${item.slug}`}>View</Link>
                  <button className="btn-danger" onClick={() => remove(item.productId)}>Remove</button>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
