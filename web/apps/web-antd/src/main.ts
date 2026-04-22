import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query';
import { configureOpenStaffClient } from '@openstaff/api';
import Antd, { message } from 'ant-design-vue';
import { initPreferences, preferences, updatePreferences } from '@vben/preferences';
import { initStores } from '@vben/stores';
import '@vben/styles';
import '@vben/styles/antd';
import 'dayjs/locale/zh-cn';
import { createApp } from 'vue';

import App from './App.vue';
import { setupAppI18n, t } from './i18n';
import { router, setupAccessRoutes } from './router';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
      staleTime: 30_000,
    },
  },
});

async function bootstrap() {
  const app = createApp(App);
  const logoSource = new URL('../../../open-staff.png', import.meta.url)
    .href;
  const preferenceOverrides: Parameters<typeof updatePreferences>[0] = {
    app: {
      defaultHomePath: '/overview',
      enableCheckUpdates: false,
      enableCopyPreferences: false,
      layout: 'sidebar-nav',
      name: 'OpenStaff',
    },
    copyright: {
      enable: false,
    },
    logo: {
      enable: true,
      fit: 'contain' as const,
      source: logoSource,
      sourceDark: logoSource,
    },
    sidebar: {
      width: 248,
    },
    theme: {
      mode: 'light' as const,
    },
    widget: {
      fullscreen: false,
      lockScreen: false,
      notification: false,
      timezone: false,
    },
  };

  app.use(Antd);

  await initStores(app, { namespace: 'openstaff' });
  await initPreferences({
    namespace: 'openstaff',
    overrides: preferenceOverrides,
  });
  updatePreferences(preferenceOverrides);

  await setupAppI18n(app);
  await setupAccessRoutes();

  configureOpenStaffClient({
    baseUrl: import.meta.env.DEV
      ? ''
      : (import.meta.env.VITE_OPENSTAFF_BASE_URL ?? window.location.origin),
    getLanguage: () => preferences.app.locale,
    onError: ({ message: errorMessage, status }) => {
      message.error(
        errorMessage ?? t('common.requestFailed', { status: status ?? 'unknown' }),
      );
    },
  });

  app.use(router);
  app.use(VueQueryPlugin, { queryClient });

  await router.isReady();
  app.mount('#app');
}

void bootstrap();
