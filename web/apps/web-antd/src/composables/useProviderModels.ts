import { ref, watch, type Ref } from 'vue';

import type { SettingsApi } from '#/api/openstaff/settings';

const MAX_RETRIES = 2;
const RETRY_DELAY_MS = 1500;

/**
 * 直接用 fetch 获取模型列表，绕过 Axios 可能的请求取消问题
 */
async function fetchModelsFromApi(
  pid: string,
): Promise<SettingsApi.ProviderModel[]> {
  const resp = await fetch(`/api/provider-accounts/${pid}/models`);
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  return resp.json();
}

/**
 * 供应商 → 模型列表联动 composable
 * 监听 providerId 变化，自动加载对应模型列表
 * 失败时自动重试，并支持手动刷新
 */
export function useProviderModels(providerId: Ref<string>) {
  const models = ref<SettingsApi.ProviderModel[]>([]);
  const loading = ref(false);
  const error = ref(false);
  let fetchSeq = 0;

  async function fetchModels(pid: string) {
    if (!pid) {
      models.value = [];
      error.value = false;
      return;
    }

    const seq = ++fetchSeq;
    loading.value = true;
    error.value = false;

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      if (seq !== fetchSeq) return; // 被更新的调用取代
      try {
        const result = await fetchModelsFromApi(pid);
        if (seq !== fetchSeq) return;
        models.value = result;
        error.value = false;
        loading.value = false;
        return;
      } catch {
        if (attempt < MAX_RETRIES) {
          await new Promise((r) => setTimeout(r, RETRY_DELAY_MS));
        }
      }
    }

    if (seq !== fetchSeq) return;
    models.value = [];
    error.value = true;
    loading.value = false;
  }

  /** 手动刷新当前供应商的模型列表 */
  function refresh() {
    if (providerId.value) {
      fetchModels(providerId.value);
    }
  }

  /** 点击模型下拉框时调用：若列表为空且有供应商，自动重新加载 */
  function ensureLoaded() {
    if (providerId.value && models.value.length === 0 && !loading.value) {
      fetchModels(providerId.value);
    }
  }

  watch(
    () => providerId.value,
    (newId) => {
      if (newId) {
        fetchModels(newId);
      } else {
        fetchSeq++;
        models.value = [];
        error.value = false;
        loading.value = false;
      }
    },
  );

  return { ensureLoaded, error, fetchModels, loading, models, refresh };
}
