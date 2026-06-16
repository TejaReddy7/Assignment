import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { api, ApiError } from './api';
import { useAuth } from './auth';
import type { Cart, CartItem } from './types';

const GUEST_CART_KEY = 'ipl_guest_cart';
const MAX_QTY_PER_LINE = 10; // mirrors Domain Cart.MaxQtyPerLine

// Everything needed to add a line from anywhere (product card or details page).
export interface AddItemInput {
  productId: string;
  productVariantId: string;
  productName: string;
  imageUrl: string | null;
  unitPrice: number;
}

interface CartState {
  cart: Cart;
  itemCount: number;
  loading: boolean;
  addItem: (input: AddItemInput, quantity?: number) => Promise<void>;
  updateQty: (itemId: string, quantity: number) => Promise<void>;
  removeItem: (itemId: string) => Promise<void>;
  clear: () => Promise<void>;
  refresh: () => Promise<void>;
}

const EMPTY_CART: Cart = { id: 'guest', items: [], totalItems: 0, subtotal: 0, currency: 'INR' };

const CartContext = createContext<CartState | undefined>(undefined);

// ---- Guest cart persistence (localStorage) ----
function loadGuest(): CartItem[] {
  try {
    return JSON.parse(localStorage.getItem(GUEST_CART_KEY) || '[]') as CartItem[];
  } catch {
    return [];
  }
}

function saveGuest(items: CartItem[]) {
  localStorage.setItem(GUEST_CART_KEY, JSON.stringify(items));
}

function clearGuest() {
  localStorage.removeItem(GUEST_CART_KEY);
}

function computeGuestCart(items: CartItem[]): Cart {
  const totalItems = items.reduce((s, i) => s + i.quantity, 0);
  const subtotal = items.reduce((s, i) => s + i.lineTotal, 0);
  return { id: 'guest', items, totalItems, subtotal, currency: 'INR' };
}

export function CartProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [cart, setCart] = useState<Cart>(EMPTY_CART);
  const [loading, setLoading] = useState(false);
  // Guards against React StrictMode double-invoking the merge effect.
  const mergingRef = useRef(false);

  const refresh = useCallback(async () => {
    if (isAuthenticated) {
      setLoading(true);
      try {
        setCart(await api.get<Cart>('/cart'));
      } finally {
        setLoading(false);
      }
    } else {
      setCart(computeGuestCart(loadGuest()));
    }
  }, [isAuthenticated]);

  // On auth transition: merge any guest cart into the server cart in ONE atomic call,
  // then load it. On reload-while-authenticated the guest cart is empty (merge no-ops).
  useEffect(() => {
    let cancelled = false;
    async function sync() {
      if (isAuthenticated) {
        if (mergingRef.current) return;
        mergingRef.current = true;
        try {
          const guestItems = loadGuest();
          if (guestItems.length > 0) {
            const merged = await api.post<Cart>('/cart/merge', {
              items: guestItems.map((i) => ({
                productVariantId: i.productVariantId,
                quantity: i.quantity,
              })),
            });
            clearGuest();
            if (!cancelled) setCart(merged);
          } else {
            const serverCart = await api.get<Cart>('/cart');
            if (!cancelled) setCart(serverCart);
          }
        } finally {
          mergingRef.current = false;
        }
      } else if (!cancelled) {
        setCart(computeGuestCart(loadGuest()));
      }
    }
    // Swallow the 401 raised after a failed refresh: AuthProvider's session-expired
    // handler will flip isAuthenticated to false and this effect will re-run as guest.
    void sync().catch((err) => {
      if (err instanceof ApiError && err.status === 401) return;
      console.error('Cart sync failed:', err);
    });
    return () => {
      cancelled = true;
    };
  }, [isAuthenticated]);

  const addItem = async (input: AddItemInput, quantity = 1) => {
    if (isAuthenticated) {
      const updated = await api.post<Cart>('/cart/items', {
        productVariantId: input.productVariantId,
        quantity,
      });
      setCart(updated);
      return;
    }

    const items = loadGuest();
    const existing = items.find((i) => i.productVariantId === input.productVariantId);
    if (existing) {
      existing.quantity = Math.min(MAX_QTY_PER_LINE, existing.quantity + quantity);
      existing.lineTotal = existing.unitPrice * existing.quantity;
    } else {
      const qty = Math.min(MAX_QTY_PER_LINE, quantity);
      items.push({
        id: input.productVariantId, // guest line id == variant id (stable, local-only)
        productId: input.productId,
        productVariantId: input.productVariantId,
        productName: input.productName,
        imageUrl: input.imageUrl,
        unitPrice: input.unitPrice,
        quantity: qty,
        lineTotal: input.unitPrice * qty,
      });
    }
    saveGuest(items);
    setCart(computeGuestCart(items));
  };

  const updateQty = async (itemId: string, quantity: number) => {
    if (isAuthenticated) {
      const updated = await api.patch<Cart>(`/cart/items/${itemId}`, { quantity });
      setCart(updated);
      return;
    }

    let items = loadGuest();
    if (quantity <= 0) {
      items = items.filter((i) => i.id !== itemId);
    } else {
      const item = items.find((i) => i.id === itemId);
      if (item) {
        item.quantity = Math.min(MAX_QTY_PER_LINE, quantity);
        item.lineTotal = item.unitPrice * item.quantity;
      }
    }
    saveGuest(items);
    setCart(computeGuestCart(items));
  };

  const removeItem = async (itemId: string) => {
    if (isAuthenticated) {
      const updated = await api.del<Cart>(`/cart/items/${itemId}`);
      setCart(updated);
      return;
    }
    const items = loadGuest().filter((i) => i.id !== itemId);
    saveGuest(items);
    setCart(computeGuestCart(items));
  };

  const clear = async () => {
    if (isAuthenticated) {
      await api.del('/cart');
    } else {
      clearGuest();
    }
    setCart(EMPTY_CART);
  };

  return (
    <CartContext.Provider
      value={{ cart, itemCount: cart.totalItems, loading, addItem, updateQty, removeItem, clear, refresh }}
    >
      {children}
    </CartContext.Provider>
  );
}

export function useCart(): CartState {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error('useCart must be used within CartProvider');
  return ctx;
}
