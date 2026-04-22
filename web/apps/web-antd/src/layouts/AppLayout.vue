<script setup lang="ts">
import { computed, watchEffect } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { BasicLayout } from '@vben/layouts';
import { clearCache, preferences, resetPreferences } from '@vben/preferences';

import { getModuleTitle, getSectionTitle, t } from '@/i18n';
import { navigationModules } from '@/navigation';

const route = useRoute();
const router = useRouter();

const currentModule = computed(() =>
  navigationModules.find((module) => route.path.startsWith(module.path)),
);
const currentArea = computed(() =>
  currentModule.value
    ? getSectionTitle(currentModule.value.section, currentModule.value.section)
    : t('menu.overview'),
);
const currentTitle = computed(() =>
  currentModule.value
    ? getModuleTitle(currentModule.value.key, currentModule.value.title)
    : t('menu.overview'),
);

function handleClickLogo() {
  void router.push(preferences.app.defaultHomePath);
}

function handleClearPreferences() {
  clearCache();
  resetPreferences();
  void router.replace(preferences.app.defaultHomePath);
}

watchEffect(() => {
  document.title = `${currentTitle.value} - OpenStaff`;
});
</script>

<template>
  <BasicLayout
    @clear-preferences-and-logout="handleClearPreferences"
    @click-logo="handleClickLogo"
  >
    <template #logo-text>
      <span class="font-semibold tracking-tight">OpenStaff</span>
    </template>

    <template #header-left-10>
      <div class="hidden items-center gap-2 lg:flex">
        <a-tag :bordered="false" color="processing">
          {{ currentArea }}
        </a-tag>
        <span class="text-sm text-muted-foreground">
          {{ currentTitle }}
        </span>
      </div>
    </template>
  </BasicLayout>
</template>
