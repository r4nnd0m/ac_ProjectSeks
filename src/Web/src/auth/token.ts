// Liest Claims aus dem JWT nur zur ANZEIGE (z. B. „mit 2FA angemeldet“).
// Die Signatur prüft ausschließlich das Backend; hier wird nichts vertraut.

export function readTokenClaims(token: string): Record<string, unknown> {
  const parts = token.split('.');
  if (parts.length < 2) return {};
  try {
    const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = b64 + '='.repeat((4 - (b64.length % 4)) % 4);
    const json = decodeURIComponent(
      Array.from(atob(padded), (c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join(''),
    );
    const parsed: unknown = JSON.parse(json);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

export function hasMfaClaim(token: string): boolean {
  const mfa = readTokenClaims(token)['mfa'];
  return mfa === true || mfa === 'true';
}
