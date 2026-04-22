<script setup lang="ts">
import { computed } from 'vue';

import { theme } from 'ant-design-vue';
import { useThemeMode } from '@/composables/useThemeMode';

const { isDarkMode } = useThemeMode();

function resolveThemeToken(variableName: string, fallback: string) {
  if (typeof window === 'undefined') {
    return fallback;
  }

  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(variableName)
    .trim();

  return value ? `hsl(${value})` : fallback;
}

// 统一把 Ant Design 组件切到亮/暗算法，避免大量基础组件停留在浅色皮肤。
const antTheme = computed(() => {
  const isDark = isDarkMode.value;

  return {
    algorithm: isDark ? theme.darkAlgorithm : theme.defaultAlgorithm,
    token: {
      colorBgBase: resolveThemeToken('--background', isDark ? '#141414' : '#ffffff'),
      colorBgContainer: resolveThemeToken('--card', isDark ? '#1f1f1f' : '#ffffff'),
      colorBgElevated: resolveThemeToken('--card', isDark ? '#1f1f1f' : '#ffffff'),
      colorBorder: resolveThemeToken('--border', isDark ? '#303030' : '#e4e4e7'),
      colorFillAlter: resolveThemeToken('--muted', isDark ? '#1f1f1f' : '#f4f4f5'),
      colorPrimary: resolveThemeToken('--primary', '#006be6'),
      colorTextBase: resolveThemeToken('--foreground', isDark ? '#f2f2f2' : '#323639'),
      borderRadius: 12,
    },
  };
});
</script>

<template>
  <a-config-provider :theme="antTheme">
    <RouterView />
  </a-config-provider>
</template>
