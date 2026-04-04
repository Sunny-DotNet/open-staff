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
    providerType: string;
    baseUrl: string;
    apiKeyMode: 'device' | 'env' | 'input';
    apiKeyEnvVar: string;
    hasApiKey: boolean;
    defaultModel: string;
    extraConfig: string | null;
    isEnabled: boolean;
    isBuiltin: boolean;
    createdAt: string;
    updatedAt: string;
  }

  export interface CreateModelProviderParams {
    name: string;
    providerType: string;
    baseUrl?: string;
    apiKeyMode?: string;
    apiKeyEnvVar?: string;
    apiKey?: string;
    defaultModel?: string;
    extraConfig?: string;
    isEnabled?: boolean;
  }

  export interface UpdateModelProviderParams {
    name?: string;
    baseUrl?: string;
    apiKeyMode?: string;
    apiKeyEnvVar?: string;
    apiKey?: string;
    defaultModel?: string;
    extraConfig?: string;
    isEnabled?: boolean;
  }

  export interface DeviceCodeResponse {
    userCode: string;
    verificationUri: string;
    expiresIn: number;
    interval: number;
  }

  export interface DeviceAuthPollResult {
    status:
      | 'denied'
      | 'error'
      | 'expired'
      | 'no_session'
      | 'pending'
      | 'success';
    message: string;
    interval?: number;
  }

  export interface ProviderModel {
    id: string;
    displayName: string | null;
  }
}

/** 获取全局设置 */
export async function getSettingsApi() {
  const resp = await requestClient.get('/settings');
  return (resp as any)?.data ?? resp;
}

/** 更新全局设置 */
export async function updateSettingsApi(data: SettingsApi.GlobalSettings) {
  const resp = await requestClient.put('/settings', data);
  return (resp as any)?.data ?? resp;
}

/** 获取模型提供商列表 */
export async function getModelProvidersApi(): Promise<
  SettingsApi.ModelProvider[]
> {
  const resp = await requestClient.get('/model-providers');
  return (resp as any)?.data ?? resp;
}

/** 创建模型提供商 */
export async function createModelProviderApi(
  data: SettingsApi.CreateModelProviderParams,
): Promise<SettingsApi.ModelProvider> {
  const resp = await requestClient.post('/model-providers', data);
  return (resp as any)?.data ?? resp;
}

/** 更新模型提供商 */
export async function updateModelProviderApi(
  id: string,
  data: SettingsApi.UpdateModelProviderParams,
): Promise<SettingsApi.ModelProvider> {
  const resp = await requestClient.put(`/model-providers/${id}`, data);
  return (resp as any)?.data ?? resp;
}

/** 删除模型提供商 */
export async function deleteModelProviderApi(id: string): Promise<void> {
  await requestClient.delete(`/model-providers/${id}`);
}

/** 发起 GitHub 设备码授权 */
export async function initiateDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceCodeResponse> {
  const resp = await requestClient.post(`/model-providers/${id}/device-auth`);
  return (resp as any)?.data ?? resp;
}

/** 轮询设备码授权状态 */
export async function pollDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceAuthPollResult> {
  const resp = await requestClient.post(
    `/model-providers/${id}/device-auth/poll`,
  );
  return (resp as any)?.data ?? resp;
}

/** 取消设备码授权 */
export async function cancelDeviceAuthApi(id: string): Promise<void> {
  await requestClient.delete(`/model-providers/${id}/device-auth`);
}

/** 获取供应商可用模型列表 */
export async function getProviderModelsApi(
  id: string,
): Promise<SettingsApi.ProviderModel[]> {
  const resp = await requestClient.get(`/model-providers/${id}/models`);
  return (resp as any)?.data ?? resp;
}
