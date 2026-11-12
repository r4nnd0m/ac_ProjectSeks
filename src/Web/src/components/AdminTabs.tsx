// Gemeinsame Reiter für die Verwaltungsseiten (nur Admins, E4).

import { NavLink } from 'react-router-dom';

export function AdminTabs() {
  const link = ({ isActive }: { isActive: boolean }) => (isActive ? 'active' : undefined);
  return (
    <nav className="admintabs" aria-label="Verwaltung">
      <NavLink to="/admin/users" className={link}>Benutzer</NavLink>
      <NavLink to="/admin/teams" className={link}>Teams</NavLink>
      <NavLink to="/admin/audit" className={link}>Audit-Protokoll</NavLink>
    </nav>
  );
}
