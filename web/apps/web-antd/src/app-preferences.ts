import dayjs from 'dayjs';
import { computed, ref, watch } from 'vue';

export type AppLocale = 'en-US' | 'zh-CN';
export type AppTheme = 'dark' | 'light';

const LOCALE_STORAGE_KEY = 'openstaff.locale';
const THEME_STORAGE_KEY = 'openstaff.theme';

const appLocale = ref<AppLocale>(resolveInitialLocale());
const appTheme = ref<AppTheme>(resolveInitialTheme());

const isDarkTheme = computed(() => appTheme.value === 'dark');

let initialized = false;

export { appLocale, appTheme, isDarkTheme };

export function initializeAppPreferences() {
  if (initialized || typeof document === 'undefined') {
    return;
  }

  initialized = true;

  watch(
    appLocale,
    (value) => {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, value);
      dayjs.locale(value === 'zh-CN' ? 'zh-cn' : 'en');
      document.documentElement.lang = value;
    },
    { immediate: true },
  );

  watch(
    appTheme,
    (value) => {
      window.localStorage.setItem(THEME_STORAGE_KEY, value);
      document.documentElement.classList.toggle('dark', value === 'dark');
      document.documentElement.dataset.theme = value;
      document.documentElement.style.colorScheme = value;
    },
    { immediate: true },
  );
}

export function setAppLocale(value: AppLocale) {
  appLocale.value = value;
}

export function toggleAppTheme() {
  appTheme.value = appTheme.value === 'dark' ? 'light' : 'dark';
}

function resolveInitialLocale(): AppLocale {
  if (typeof window === 'undefined') {
    return 'zh-CN';
  }

  const stored = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  if (stored === 'zh-CN' || stored === 'en-US') {
    return stored;
  }

  return navigator.language.startsWith('zh') ? 'zh-CN' : 'en-US';
}

function resolveInitialTheme(): AppTheme {
  if (typeof window === 'undefined') {
    return 'light';
  }

  const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
  if (stored === 'dark' || stored === 'light') {
    return stored;
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light';
}
