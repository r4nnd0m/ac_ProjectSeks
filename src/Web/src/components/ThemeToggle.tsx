// Theme-Umschalter: Light, Dark, System
// Speichert die Auswahl in localStorage und reagiert live auf Systemänderungen.

import { useEffect, useState } from 'react';

type Theme = 'light' | 'dark' | 'system';

const THEME_STORAGE_KEY = 'sims-theme';

function readStoredTheme(): Theme {
  const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
  return stored === 'light' || stored === 'dark' ? stored : 'system';
}

function applyTheme(selected: Theme) {
  const isDark =
    selected === 'dark' ||
    (selected === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
}

export function ThemeToggle() {
  // Direkt mit dem gespeicherten Wert starten, sonst überschreibt der erste Render das Theme.
  const [theme, setTheme] = useState<Theme>(readStoredTheme);

  useEffect(() => {
    applyTheme(theme);
    if (theme !== 'system') return;

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = () => applyTheme('system');
    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, [theme]);

  const setAndSaveTheme = (newTheme: Theme) => {
    window.localStorage.setItem(THEME_STORAGE_KEY, newTheme);
    setTheme(newTheme);
  };

  const icon = theme === 'light' ? '☀️' : theme === 'dark' ? '🌙' : '💻';

  return (
    <div className="theme-toggle" title="Farbschema wählen">
      <button
        type="button"
        className="link-btn"
        aria-label={`Theme wechseln (aktuell: ${theme})`}
        onClick={() => {
          const themes: Theme[] = ['light', 'dark', 'system'];
          const currentIndex = themes.indexOf(theme);
          const nextIndex = (currentIndex + 1) % themes.length;
          setAndSaveTheme(themes[nextIndex]);
        }}
      >
        <span className="theme-icon" aria-hidden="true">
          {icon}
        </span>
        <span className="theme-label">{theme === 'system' ? 'Sys' : theme === 'dark' ? 'Dunkel' : 'Hell'}</span>
      </button>
    </div>
  );
}
