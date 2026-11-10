// Routing der Hauptseiten aus dem Auftrag E1–E5.

import { Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import type { ReactNode } from 'react';
import { LoginPage } from './pages/LoginPage';
import { MfaLoginPage } from './pages/MfaLoginPage';
import { MfaSetupPage } from './pages/MfaSetupPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { ProjectDetailPage } from './pages/ProjectDetailPage';
import { ScanResultPage } from './pages/ScanResultPage';
import { TicketsPage } from './pages/TicketsPage';
import { TicketDetailPage } from './pages/TicketDetailPage';
import { AdminUsersPage } from './pages/AdminUsersPage';
import { AdminTeamsPage } from './pages/AdminTeamsPage';
import { AdminAuditPage } from './pages/AdminAuditPage';
import { NewsPage } from './pages/NewsPage';
import { HinweisePage } from './pages/HinweisePage';
import { Layout } from './components/Layout';

/** Schützt Bereiche, die eine Anmeldung brauchen (E2). */
function RequireAuth() {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Outlet />;
}

function RequireAdmin({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role !== 'Admin') return <Navigate to="/tickets" replace />;
  return <>{children}</>;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/mfa" element={<MfaLoginPage />} />
      <Route path="/news" element={<NewsPage />} />
      <Route path="/hinweise" element={<HinweisePage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/projects/:id" element={<ProjectDetailPage />} />
          <Route path="/scans/:id" element={<ScanResultPage />} />
          <Route path="/tickets" element={<TicketsPage />} />
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
          <Route path="/mfa-setup" element={<MfaSetupPage />} />
          <Route
            path="/admin/users"
            element={<RequireAdmin><AdminUsersPage /></RequireAdmin>}
          />
          <Route
            path="/admin/teams"
            element={<RequireAdmin><AdminTeamsPage /></RequireAdmin>}
          />
          <Route
            path="/admin/audit"
            element={<RequireAdmin><AdminAuditPage /></RequireAdmin>}
          />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/projects" replace />} />
    </Routes>
  );
}

// Der Seitenrahmen mit Navigation liegt als eigene Route hier, damit
// Login und MFA bewusst ohne Navigationszugriff bleiben.
function AppShell() {
  return <Layout />;
}
