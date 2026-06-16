import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import type { ProductDetails, ProductReviews, ProductVariant, ReviewItem } from '../types';
import { resolveImage } from '../imageResolver';
import { useCart } from '../cart';
import { useAuth } from '../auth';

export function ProductDetailsPage() {
  const { slug } = useParams<{ slug: string }>();
  const { addItem } = useCart();
  const { isAuthenticated } = useAuth();
  const [product, setProduct] = useState<ProductDetails | null>(null);
  const [variant, setVariant] = useState<ProductVariant | null>(null);
  const [qty, setQty] = useState(1);
  const [message, setMessage] = useState('');
  const [messageType, setMessageType] = useState<'success' | 'error'>('success');

  // Reviews
  const [reviews, setReviews] = useState<ProductReviews | null>(null);
  const [showReviewForm, setShowReviewForm] = useState(false);
  const [reviewRating, setReviewRating] = useState(5);
  const [reviewTitle, setReviewTitle] = useState('');
  const [reviewBody, setReviewBody] = useState('');
  const [reviewError, setReviewError] = useState('');
  const [reviewBusy, setReviewBusy] = useState(false);

  // Wishlist
  const [inWishlist, setInWishlist] = useState(false);
  const [wishlistBusy, setWishlistBusy] = useState(false);

  const loadReviews = (productId: string) =>
    api.get<ProductReviews>(`/products/${productId}/reviews?page=1&pageSize=20`)
      .then(setReviews)
      .catch(() => setReviews(null));

  useEffect(() => {
    if (!slug) return;
    api.get<ProductDetails>(`/products/${slug}`).then((p) => {
      setProduct(p);
      setVariant(p.variants[0] ?? null);
      loadReviews(p.id);
    });
  }, [slug]);

  useEffect(() => {
    if (!isAuthenticated || !product) {
      setInWishlist(false);
      return;
    }
    api.get<Array<{ productId: string }>>('/wishlist')
      .then((items) => setInWishlist(items.some((i) => i.productId === product.id)))
      .catch(() => setInWishlist(false));
  }, [isAuthenticated, product]);

  const addToCart = async () => {
    if (!product || !variant) return;
    setMessage('');
    try {
      await addItem(
        {
          productId: product.id,
          productVariantId: variant.id,
          productName: product.name,
          imageUrl: product.imageUrl,
          unitPrice: variant.price.amount,
        },
        qty
      );
      setMessage('Added to cart ✓');
      setMessageType('success');
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Failed to add');
      setMessageType('error');
    }
  };

  const toggleWishlist = async () => {
    if (!product) return;
    setWishlistBusy(true);
    try {
      if (inWishlist) {
        await api.del(`/wishlist/${product.id}`);
        setInWishlist(false);
      } else {
        await api.post(`/wishlist/${product.id}`);
        setInWishlist(true);
      }
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : 'Wishlist update failed');
      setMessageType('error');
    } finally {
      setWishlistBusy(false);
    }
  };

  const submitReview = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!product) return;
    setReviewError('');
    setReviewBusy(true);
    try {
      await api.post(`/products/${product.id}/reviews`, {
        rating: reviewRating,
        title: reviewTitle.trim(),
        body: reviewBody.trim(),
      });
      setReviewTitle('');
      setReviewBody('');
      setReviewRating(5);
      setShowReviewForm(false);
      // Refresh product (averageRating denormalized) and reviews list.
      const refreshed = await api.get<ProductDetails>(`/products/${slug}`);
      setProduct(refreshed);
      await loadReviews(refreshed.id);
    } catch (err) {
      setReviewError(err instanceof ApiError ? err.message : 'Failed to submit review');
    } finally {
      setReviewBusy(false);
    }
  };

  const deleteReview = async (review: ReviewItem) => {
    if (!product) return;
    if (!confirm('Delete this review?')) return;
    try {
      await api.del(`/products/${product.id}/reviews/${review.id}`);
      const refreshed = await api.get<ProductDetails>(`/products/${slug}`);
      setProduct(refreshed);
      await loadReviews(refreshed.id);
    } catch (err) {
      setReviewError(err instanceof ApiError ? err.message : 'Failed to delete review');
    }
  };

  if (!product) return <div className="loading">Loading product…</div>;

  const img = resolveImage(product.name, product.typeName, product.franchise.shortCode);
  const selectedPrice = variant?.price.amount ?? product.basePrice.amount;

  return (
    <div className="details">
      {/* Breadcrumb */}
      <nav className="details-breadcrumb">
        <Link to="/">Products</Link>
        <span className="sep">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6"/>
          </svg>
        </span>
        <span>{product.typeName}</span>
        <span className="sep">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6"/>
          </svg>
        </span>
        <span style={{ color: 'var(--text-primary)' }}>{product.name}</span>
      </nav>

      {/* Two-column layout */}
      <div className="details-layout">
        {/* Left — Image */}
        <div className="details-image-wrap">
          {img ? (
            <img src={img} alt={product.name} />
          ) : (
            <div className="details-image-placeholder">🏏</div>
          )}
        </div>

        {/* Right — Info */}
        <div className="details-info">
          <div className="card-type">{product.typeName}</div>
          <h1>{product.name}</h1>

          <div className="details-franchise">
            <span className="badge">{product.franchise.shortCode}</span>
            <span style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>{product.franchise.name} · {product.franchise.city}</span>
          </div>

          {product.reviewCount > 0 && (
            <div className="details-rating">
              {'★'.repeat(Math.round(product.averageRating))}{'☆'.repeat(5 - Math.round(product.averageRating))}
              <span>{product.averageRating.toFixed(1)} ({product.reviewCount} reviews)</span>
            </div>
          )}

          <p className="details-desc">{product.description}</p>

          <div className="details-price-tag">
            <span className="currency">₹</span>{selectedPrice.toFixed(0)}
          </div>

          {/* Purchase Controls */}
          <div className="purchase">
            <label>
              Variant
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
              Qty
              <input type="number" min={1} max={10} value={qty}
                onChange={(e) => setQty(Math.max(1, Number(e.target.value)))}
                style={{ width: '70px' }} />
            </label>
            <button onClick={addToCart} disabled={!variant?.inStock} id="add-to-cart-btn">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-2px', marginRight: '6px' }}>
                <circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/>
                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"/>
              </svg>
              Add to cart
            </button>
            {isAuthenticated && (
              <button
                type="button"
                className={inWishlist ? 'btn-secondary wishlist-btn active' : 'btn-secondary wishlist-btn'}
                onClick={toggleWishlist}
                disabled={wishlistBusy}
                title={inWishlist ? 'Remove from wishlist' : 'Add to wishlist'}
              >
                <svg width="16" height="16" viewBox="0 0 24 24"
                  fill={inWishlist ? 'currentColor' : 'none'}
                  stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
                  style={{ verticalAlign: '-2px', marginRight: '6px' }}>
                  <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                </svg>
                {inWishlist ? 'In wishlist' : 'Wishlist'}
              </button>
            )}
          </div>

          {message && (
            <p className={`message ${messageType === 'error' ? 'error' : ''}`}>{message}</p>
          )}

          {/* Stock status */}
          {variant && (
            <div className={variant.inStock ? 'in-stock' : 'out-stock'}>
              {variant.inStock ? `In stock · ${variant.stockQuantity} available` : 'Currently out of stock'}
            </div>
          )}
        </div>
      </div>

      {/* Reviews section */}
      <section className="reviews-section">
        <div className="reviews-header">
          <h2>
            Customer Reviews
            {reviews && reviews.reviewCount > 0 && (
              <span className="reviews-aggregate">
                {' '}· {reviews.averageRating.toFixed(1)} ★ ({reviews.reviewCount})
              </span>
            )}
          </h2>
          {isAuthenticated && !showReviewForm && (
            <button className="btn-secondary" onClick={() => setShowReviewForm(true)}>
              Write a review
            </button>
          )}
        </div>

        {!isAuthenticated && (
          <p className="muted">
            <Link to="/login">Log in</Link> to write a review (verified buyers only).
          </p>
        )}

        {showReviewForm && (
          <form className="review-form" onSubmit={submitReview}>
            <label>
              Rating
              <div className="rating-input">
                {[1, 2, 3, 4, 5].map((n) => (
                  <button
                    key={n}
                    type="button"
                    className={`star-btn ${n <= reviewRating ? 'active' : ''}`}
                    onClick={() => setReviewRating(n)}
                    aria-label={`${n} star${n > 1 ? 's' : ''}`}
                  >
                    ★
                  </button>
                ))}
              </div>
            </label>
            <label>
              Title
              <input
                value={reviewTitle}
                onChange={(e) => setReviewTitle(e.target.value)}
                maxLength={120}
                placeholder="Summarise your experience"
                required
              />
            </label>
            <label>
              Review
              <textarea
                value={reviewBody}
                onChange={(e) => setReviewBody(e.target.value)}
                maxLength={2000}
                rows={4}
                placeholder="What did you like or dislike?"
                required
              />
            </label>
            {reviewError && <p className="message error">{reviewError}</p>}
            <div className="review-form-actions">
              <button type="button" className="btn-secondary" onClick={() => { setShowReviewForm(false); setReviewError(''); }}>
                Cancel
              </button>
              <button type="submit" disabled={reviewBusy}>
                {reviewBusy ? 'Submitting…' : 'Submit review'}
              </button>
            </div>
          </form>
        )}

        {reviews && reviews.reviews.length > 0 ? (
          <ul className="review-list">
            {reviews.reviews.map((r) => (
              <li className="review-item" key={r.id}>
                <div className="review-item-head">
                  <span className="review-rating">{'★'.repeat(r.rating)}{'☆'.repeat(5 - r.rating)}</span>
                  <strong>{r.title}</strong>
                  <span className="review-author">— {r.customerDisplayName}</span>
                  <span className="review-date">{new Date(r.createdAtUtc).toLocaleDateString('en-IN')}</span>
                  {isAuthenticated && (
                    <button className="link-btn danger" onClick={() => deleteReview(r)} title="Delete review">
                      ✕
                    </button>
                  )}
                </div>
                <p className="review-body">{r.body}</p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">No reviews yet. Be the first to share your thoughts.</p>
        )}
      </section>
    </div>
  );
}

