import { requestClient } from '#/api/request';

export namespace SettingsApi {
  export interface GlobalSettings {
    defaultModel: string;
    language: string;
    maxTokens: number;
    enableAutoSave: boolean;
  }

  /** 供应商账户 */
  export interface ProviderAccount {
    id: string;
    name: string;
    protocolType: string;
    isEnabled: boolean;
    envConfig?: Record<string, any>;
    createdAt: string;
    updatedAt: string;
  }

  export interface CreateProviderAccountParams {
    name: string;
    protocolType: string;
    envConfig?: Record<string, any>;
    isEnabled?: boolean;
  }

  export interface UpdateProviderAccountParams {
    name?: string;
    envConfig?: Record<string, any>;
    isEnabled?: boolean;
  }

  /** 协议元数据 */
  export interface ProtocolEnvField {
    name: string;
    fieldType: string; // string, secret, bool, number
    defaultValue: any;
  }

  export interface ProtocolMetadata {
    providerKey: string;
    providerName: string;
    logo: string;
    isVendor: boolean;
    protocolClassName: string;
    envSchema: ProtocolEnvField[];
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
    vendor: string;
    protocols: string;
  }
}

// ===== 全局设置 =====

export async function getSettingsApi() {
  const resp = await requestClient.get('/settings');
  return (resp as any)?.data ?? resp;
}

export async function updateSettingsApi(data: SettingsApi.GlobalSettings) {
  const resp = await requestClient.put('/settings', data);
  return (resp as any)?.data ?? resp;
}

// ===== 协议元数据 =====

export async function getProtocolsApi(): Promise<
  SettingsApi.ProtocolMetadata[]
> {
  const resp = await requestClient.get('/protocols');
  return (resp as any)?.data ?? resp;
}

// ===== 供应商账户 =====

export async function getProviderAccountsApi(): Promise<
  SettingsApi.ProviderAccount[]
> {
  const resp = await requestClient.get('/provider-accounts');
  return (resp as any)?.data ?? resp;
}

export async function getProviderAccountApi(
  id: string,
): Promise<SettingsApi.ProviderAccount> {
  const resp = await requestClient.get(`/provider-accounts/${id}`);
  return (resp as any)?.data ?? resp;
}

export async function createProviderAccountApi(
  data: SettingsApi.CreateProviderAccountParams,
): Promise<SettingsApi.ProviderAccount> {
  const resp = await requestClient.post('/provider-accounts', data);
  return (resp as any)?.data ?? resp;
}

export async function updateProviderAccountApi(
  id: string,
  data: SettingsApi.UpdateProviderAccountParams,
): Promise<SettingsApi.ProviderAccount> {
  const resp = await requestClient.put(`/provider-accounts/${id}`, data);
  return (resp as any)?.data ?? resp;
}

export async function deleteProviderAccountApi(id: string): Promise<void> {
  await requestClient.delete(`/provider-accounts/${id}`);
}

// ===== 设备码授权 =====

export async function initiateDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceCodeResponse> {
  const resp = await requestClient.post(
    `/provider-accounts/${id}/device-auth`,
  );
  return (resp as any)?.data ?? resp;
}

export async function pollDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceAuthPollResult> {
  const resp = await requestClient.post(
    `/provider-accounts/${id}/device-auth/poll`,
  );
  return (resp as any)?.data ?? resp;
}

export async function cancelDeviceAuthApi(id: string): Promise<void> {
  await requestClient.delete(`/provider-accounts/${id}/device-auth`);
}

// ===== 模型列表 =====

export async function getProviderModelsApi(
  id: string,
): Promise<SettingsApi.ProviderModel[]> {
  const resp = await requestClient.get(`/provider-accounts/${id}/models`);
  return (resp as any)?.data ?? resp;
}


