import { ref, computed } from 'vue';

export type Theme = 'dark' | 'light';

// Keep in sync with the inline pre-paint script in index.html.
const STORAGE_KEY = 'theme';
const DEFAULT_THEME: Theme = 'dark';

function readStoredTheme(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'dark' || stored === 'light' ? stored : DEFAULT_THEME;
}

function applyTheme(value: Theme): void {
  document.documentElement.setAttribute('data-theme', value);
}

// Shared state across all components
const theme = ref<Theme>(readStoredTheme());

/**
 * Applies the stored theme (or the dark default) to <html>. Called once at startup
 * from main.ts; index.html already does the same thing inline to avoid a flash, so
 * this mostly re-asserts the attribute for the running app.
 */
export function initTheme(): void {
  applyTheme(theme.value);
}

export function useTheme() {
  const setTheme = (value: Theme): void => {
    theme.value = value;
    localStorage.setItem(STORAGE_KEY, value);
    applyTheme(value);
  };

  const toggleTheme = (): void => {
    setTheme(theme.value === 'dark' ? 'light' : 'dark');
  };

  return {
    theme: computed(() => theme.value),
    isDark: computed(() => theme.value === 'dark'),
    setTheme,
    toggleTheme
  };
}
