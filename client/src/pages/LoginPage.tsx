import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth';
import { ApiError } from '../api';

export function LoginPage() {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('fan@iplstore.local');
  const [password, setPassword] = useState('Fan#12345');
  const [fullName, setFullName] = useState('');
  const [error, setError] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      if (mode === 'login') await login(email, password);
      else await register(email, fullName, password);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Authentication failed');
    }
  };

  return (
    <div className="auth-box">
      <h1>{mode === 'login' ? 'Login' : 'Register'}</h1>
      <form onSubmit={submit}>
        <input type="email" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        {mode === 'register' && (
          <input placeholder="Full name" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
        )}
        <input type="password" placeholder="Password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        <button type="submit">{mode === 'login' ? 'Login' : 'Create account'}</button>
      </form>
      {error && <p className="message error">{error}</p>}
      <p className="muted">
        {mode === 'login' ? "No account?" : 'Have an account?'}{' '}
        <button className="link-btn" onClick={() => setMode(mode === 'login' ? 'register' : 'login')}>
          {mode === 'login' ? 'Register' : 'Login'}
        </button>
      </p>
      <p className="muted">Seeded: admin@iplstore.local / Admin#12345 · fan@iplstore.local / Fan#12345</p>
    </div>
  );
}
