import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import { useAuth } from '../auth';
import type { ProductDetails, ProductVariant } from '../types';

export function ProductDetailsPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const [product, setProduct] = useState<ProductDetails | null>(null);
  const [variant, setVariant] = useState<ProductVariant | null>(null);
  const [qty, setQty] = useState(1);
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!slug) return;
    api.get<ProductDetails>(`/products/${slug}`).then((p) => {
      setProduct(p);
      setVariant(p.variants[0] ?? null);
    });
  }, [slug]);

  const addToCart = async () => {
    if (!isAuthenticated) {
      navigate('/login');
      return;
    }
    if (!variant) return;
    setMessage('');
    try {
      await api.post('/cart/items', { productVariantId: variant.id, quantity: qty });
      setMessage('Added to cart ✓');
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Failed to add');
    }
  };

  if (!product) return <p>Loading…</p>;

  return (
    <div className="details">
      <button className="link-btn" onClick={() => navigate(-1)}>← Back</button>
      <h1>{product.name}</h1>
      <div className="badge">{product.franchise.name}</div>
      <span className="card-type"> {product.typeName}</span>
      <p>{product.description}</p>
      {product.reviewCount > 0 && (
        <p className="muted">★ {product.averageRating.toFixed(1)} ({product.reviewCount} reviews)</p>
      )}

      <div className="purchase">
        <label>
          Variant:
          <select
            value={variant?.id ?? ''}
            onChange={(e) => setVariant(product.variants.find((v) => v.id === e.target.value) ?? null)}
          >
            {product.variants.map((v) => (
              <option key={v.id} value={v.id} disabled={!v.inStock}>
                {[v.size, v.color].filter(Boolean).join(' / ') || v.sku} — ₹{v.price.amount.toFixed(0)}
                {v.inStock ? '' : ' (out of stock)'}
              </option>
            ))}
          </select>
        </label>
        <label>
          Qty:
          <input type="number" min={1} max={10} value={qty}
            onChange={(e) => setQty(Math.max(1, Number(e.target.value)))} />
        </label>
        <button onClick={addToCart} disabled={!variant?.inStock}>Add to cart</button>
      </div>
      {message && <p className="message">{message}</p>}
    </div>
  );
}
