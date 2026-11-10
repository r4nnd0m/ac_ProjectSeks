// E2: Eigene Seite zum Einrichten der Zwei-Faktor-Anmeldung:
// QR-Code (aus der otpauth-URL erzeugt), Secret zum Abtippen, Bestätigung
// mit einem Code.

import { useEffect, useState, type FormEvent } from 'react';
import QRCode from 'qrcode';
import { useAuth } from '../auth/AuthContext';
import { authApi } from '../api/authApi';
import { USE_MOCKS } from '../config';
import { ErrorBox, Field, Spinner } from '../components/ui';
import type { MfaSetupDto } from '../types';

export function MfaSetupPage() {
  const { token, user, markMfaEnabled } = useAuth();
  const [setup, setSetup] = useState<MfaSetupDto | null>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [code, setCode] = useState('');
  const [confirmBusy, setConfirmBusy] = useState(false);
  const [confirmError, setConfirmError] = useState<unknown>(null);
  const [done, setDone] = useState(false);

  useEffect(() => {
    if (!token) return;
    let stopped = false;
    authApi
      .setupMfa(token)
      .then(async (data) => {
        if (stopped) return;
        setSetup(data);
        const url = await QRCode.toDataURL(data.otpauthUrl, { margin: 1 });
        if (!stopped) setQrDataUrl(url);
      })
      .catch((err) => {
        if (!stopped) setLoadError(err);
      });
    return () => {
      stopped = true;
    };
  }, [token]);

  if (!token || !user) return null;

  if (done) {
    return (
      <div>
        <h1>Zwei-Faktor-Anmeldung eingerichtet</h1>
        <div className="noticebox" role="status">
          Fertig! Bei der nächsten Anmeldung verlangt SIMS zusätzlich zum Passwort einen Einmalcode.
        </div>
      </div>
    );
  }

  async function onConfirm(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setConfirmBusy(true);
    setConfirmError(null);
    try {
      await authApi.confirmMfa(token, code);
      markMfaEnabled();
      setDone(true);
    } catch (err) {
      setConfirmError(err);
    } finally {
      setConfirmBusy(false);
    }
  }

  return (
    <div>
      <h1>Zwei-Faktor-Anmeldung einrichten</h1>
      <p className="hint">
        1. QR-Code mit einer Authenticator-App scannen (oder das Secret abtippen).{' '}
        2. Anschließend einen angezeigten Code zur Bestätigung eingeben.
      </p>
      {loadError ? <ErrorBox error={loadError} /> : null}
      {!setup && !loadError ? <Spinner label="Vorbereitungen laufen…" /> : null}
      {setup ? (
        <div className="card">
          <h2>Schritt 1: Gerät verbinden</h2>
          {qrDataUrl ? (
            <img className="qr" src={qrDataUrl} alt="QR-Code zum Einrichten der Zwei-Faktor-Anmeldung" width={220} height={220} />
          ) : (
            <p className="hint">QR-Code wird gezeichnet…</p>
          )}
          <p>
            Secret zum manuellen Eintragen: <span className="secret">{setup.secret}</span>
          </p>
          <p className="hint">
            Anmeldekette für Apps, die den QR-Code nicht lesen können: <span className="secret">{setup.otpauthUrl}</span>
          </p>
        </div>
      ) : null}
      {setup ? (
        <div className="card">
          <h2>Schritt 2: Bestätigen</h2>
          {USE_MOCKS ? <p className="hint">Demo-Modus: als Bestätigungscode genügt <code>123456</code>.</p> : null}
          <form onSubmit={onConfirm}>
            <Field label="Code aus der App">
              <input value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" required />
            </Field>
            {confirmError ? <ErrorBox error={confirmError} /> : null}
            <button type="submit" disabled={confirmBusy}>
              {confirmBusy ? 'Prüfung läuft…' : 'Zwei-Faktor aktivieren'}
            </button>
          </form>
        </div>
      ) : null}
    </div>
  );
}
