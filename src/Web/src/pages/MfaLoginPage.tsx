// E2: Zweiter Anmeldeschritt mit Einmalcode, wenn die API ihn verlangt.

import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { describeError } from '../api/http';
import { Field } from '../components/ui';

export function MfaLoginPage() {
  const { pendingMfaToken, verifyMfa } = useAuth();
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!pendingMfaToken) {
    navigate('/login', { replace: true });
    return null;
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await verifyMfa(code);
      navigate('/projects');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="loginbox">
      <div className="card">
        <h1>Zweiter Anmeldeschritt</h1>
        <p className="hint">
          Deine Anmeldung braucht einen Einmalcode (Zwei-Faktor). Gib den sechsstelligen Code
          aus deiner Authenticator-App ein.
        </p>
        <form onSubmit={onSubmit}>
          <Field label="Einmalcode">
            <input
              value={code}
              onChange={(e) => setCode(e.target.value)}
              inputMode="numeric"
              pattern="[0-9]*"
              maxLength={10}
              autoFocus
              required
            />
          </Field>
          {error ? <div className="errorbox" role="alert">{error}</div> : null}
          <button type="submit" disabled={busy}>{busy ? 'Prüfung läuft…' : 'Bestätigen'}</button>{' '}
          <button type="button" className="secondary" onClick={() => navigate('/login')}>
            Abbrechen
          </button>
        </form>
      </div>
    </div>
  );
}
