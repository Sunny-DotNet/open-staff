import { ref, watch, type Ref } from 'vue';

import type { SettingsApi } from '#/api/openstaff/settings';

import { getProviderModelsApi } from '#/api/openstaff/settings';

const MAX_RETRIES = 2;
const RETRY_DELAY_MS = 1500;

/**
 * 供应商 → 模型列表联动 composable
 * 监听 providerId 变化，自动加载对应模型列表
 * 失败时自动重试，并支持手动刷新
 */
export function useProviderModels(providerId: Ref<string>) {
  const models = ref<SettingsApi.ProviderModel[]>([]);
  const loading = ref(false);
  const error = ref(false);
  let lastFetchedId = '';

  async function fetchModels(pid: string) {
    if (!pid) {
      models.value = [];
      error.value = false;
      lastFetchedId = '';
      return;
    }
    loading.value = true;
    error.value = false;
    models.value = [];

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      try {
        models.value = await getProviderModelsApi(pid);
        lastFetchedId = pid;
        error.value = false;
        break;
      } catch {
        if (attempt < MAX_RETRIES) {
          await new Promise((r) => setTimeout(r, RETRY_DELAY_MS));
        } else {
          models.value = [];
          error.value = true;
        }
      }
    }

    loading.value = false;
  }

  /** 手动刷新当前供应商的模型列表 */
  function refresh() {
    if (providerId.value) {
      fetchModels(providerId.value);
    }
  }

  /**
   * 点击模型下拉框时调用：若列表为空且有供应商，自动重新加载
   */
  function ensureLoaded() {
    if (
      providerId.value &&
      models.value.length === 0 &&
      !loading.value
    ) {
      fetchModels(providerId.value);
    }
  }

  watch(
    () => providerId.value,
    async (newId) => {
      if (newId) {
        await fetchModels(newId);
      } else {
        models.value = [];
        error.value = false;
        lastFetchedId = '';
      }
    },
  );

  return { ensureLoaded, error, fetchModels, loading, models, refresh };
}
