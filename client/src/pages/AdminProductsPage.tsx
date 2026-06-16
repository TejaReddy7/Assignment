import { useEffect, useState } from 'react';
import { api, ApiError } from '../api';
import type { Franchise, Paged, ProductListItem } from '../types';

const PRODUCT_TYPES: { value: number; label: string }[] = [
  { value: 1, label: 'Jersey' },
  { value: 2, label: 'Cap' },
  { value: 3, label: 'Flag' },
  { value: 4, label: 'AutographedPhoto' },
  { value: 5, label: 'Accessory' },
  { value: 6, label: 'Memorabilia' },
];

interface VariantDraft {
  sku: string;
  size: string;
  color: string;
  stock: number;
}

interface CreateForm {
  name: string;
  description: string;
  type: number;
  franchiseId: string;
  basePrice: number;
  imageUrl: string;
  variants: VariantDraft[];
}

const emptyForm = (franchiseId = ''): CreateForm => ({
  name: '',
  description: '',
  type: 1,
  franchiseId,
  basePrice: 999,
  imageUrl: '',
  variants: [{ sku: '', size: '', color: '', stock: 50 }],
});

interface EditState {
  id: string;
  name: string;
  description: string;
  basePrice: number;
  imageUrl: string;
}

export function AdminProductsPage() {
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [franchises, setFranchises] = useState<Franchise[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState<CreateForm>(emptyForm());
  const [edit, setEdit] = useState<EditState | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const loadProducts = async () => {
    const page = await api.get<Paged<ProductListItem>>('/products?page=1&pageSize=100&sortBy=newest&sortDir=desc');
    setProducts(page.items);
    setTotalCount(page.totalCount);
  };

  useEffect(() => {
    api.get<Franchise[]>('/franchises').then((f) => {
      setFranchises(f);
      setForm((cur) => ({ ...cur, franchiseId: cur.franchiseId || f[0]?.id || '' }));
    }).catch(() => {});
    loadProducts().catch(() => {});
  }, []);

  const flash = (msg: string) => {
    setMessage(msg);
    setError('');
    window.setTimeout(() => setMessage((m) => (m === msg ? '' : m)), 2500);
  };

  // ---- Create ----
  const setFormField = <K extends keyof CreateForm>(field: K, value: CreateForm[K]) =>
    setForm((f) => ({ ...f, [field]: value }));

  const setVariant = (idx: number, field: keyof VariantDraft, value: string | number) =>
    setForm((f) => ({
      ...f,
      variants: f.variants.map((v, i) => (i === idx ? { ...v, [field]: value } : v)),
    }));

  const addVariantRow = () =>
    setForm((f) => ({ ...f, variants: [...f.variants, { sku: '', size: '', color: '', stock: 50 }] }));

  const removeVariantRow = (idx: number) =>
    setForm((f) => ({ ...f, variants: f.variants.filter((_, i) => i !== idx) }));

  const createProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      await api.post('/products', {
        name: form.name,
        description: form.description,
        type: form.type,
        franchiseId: form.franchiseId,
        basePrice: Number(form.basePrice),
        imageUrl: form.imageUrl || null,
        variants: form.variants
          .filter((v) => v.sku.trim())
          .map((v) => ({
            sku: v.sku.trim(),
            size: v.size.trim() || null,
            color: v.color.trim() || null,
            stock: Number(v.stock),
            priceOverride: null,
          })),
      });
      flash('Product created ✓');
      setForm(emptyForm(franchises[0]?.id));
      setShowCreate(false);
      await loadProducts();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  };

  // ---- Edit ----
  const startEdit = (p: ProductListItem) =>
    setEdit({
      id: p.id,
      name: p.name,
      description: '',
      basePrice: p.basePrice.amount,
      imageUrl: p.imageUrl ?? '',
    });

  const saveEdit = async () => {
    if (!edit) return;
    setError('');
    setBusy(true);
    try {
      await api.put(`/products/${edit.id}`, {
        id: edit.id,
        name: edit.name,
        description: edit.description || 'Updated via admin.',
        basePrice: Number(edit.basePrice),
        imageUrl: edit.imageUrl || null,
      });
      flash('Product updated ✓');
      setEdit(null);
      await loadProducts();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Update failed');
    } finally {
      setBusy(false);
    }
  };

  // ---- Delete ----
  const deleteProduct = async (p: ProductListItem) => {
    if (!confirm(`Delete "${p.name}"? This soft-deletes it (hidden from the store).`)) return;
    setError('');
    try {
      await api.del(`/products/${p.id}`);
      flash('Product deleted ✓');
      await loadProducts();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Delete failed');
    }
  };

  return (
    <div className="admin-page">
      <div className="admin-header">
        <h1>⚙ Product Management</h1>
        <button onClick={() => setShowCreate((s) => !s)}>
          {showCreate ? 'Close' : '+ New product'}
        </button>
      </div>
      <p className="muted">{totalCount} active products in the catalog.</p>

      {message && <div className="message">{message}</div>}
      {error && <div className="message error">{error}</div>}

      {/* Create form */}
      {showCreate && (
        <form className="admin-create" onSubmit={createProduct}>
          <h3>Create product</h3>
          <div className="admin-grid">
            <label>
              Name
              <input value={form.name} onChange={(e) => setFormField('name', e.target.value)} required />
            </label>
            <label>
              Franchise
              <select value={form.franchiseId} onChange={(e) => setFormField('franchiseId', e.target.value)} required>
                {franchises.map((f) => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
            </label>
            <label>
              Type
              <select value={form.type} onChange={(e) => setFormField('type', Number(e.target.value))}>
                {PRODUCT_TYPES.map((t) => (
                  <option key={t.value} value={t.value}>{t.label}</option>
                ))}
              </select>
            </label>
            <label>
              Base price (₹)
              <input type="number" min={1} value={form.basePrice}
                onChange={(e) => setFormField('basePrice', Number(e.target.value))} required />
            </label>
            <label className="span-2">
              Image URL (optional)
              <input value={form.imageUrl} onChange={(e) => setFormField('imageUrl', e.target.value)} />
            </label>
            <label className="span-2">
              Description
              <textarea value={form.description} onChange={(e) => setFormField('description', e.target.value)} required />
            </label>
          </div>

          <div className="admin-variants">
            <div className="admin-variants-head">
              <strong>Variants</strong>
              <button type="button" className="btn-secondary" onClick={addVariantRow}>+ Add variant</button>
            </div>
            {form.variants.map((v, idx) => (
              <div className="admin-variant-row" key={idx}>
                <input placeholder="SKU *" value={v.sku} onChange={(e) => setVariant(idx, 'sku', e.target.value)} />
                <input placeholder="Size" value={v.size} onChange={(e) => setVariant(idx, 'size', e.target.value)} />
                <input placeholder="Color" value={v.color} onChange={(e) => setVariant(idx, 'color', e.target.value)} />
                <input type="number" placeholder="Stock" min={0} value={v.stock}
                  onChange={(e) => setVariant(idx, 'stock', Number(e.target.value))} />
                {form.variants.length > 1 && (
                  <button type="button" className="link-btn" onClick={() => removeVariantRow(idx)}>✕</button>
                )}
              </div>
            ))}
          </div>

          <button type="submit" disabled={busy}>{busy ? 'Creating…' : 'Create product'}</button>
        </form>
      )}

      {/* Product table */}
      <table className="table admin-table">
        <thead>
          <tr><th>Name</th><th>Type</th><th>Franchise</th><th>Price</th><th>Stock</th><th>Actions</th></tr>
        </thead>
        <tbody>
          {products.map((p) => {
            const totalStock = p.variants.reduce((s, v) => s + v.stockQuantity, 0);
            return (
              <tr key={p.id}>
                <td>{p.name}</td>
                <td>{p.typeName}</td>
                <td><span className="badge">{p.franchiseShortCode}</span></td>
                <td>₹{p.basePrice.amount.toFixed(0)}</td>
                <td className={totalStock > 0 ? '' : 'out-stock'}>{totalStock}</td>
                <td className="admin-actions">
                  <button className="link-btn" onClick={() => startEdit(p)}>Edit</button>
                  <button className="link-btn danger" onClick={() => deleteProduct(p)}>Delete</button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* Edit modal */}
      {edit && (
        <div className="modal-overlay" onClick={() => setEdit(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h3>Edit product</h3>
            <label>
              Name
              <input value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} />
            </label>
            <label>
              Description
              <textarea value={edit.description}
                placeholder="New description"
                onChange={(e) => setEdit({ ...edit, description: e.target.value })} />
            </label>
            <label>
              Base price (₹)
              <input type="number" min={1} value={edit.basePrice}
                onChange={(e) => setEdit({ ...edit, basePrice: Number(e.target.value) })} />
            </label>
            <label>
              Image URL
              <input value={edit.imageUrl} onChange={(e) => setEdit({ ...edit, imageUrl: e.target.value })} />
            </label>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setEdit(null)}>Cancel</button>
              <button onClick={saveEdit} disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
