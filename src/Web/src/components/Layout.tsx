// Gemeinsamer Seitenrahmen mit modernem Layout (Atlassian-Style).

import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { simsConfig, USE_MOCKS } from '../config';
import { DemoBadge } from './ui';
import { ThemeToggle } from './ThemeToggle';
import { BrandLogo } from './BrandLogo';

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const link = ({ isActive }: { isActive: boolean }) => (isActive ? 'active' : undefined);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <NavLink to="/dashboard" className="brand-title">
            <BrandLogo size={40} />
          </NavLink>
          <span className="brand-sub">Security Incident Management System</span>
        </div>
        <div className="session">
          {USE_MOCKS ? <DemoBadge /> : null}
          {user ? (
            <span className="who">
              {user.email} · {user.role}
              {user.mfaVerified ? ' · 2FA' : ''}
            </span>
          ) : (
            <span className="who">Nicht angemeldet</span>
          )}
          {user ? (
            <button
              className="link-btn"
              onClick={() => {
                void logout();
                navigate('/login');
              }}
            >
              Abmelden
            </button>
          ) : (
            <NavLink to="/login">Anmelden</NavLink>
          )}
          <ThemeToggle />
        </div>
      </header>

      <nav className="mainnav" aria-label="Hauptnavigation">
        <NavLink to="/dashboard" className={link}>Dashboard</NavLink>
        <NavLink to="/projects" className={link}>Projekte</NavLink>
        <NavLink to="/tickets" className={link}>Tickets</NavLink>
        {user?.role === 'Admin' ? (
          <NavLink to="/admin/users" className={link}>Verwaltung</NavLink>
        ) : null}
        <NavLink to="/news" className={link}>Öffentliche Meldungen</NavLink>
        {user ? <NavLink to="/mfa-setup" className={link}>2FA</NavLink> : null}
        <NavLink to="/hinweise" className={link}>Hinweise</NavLink>
      </nav>

      <main className="content">
        <Outlet />
      </main>

      <footer className="footer">
        {USE_MOCKS ? (
          <p>
            <strong>Demo-Modus</strong> (VITE_USE_MOCKS): alle Daten sind erfunden. 
            Echte Scanurteile, Ticketregeln und Eskalationen liefert das Backend.
          </p>
        ) : (
          <p>
            APIs: Auth {simsConfig.authUrl} · Scan {simsConfig.scanUrl} · News {simsConfig.newsUrl}
          </p>
        )}
      </footer>
    </div>
  );
}
