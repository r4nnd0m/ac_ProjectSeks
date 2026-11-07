// E2: Anmeldung mit E-Mail und Passwort. Verlangt die API einen zweiten
// Faktor, führt die Seite weiter nach /mfa.

import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { describeError } from '../api/http';
import { USE_MOCKS } from '../config';
import { Field } from '../components/ui';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await login(email, password);
      navigate(result.mfaRequired ? '/mfa' : '/projects');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="loginbox">
      <div className="card">
        <h1>Anmeldung</h1>
        <p className="hint">
          Neu bei SIMS? <Link to="/news">Öffentliche Meldungen</Link> sind auch ohne Anmeldung lesbar.
        </p>
        <form onSubmit={onSubmit}>
          <Field label="E-Mail">
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
            />
          </Field>
          <Field label="Passwort">
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </Field>
          {error ? <div className="errorbox" role="alert">{error}</div> : null}
          <button type="submit" disabled={busy}>{busy ? 'Anmeldung läuft…' : 'Anmelden'}</button>
        </form>
        {USE_MOCKS ? (
          <p className="hint">
            <strong>Demo-Modus:</strong> erfundene Testkonten (Passwort jeweils <code>demo1234</code>):{' '}
            admin@sims.example (Admin), lead@sims.example (Lead), dev@sims.example (Developer, Team Blau),{' '}
            dev2@sims.example (Developer, Team Grün). Konten mit 2FA fordern den Einmalcode{' '}
            <code>123456</code> an.
          </p>
        ) : null}
      </div>
    </div>
  );
}
