import { requestClient } from '#/api/request';

export namespace SettingsApi {
  export interface GlobalSettings {
    defaultModel: string;
    language: string;
    maxTokens: number;
    enableAutoSave: boolean;
  }

  export interface ModelProvider {
    id: string;
    name: string;
    type: string;
    apiKey: string;
    baseUrl: string;
    enabled: boolean;
    models: string[];
  }

  export interface CreateModelProviderParams {
    name: string;
    type: string;
    apiKey: string;
    baseUrl: string;
    enabled?: boolean;
  }

  export interface UpdateModelProviderParams {
    name?: string;
    type?: string;
    apiKey?: string;
    baseUrl?: string;
    enabled?: boolean;
  }
}

/** 获取全局设置 */
export async function getSettingsApi() {
  return requestClient.get<SettingsApi.GlobalSettings>('/settings');
}

/** 更新全局设置 */
export async function updateSettingsApi(data: SettingsApi.GlobalSettings) {
  return requestClient.put<SettingsApi.GlobalSettings>('/settings', data);
}

/** 获取模型提供商列表 */
export async function getModelProvidersApi() {
  return requestClient.get<SettingsApi.ModelProvider[]>('/model-providers');
}

/** 创建模型提供商 */
export async function createModelProviderApi(
  data: SettingsApi.CreateModelProviderParams,
) {
  return requestClient.post<SettingsApi.ModelProvider>(
    '/model-providers',
    data,
  );
}

/** 更新模型提供商 */
export async function updateModelProviderApi(
  id: string,
  data: SettingsApi.UpdateModelProviderParams,
) {
  return requestClient.put<SettingsApi.ModelProvider>(
    `/model-providers/${id}`,
    data,
  );
}

/** 删除模型提供商 */
export async function deleteModelProviderApi(id: string) {
  return requestClient.delete(`/model-providers/${id}`);
}
