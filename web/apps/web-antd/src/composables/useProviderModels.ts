import { ref, watch, type Ref } from 'vue';

import type { SettingsApi } from '#/api/openstaff/settings';

import { getProviderModelsApi } from '#/api/openstaff/settings';

/**
 * 供应商 → 模型列表联动 composable
 * 监听 providerId 变化，自动加载对应模型列表
 */
export function useProviderModels(providerId: Ref<string>) {
  const models = ref<SettingsApi.ProviderModel[]>([]);
  const loading = ref(false);

  async function fetchModels(pid: string) {
    if (!pid) {
      models.value = [];
      return;
    }
    loading.value = true;
    models.value = [];
    try {
      models.value = await getProviderModelsApi(pid);
    } catch {
      models.value = [];
    } finally {
      loading.value = false;
    }
  }

  watch(
    () => providerId.value,
    async (newId, oldId) => {
      if (newId && newId !== oldId) {
        await fetchModels(newId);
      } else if (!newId) {
        models.value = [];
      }
    },
  );

  return { fetchModels, loading, models };
}
