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
    envConfig?: Record<string, string | boolean | number>;
    createdAt: string;
    updatedAt: string;
  }

  export interface CreateProviderAccountParams {
    name: string;
    protocolType: string;
    envConfig?: Record<string, string | boolean | number>;
    isEnabled?: boolean;
  }

  export interface UpdateProviderAccountParams {
    name?: string;
    envConfig?: Record<string, string | boolean | number>;
    isEnabled?: boolean;
  }

  /** 协议元数据 */
  export interface ProtocolEnvField {
    name: string;
    fieldType: 'bool' | 'number' | 'secret' | 'string';
    defaultValue: string | boolean | number;
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

export async function getSettingsApi(): Promise<SettingsApi.GlobalSettings> {
  return requestClient.get<SettingsApi.GlobalSettings>('/settings');
}

export async function updateSettingsApi(
  data: SettingsApi.GlobalSettings,
): Promise<SettingsApi.GlobalSettings> {
  return requestClient.put<SettingsApi.GlobalSettings>('/settings', data);
}

// ===== 协议元数据 =====

export async function getProtocolsApi(): Promise<
  SettingsApi.ProtocolMetadata[]
> {
  return requestClient.get<SettingsApi.ProtocolMetadata[]>('/protocols');
}

// ===== 供应商账户 =====

export async function getProviderAccountsApi(): Promise<
  SettingsApi.ProviderAccount[]
> {
  return requestClient.get<SettingsApi.ProviderAccount[]>('/provider-accounts');
}

export async function getProviderAccountApi(
  id: string,
): Promise<SettingsApi.ProviderAccount> {
  return requestClient.get<SettingsApi.ProviderAccount>(
    `/provider-accounts/${id}`,
  );
}

export async function createProviderAccountApi(
  data: SettingsApi.CreateProviderAccountParams,
): Promise<SettingsApi.ProviderAccount> {
  return requestClient.post<SettingsApi.ProviderAccount>(
    '/provider-accounts',
    data,
  );
}

export async function updateProviderAccountApi(
  id: string,
  data: SettingsApi.UpdateProviderAccountParams,
): Promise<SettingsApi.ProviderAccount> {
  return requestClient.put<SettingsApi.ProviderAccount>(
    `/provider-accounts/${id}`,
    data,
  );
}

export async function deleteProviderAccountApi(id: string): Promise<void> {
  await requestClient.delete(`/provider-accounts/${id}`);
}

// ===== 设备码授权 =====

export async function initiateDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceCodeResponse> {
  return requestClient.post<SettingsApi.DeviceCodeResponse>(
    `/provider-accounts/${id}/device-auth`,
  );
}

export async function pollDeviceAuthApi(
  id: string,
): Promise<SettingsApi.DeviceAuthPollResult> {
  return requestClient.post<SettingsApi.DeviceAuthPollResult>(
    `/provider-accounts/${id}/device-auth/poll`,
  );
}

export async function cancelDeviceAuthApi(id: string): Promise<void> {
  await requestClient.delete(`/provider-accounts/${id}/device-auth`);
}

// ===== 模型列表 =====

export async function getProviderModelsApi(
  id: string,
): Promise<SettingsApi.ProviderModel[]> {
  return requestClient.get<SettingsApi.ProviderModel[]>(
    `/provider-accounts/${id}/models`,
  );
}
