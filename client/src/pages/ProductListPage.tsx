import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import type { Franchise, SearchResult } from '../types';

const PRODUCT_TYPES = ['Jersey', 'Cap', 'Flag', 'AutographedPhoto', 'Accessory', 'Memorabilia'];

export function ProductListPage() {
  const [result, setResult] = useState<SearchResult | null>(null);
  const [franchises, setFranchises] = useState<Franchise[]>([]);
  const [q, setQ] = useState('');
  const [franchise, setFranchise] = useState('');
  const [type, setType] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    api.get<Franchise[]>('/franchises').then(setFranchises).catch(() => {});
  }, []);

  const search = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (q) params.set('q', q);
      if (franchise) params.set('franchise', franchise);
      if (type) params.set('type', type);
      params.set('pageSize', '24');
      setResult(await api.get<SearchResult>(`/products/search?${params.toString()}`));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    search();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div>
      <h1>Merchandise</h1>
      <div className="search-bar">
        <input
          placeholder="Search jerseys, caps, flags…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && search()}
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
        <button onClick={search}>Search</button>
      </div>

      {result && (
        <p className="muted">
          {result.totalCount} product(s)
          {result.facets.franchises.length > 0 && (
            <> · top franchise: {result.facets.franchises[0].label} ({result.facets.franchises[0].count})</>
          )}
        </p>
      )}

      {loading ? (
        <p>Loading…</p>
      ) : (
        <div className="grid">
          {result?.items.map((p) => (
            <Link to={`/products/${p.slug}`} key={p.id} className="card">
              <div className="card-type">{p.typeName}</div>
              <h3>{p.name}</h3>
              <div className="card-meta">
                <span className="badge">{p.franchiseShortCode}</span>
                {p.reviewCount > 0 && <span>★ {p.averageRating.toFixed(1)}</span>}
              </div>
              <div className="card-price">₹{p.basePrice.amount.toFixed(0)}</div>
              <div className={p.inStock ? 'in-stock' : 'out-stock'}>
                {p.inStock ? 'In stock' : 'Out of stock'}
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
