import { client } from './client.gen';

let configured = false;
let getLanguage: (() => null | string | undefined) | undefined;
let onError:
  | ((error: { message?: string; response: Response; status: number }) => void)
  | undefined;

function normalizeOpenStaffBaseUrl(apiURL: string) {
  const normalized = apiURL.replace(/\/api\/?$/, '');
  return normalized.endsWith('/') ? normalized.slice(0, -1) : normalized;
}

function tryGetErrorMessage(value: unknown): string | undefined {
  if (!value || typeof value !== 'object') {
    return undefined;
  }

  const candidate = value as {
    data?: { error?: string; message?: string };
    error?: string;
    message?: string;
  };
  return (
    candidate.message ??
    candidate.error ??
    candidate.data?.message ??
    candidate.data?.error
  );
}

export function configureOpenStaffClient(options: {
  baseUrl: string;
  getLanguage?: () => null | string | undefined;
  onError?: (error: { message?: string; response: Response; status: number }) => void;
}) {
  getLanguage = options.getLanguage;
  onError = options.onError;
  client.setConfig({
    baseUrl: normalizeOpenStaffBaseUrl(options.baseUrl),
    responseStyle: 'data',
    throwOnError: true,
  });

  if (configured) {
    return;
  }

  client.interceptors.request.use((request) => {
    const language = getLanguage?.();
    if (language) {
      request.headers.set('Accept-Language', language);
    }
    return request;
  });

  client.interceptors.response.use(async (response) => {
    if (!response.ok) {
      let message: string | undefined;
      const contentType = response.headers.get('content-type') ?? '';
      if (contentType.includes('json')) {
        try {
          const body = await response.clone().json();
          message = tryGetErrorMessage(body);
        } catch {
          // Ignore JSON parsing failures for non-standard error responses.
        }
      }

      onError?.({
        message,
        response,
        status: response.status,
      });
    }

    return response;
  });

  configured = true;
}

export { client as openStaffClient };
