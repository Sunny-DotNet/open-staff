import type { SettingsApi } from '#/api/openstaff/settings';

import { ref } from 'vue';

import { defineStore } from 'pinia';

import {
  getModelProvidersApi,
  getSettingsApi,
  updateModelProviderApi,
  updateSettingsApi,
} from '#/api/openstaff/settings';

export const useSettingsStore = defineStore('openstaff-settings', () => {
  const settings = ref<SettingsApi.GlobalSettings | null>(null);
  const modelProviders = ref<SettingsApi.ModelProvider[]>([]);
  const loading = ref(false);

  async function fetchSettings() {
    loading.value = true;
    try {
      settings.value = await getSettingsApi();
    } finally {
      loading.value = false;
    }
  }

  async function saveSettings(data: SettingsApi.GlobalSettings) {
    loading.value = true;
    try {
      settings.value = await updateSettingsApi(data);
    } finally {
      loading.value = false;
    }
  }

  async function fetchModelProviders() {
    try {
      modelProviders.value = await getModelProvidersApi();
    } catch {
      // Silently fail
    }
  }

  async function updateProvider(
    id: string,
    data: SettingsApi.UpdateModelProviderParams,
  ) {
    const updated = await updateModelProviderApi(id, data);
    const idx = modelProviders.value.findIndex((p) => p.id === id);
    if (idx >= 0 && updated) {
      modelProviders.value[idx] = updated;
    }
    return updated;
  }

  function $reset() {
    settings.value = null;
    modelProviders.value = [];
    loading.value = false;
  }

  return {
    $reset,
    fetchModelProviders,
    fetchSettings,
    loading,
    modelProviders,
    saveSettings,
    settings,
    updateProvider,
  };
});
