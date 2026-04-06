import type { SettingsApi } from '#/api/openstaff/settings';

import { ref } from 'vue';

import { defineStore } from 'pinia';

import {
  getProviderAccountsApi,
  getSettingsApi,
  updateProviderAccountApi,
  updateSettingsApi,
} from '#/api/openstaff/settings';

export const useSettingsStore = defineStore('openstaff-settings', () => {
  const settings = ref<SettingsApi.GlobalSettings | null>(null);
  const providerAccounts = ref<SettingsApi.ProviderAccount[]>([]);
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

  async function fetchProviderAccounts() {
    try {
      providerAccounts.value = await getProviderAccountsApi();
    } catch {
      // Silently fail
    }
  }

  async function updateProviderAccount(
    id: string,
    data: SettingsApi.UpdateProviderAccountParams,
  ) {
    const updated = await updateProviderAccountApi(id, data);
    const idx = providerAccounts.value.findIndex((p) => p.id === id);
    if (idx >= 0 && updated) {
      providerAccounts.value[idx] = updated;
    }
    return updated;
  }

  function $reset() {
    settings.value = null;
    providerAccounts.value = [];
    loading.value = false;
  }

  return {
    $reset,
    fetchProviderAccounts,
    fetchSettings,
    loading,
    providerAccounts,
    saveSettings,
    settings,
    updateProviderAccount,
  };
});
