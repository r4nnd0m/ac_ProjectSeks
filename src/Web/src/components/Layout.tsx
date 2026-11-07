// Gemeinsamer Seitenrahmen mit schlichter Navigation (E1).

import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { simsConfig, USE_MOCKS } from '../config';
import { DemoBadge } from './ui';

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const link = ({ isActive }: { isActive: boolean }) => (isActive ? 'active' : undefined);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <NavLink to="/projects" className="brand-title">
            SIMS – Sul Sul, Exploit!
          </NavLink>
          <span className="brand-sub">Prüfung von Bibliotheken vor der Softwarefreigabe</span>
        </div>
        <div className="session">
          {USE_MOCKS ? <DemoBadge /> : null}
          {user ? (
            <span className="who">
              {user.name} · {user.role}
              {user.mfaVerified ? ' · 2FA bestätigt' : ''}
            </span>
          ) : (
            <span className="who">Nicht angemeldet</span>
          )}
          {user ? (
            <button
              className="linkbtn"
              onClick={() => {
                logout();
                navigate('/login');
              }}
            >
              Abmelden
            </button>
          ) : (
            <NavLink to="/login">Anmelden</NavLink>
          )}
        </div>
      </header>

      <nav className="mainnav" aria-label="Hauptnavigation">
        <NavLink to="/projects" className={link}>Projekte</NavLink>
        <NavLink to="/tickets" className={link}>Tickets</NavLink>
        {user?.role === 'Admin' ? (
          <NavLink to="/admin/users" className={link}>Verwaltung</NavLink>
        ) : null}
        <NavLink to="/news" className={link}>Öffentliche Meldungen</NavLink>
        {user ? <NavLink to="/mfa-setup" className={link}>Zwei-Faktor</NavLink> : null}
        <NavLink to="/hinweise" className={link}>Hinweise</NavLink>
      </nav>

      <main className="content">
        <Outlet />
      </main>

      <footer className="footer">
        {USE_MOCKS ? (
          <p>
            <strong>Demo-Modus aktiv</strong> (VITE_USE_MOCKS): alle Daten sind erfunden. Echte Scanurteile,
            Ticketregeln und Eskalationen liefert das Backend.
          </p>
        ) : (
          <p>
            APIs: Auth {simsConfig.authUrl} · Scan {simsConfig.scanUrl} · News {simsConfig.newsUrl}
            (Basis-URLs in public/config.js)
          </p>
        )}
      </footer>
    </div>
  );
}
