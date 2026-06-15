import { createContext, useContext, useState, type ReactNode } from 'react';
import { api, tokenStore } from './api';
import type { AuthResponse } from './types';

interface AuthState {
  email: string | null;
  roles: string[];
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, fullName: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<string | null>(localStorage.getItem('ipl_email'));
  const [roles, setRoles] = useState<string[]>(JSON.parse(localStorage.getItem('ipl_roles') || '[]'));

  const apply = (auth: AuthResponse) => {
    tokenStore.set(auth.accessToken, auth.refreshToken);
    localStorage.setItem('ipl_email', auth.email);
    localStorage.setItem('ipl_roles', JSON.stringify(auth.roles));
    setEmail(auth.email);
    setRoles(auth.roles);
  };

  const login = async (e: string, password: string) => {
    apply(await api.post<AuthResponse>('/auth/login', { email: e, password }));
  };

  const register = async (e: string, fullName: string, password: string) => {
    apply(await api.post<AuthResponse>('/auth/register', { email: e, fullName, password }));
  };

  const logout = () => {
    tokenStore.clear();
    localStorage.removeItem('ipl_email');
    localStorage.removeItem('ipl_roles');
    setEmail(null);
    setRoles([]);
  };

  return (
    <AuthContext.Provider
      value={{
        email,
        roles,
        isAuthenticated: !!email,
        isAdmin: roles.includes('Admin'),
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
