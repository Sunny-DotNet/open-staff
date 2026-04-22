import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import { preferences } from '@vben/preferences';

export function useThemeMode() {
  const prefersDark = ref(false);

  let mediaQuery: MediaQueryList | null = null;

  function syncPreferredDark(nextValue?: boolean) {
    if (typeof nextValue === 'boolean') {
      prefersDark.value = nextValue;
      return;
    }

    if (typeof window === 'undefined') {
      prefersDark.value = false;
      return;
    }

    prefersDark.value = window.matchMedia('(prefers-color-scheme: dark)').matches;
  }

  function handleThemeMediaChange(event: MediaQueryListEvent) {
    syncPreferredDark(event.matches);
  }

  onMounted(() => {
    if (typeof window === 'undefined') {
      return;
    }

    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    syncPreferredDark(mediaQuery.matches);
    mediaQuery.addEventListener('change', handleThemeMediaChange);
  });

  onBeforeUnmount(() => {
    mediaQuery?.removeEventListener('change', handleThemeMediaChange);
    mediaQuery = null;
  });

  const isDarkMode = computed(() => {
    const mode = preferences.theme.mode;
    if (mode === 'auto') {
      return prefersDark.value;
    }

    return mode === 'dark';
  });

  return {
    isDarkMode,
  };
}
