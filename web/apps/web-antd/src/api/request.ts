/**
 * 该文件可自行根据业务逻辑进行调整
 */
import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';

import { message } from 'ant-design-vue';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
  });

  // 请求头处理
  client.addRequestInterceptor({
    fulfilled: async (config) => {
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  // 自动解包 AxiosResponse.data
  client.addResponseInterceptor({
    fulfilled: (response: any) => response.data,
  });

  // 通用的错误处理
  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseData = error?.response?.data ?? {};
      const errorMessage = responseData?.error ?? responseData?.message ?? '';
      message.error(errorMessage || msg);
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });
