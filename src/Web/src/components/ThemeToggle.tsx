// Theme-Umschalter: Light, Dark, System
// Speichert die Auswahl in localStorage und reagiert live auf Systemänderungen.

import { useEffect, useState } from 'react';

type Theme = 'light' | 'dark' | 'system';

const THEME_STORAGE_KEY = 'sims-theme';

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>('system');
  const [mounted, setMounted] = useState(false);

  // Initialen Theme-Wert laden und auf html-Element setzen
  useEffect(() => {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY) as Theme | null;
    const initial = stored ?? 'system';
    setTheme(initial);
    setMounted(true);
    applyTheme(initial);
  }, []);

  // Reagiere auf System-Änderungen, wenn 'system' ausgewählt ist
  useEffect(() => {
    if (theme !== 'system') return;

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = () => {
      applyTheme('system');
    };

    mediaQuery.addEventListener('change', handleChange);
    applyTheme('system');

    return () => mediaQuery.removeEventListener('change', handleChange);
  }, [theme]);

  const applyTheme = (selected: Theme) => {
    const html = document.documentElement;
    let isDark = false;

    if (selected === 'dark') {
      isDark = true;
    } else if (selected === 'system') {
      isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    html.setAttribute('data-theme', isDark ? 'dark' : 'light');
  };

  const setAndSaveTheme = (newTheme: Theme) => {
    setTheme(newTheme);
    window.localStorage.setItem(THEME_STORAGE_KEY, newTheme);
    applyTheme(newTheme);
  };

  if (!mounted) return null;

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
