import { useEffect, useState, useRef, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import type { FeaturedProduct, Franchise, ProductListItem, SearchResult } from '../types';
import { resolveImage } from '../imageResolver';
import { useCart, type AddItemInput } from '../cart';

import heroImg from '../assets/hero.jpg';

const PRODUCT_TYPES = ['Jersey', 'Cap', 'Flag', 'AutographedPhoto', 'Accessory', 'Memorabilia'];
const DEBOUNCE_MS = 350;

// Builds the add-to-cart payload from a list item using its first in-stock variant.
function defaultAddInput(p: ProductListItem): AddItemInput | null {
  if (!p.defaultVariantId) return null;
  const variant = p.variants.find((v) => v.id === p.defaultVariantId);
  return {
    productId: p.id,
    productVariantId: p.defaultVariantId,
    productName: p.name,
    imageUrl: p.imageUrl,
    unitPrice: variant ? variant.price.amount : p.basePrice.amount,
  };
}

// Maps a featured badge to a colour class.
function badgeClass(badge: string): string {
  switch (badge) {
    case 'Bestseller': return 'badge-bestseller';
    case 'Trending': return 'badge-trending';
    case 'Top Rated': return 'badge-toprated';
    case 'New Arrival': return 'badge-new';
    default: return 'badge-discover';
  }
}

function StarRating({ rating, count }: { rating: number; count: number }) {
  const full = Math.floor(rating);
  const half = rating - full >= 0.5;
  const stars: string[] = [];
  for (let i = 0; i < 5; i++) {
    if (i < full) stars.push('★');
    else if (i === full && half) stars.push('★');
    else stars.push('☆');
  }
  return (
    <span className="card-rating">
      {stars.join('')} <span className="star-count">({count})</span>
    </span>
  );
}

export function ProductListPage() {
  const [result, setResult] = useState<SearchResult | null>(null);
  const [franchises, setFranchises] = useState<Franchise[]>([]);
  const [featured, setFeatured] = useState<FeaturedProduct[]>([]);
  const [q, setQ] = useState('');
  const [franchise, setFranchise] = useState('');
  const [type, setType] = useState('');
  const [loading, setLoading] = useState(false);
  const [addedId, setAddedId] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const { addItem } = useCart();

  // Track latest search params to avoid stale closures
  const latestSearch = useRef({ q: '', franchise: '', type: '' });
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Featured rail is a discovery aid — only meaningful before the shopper narrows things down.
  const isBrowsing = !q && !franchise && !type;
  const hasActiveFilters = !!q || !!franchise || !!type;

  useEffect(() => {
    api.get<Franchise[]>('/franchises').then(setFranchises).catch(() => {});
    api.get<FeaturedProduct[]>('/products/featured?count=8').then(setFeatured).catch(() => {});
  }, []);

  const quickAdd = async (p: ProductListItem) => {
    const input = defaultAddInput(p);
    if (!input) return;
    await addItem(input, 1);
    setAddedId(p.id);
    window.setTimeout(() => setAddedId((cur) => (cur === p.id ? null : cur)), 1500);
  };

  const doSearch = useCallback(async (searchQ: string, searchFranchise: string, searchType: string) => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (searchQ) params.set('q', searchQ);
      if (searchFranchise) params.set('franchise', searchFranchise);
      if (searchType) params.set('type', searchType);
      params.set('pageSize', '24');
      const res = await api.get<SearchResult>(`/products/search?${params.toString()}`);
      // Only apply if this is still the latest search
      if (latestSearch.current.q === searchQ &&
          latestSearch.current.franchise === searchFranchise &&
          latestSearch.current.type === searchType) {
        setResult(res);
        setHasSearched(true);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  // Debounced auto-search: fires whenever q, franchise, or type changes
  useEffect(() => {
    latestSearch.current = { q, franchise, type };

    if (debounceTimer.current) clearTimeout(debounceTimer.current);

    // Dropdown changes (franchise/type) fire immediately; text input is debounced
    const delay = q !== '' ? DEBOUNCE_MS : 0;

    debounceTimer.current = setTimeout(() => {
      void doSearch(q, franchise, type);
    }, delay);

    return () => {
      if (debounceTimer.current) clearTimeout(debounceTimer.current);
    };
  }, [q, franchise, type, doSearch]);

  const clearAllFilters = () => {
    setQ('');
    setFranchise('');
    setType('');
  };

  return (
    <div>
      {/* Hero Banner */}
      <section className="hero" id="hero-banner">
        <img src={heroImg} alt="IPL Cricket Merchandise" className="hero-img" />
        <div className="hero-overlay" />
        <div className="hero-content">
          <h1>Gear Up for the Season</h1>
          <p>Official jerseys, caps, flags &amp; collectibles from all your favourite IPL franchises.</p>
        </div>
      </section>

      {/* Features Marquee */}
      <div className="features-marquee">
        <div className="features-marquee-content">
          <span>★ OFFICIAL MERCHANDISE</span>
          <span>★ PREMIUM QUALITY</span>
          <span>★ SECURE CHECKOUT</span>
          <span>★ FAST SHIPPING</span>
          <span>★ AUTHENTIC GEAR</span>
          <span aria-hidden="true">★ OFFICIAL MERCHANDISE</span>
          <span aria-hidden="true">★ PREMIUM QUALITY</span>
          <span aria-hidden="true">★ SECURE CHECKOUT</span>
          <span aria-hidden="true">★ FAST SHIPPING</span>
          <span aria-hidden="true">★ AUTHENTIC GEAR</span>
        </div>
      </div>

      {/* Featured / Trending rail — demand-ranked, with every category represented */}
      {isBrowsing && featured.length > 0 && (
        <section className="featured" id="featured-rail">
          <div className="featured-head">
            <h2>🔥 Trending &amp; Featured</h2>
            <span className="featured-sub">Ranked by demand — every category represented</span>
          </div>
          <div className="featured-rail-wrapper">
            <div className="featured-rail">
              {/* Duplicate featured array for seamless infinite marquee scroll */}
              {[...featured, ...featured].map((f, i) => {
                const p = f.product;
                const img = resolveImage(p.name, p.typeName, p.franchiseShortCode);
                // use combination of id and index for key due to duplication
                return (
                  <Link to={`/products/${p.slug}`} key={`${p.id}-${i}`} className="featured-card">
                    <span className={`feat-badge ${badgeClass(f.badge)}`}>{f.badge}</span>
                    <div className="featured-card-image">
                      {img ? <img src={img} alt={p.name} loading="lazy" /> : <div className="card-image-placeholder">🏏</div>}
                    </div>
                    <div className="featured-card-body">
                      <div className="card-type">{p.typeName}</div>
                      <h4>{p.name}</h4>
                      <div className="feat-reason">{f.reason}</div>
                      <div className="card-bottom">
                        <div className="card-price"><span className="currency">₹</span>{p.basePrice.amount.toFixed(0)}</div>
                        <button
                          type="button"
                          className="add-btn compact"
                          disabled={!p.inStock}
                          onClick={(e) => { e.preventDefault(); e.stopPropagation(); void quickAdd(p); }}
                        >
                          {addedId === p.id ? 'Added ✓' : 'Add'}
                        </button>
                      </div>
                    </div>
                  </Link>
                );
              })}
            </div>
          </div>
        </section>
      )}

      {/* Search */}
      <div className="search-bar" id="product-search">
        <input
          placeholder="Search by name, type, franchise, city…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <select value={franchise} onChange={(e) => setFranchise(e.target.value)}>
          <option value="">All franchises</option>
          {franchises.map((f) => (
            <option key={f.id} value={f.shortCode}>{f.name}</option>
          ))}
        </select>
        <select value={type} onChange={(e) => setType(e.target.value)}>
          <option value="">All types</option>
          {PRODUCT_TYPES.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        {hasActiveFilters && (
          <button className="clear-filters-btn" onClick={clearAllFilters} title="Clear all filters">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
            Clear
          </button>
        )}
      </div>

      {/* Active filter chips */}
      {hasActiveFilters && (
        <div className="active-filters" id="active-filters">
          {q && (
            <span className="filter-chip">
              <span className="filter-chip-label">Search:</span> {q}
              <button className="chip-remove" onClick={() => setQ('')} aria-label="Remove search filter">×</button>
            </span>
          )}
          {franchise && (
            <span className="filter-chip">
              <span className="filter-chip-label">Franchise:</span> {franchises.find(f => f.shortCode === franchise)?.name || franchise}
              <button className="chip-remove" onClick={() => setFranchise('')} aria-label="Remove franchise filter">×</button>
            </span>
          )}
          {type && (
            <span className="filter-chip">
              <span className="filter-chip-label">Type:</span> {type}
              <button className="chip-remove" onClick={() => setType('')} aria-label="Remove type filter">×</button>
            </span>
          )}
        </div>
      )}

      {/* Result count */}
      {result && (
        <div className="results-info">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/>
            <line x1="7" y1="7" x2="7.01" y2="7"/>
          </svg>
          {result.totalCount} product{result.totalCount !== 1 ? 's' : ''} found
          {result.facets.franchises.length > 0 && (
            <> · {result.facets.franchises[0].label} ({result.facets.franchises[0].count})</>
          )}
        </div>
      )}

      {/* Product Grid */}
      {loading ? (
        <div className="loading">Loading products…</div>
      ) : hasSearched && result && result.items.length === 0 ? (
        <div className="no-results" id="no-results">
          <div className="no-results-icon">🔍</div>
          <h3>No products found</h3>
          <p>
            We couldn't find any products matching your search.
            {q && <> Try a simpler term like <strong>"Jersey"</strong> or <strong>"Cap"</strong>.</>}
          </p>
          <div className="no-results-suggestions">
            <span>Suggestions:</span>
            <ul>
              <li>Check the spelling of your search term</li>
              <li>Try broader keywords (e.g., "Jersey" instead of a long phrase)</li>
              <li>Remove some filters to expand results</li>
              <li>Search by franchise name or city (e.g., "Mumbai", "Chennai")</li>
            </ul>
          </div>
          {hasActiveFilters && (
            <button className="clear-filters-btn large" onClick={clearAllFilters}>
              Clear all filters
            </button>
          )}
        </div>
      ) : (
        <div className="grid" id="product-grid">
          {result?.items.map((p) => {
            const img = resolveImage(p.name, p.typeName, p.franchiseShortCode);
            return (
              <Link to={`/products/${p.slug}`} key={p.id} className="card">
                <div className="card-image-wrap">
                  {img ? (
                    <img src={img} alt={p.name} loading="lazy" />
                  ) : (
                    <div className="card-image-placeholder">🏏</div>
                  )}
                </div>
                <div className="card-body">
                  <div className="card-type">{p.typeName}</div>
                  <h3>{p.name}</h3>
                  <div className="card-meta">
                    <span className="badge">{p.franchiseShortCode}</span>
                    {p.reviewCount > 0 && <StarRating rating={p.averageRating} count={p.reviewCount} />}
                  </div>
                  <div className="card-bottom">
                    <div className="card-price">
                      <span className="currency">₹</span>{p.basePrice.amount.toFixed(0)}
                    </div>
                    <div className={p.inStock ? 'in-stock' : 'out-stock'}>
                      {p.inStock ? 'In stock' : 'Out of stock'}
                    </div>
                  </div>
                  <button
                    type="button"
                    className="add-btn"
                    disabled={!p.inStock}
                    onClick={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      void quickAdd(p);
                    }}
                  >
                    {addedId === p.id ? (
                      'Added ✓'
                    ) : (
                      <>
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ verticalAlign: '-2px', marginRight: '5px' }}>
                          <circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/>
                          <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"/>
                        </svg>
                        Add to cart
                      </>
                    )}
                  </button>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
