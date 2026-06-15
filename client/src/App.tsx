import { Link, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth';
import { ProductListPage } from './pages/ProductListPage';
import { ProductDetailsPage } from './pages/ProductDetailsPage';
import { CartPage } from './pages/CartPage';
import { OrdersPage } from './pages/OrdersPage';
import { LoginPage } from './pages/LoginPage';
import type { ReactNode } from 'react';

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

export function App() {
  const { isAuthenticated, email, logout } = useAuth();

  return (
    <div className="app">
      <header className="nav">
        <Link to="/" className="brand">🏏 IPL Store</Link>
        <nav>
          <Link to="/">Products</Link>
          <Link to="/cart">Cart</Link>
          {isAuthenticated && <Link to="/orders">Orders</Link>}
          {isAuthenticated ? (
            <>
              <span className="user">{email}</span>
              <button className="link-btn" onClick={logout}>Logout</button>
            </>
          ) : (
            <Link to="/login">Login</Link>
          )}
        </nav>
      </header>

      <main className="content">
        <Routes>
          <Route path="/" element={<ProductListPage />} />
          <Route path="/products/:slug" element={<ProductDetailsPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/cart" element={<RequireAuth><CartPage /></RequireAuth>} />
          <Route path="/orders" element={<RequireAuth><OrdersPage /></RequireAuth>} />
        </Routes>
      </main>

      <footer className="footer">
        IPL Franchise Store — assessment build. Backend: .NET 10 · EF Core · CQRS.
      </footer>
    </div>
  );
}
