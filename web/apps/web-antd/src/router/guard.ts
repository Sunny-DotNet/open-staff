import type { Router } from 'vue-router';

import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';
import { startProgress, stopProgress } from '@vben/utils';

import { accessRoutes, coreRouteNames } from '#/router/routes';

import { generateAccess } from './access';

/**
 * 通用守卫配置
 */
function setupCommonGuard(router: Router) {
  const loadedPaths = new Set<string>();

  router.beforeEach((to) => {
    to.meta.loaded = loadedPaths.has(to.path);
    if (!to.meta.loaded && preferences.transition.progress) {
      startProgress();
    }
    return true;
  });

  router.afterEach((to) => {
    loadedPaths.add(to.path);
    if (preferences.transition.progress) {
      stopProgress();
    }
  });
}

/**
 * 无认证路由守卫 — 首次访问时自动生成动态路由
 */
function setupAccessGuard(router: Router) {
  router.beforeEach(async (to, from) => {
    const accessStore = useAccessStore();

    if (coreRouteNames.includes(to.name as string)) {
      return true;
    }

    if (accessStore.isAccessChecked) {
      return true;
    }

    // 首次访问：直接生成路由（无需认证）
    const { accessibleMenus, accessibleRoutes } = await generateAccess({
      roles: ['admin'],
      router,
      routes: accessRoutes,
    });

    accessStore.setAccessMenus(accessibleMenus);
    accessStore.setAccessRoutes(accessibleRoutes);
    accessStore.setIsAccessChecked(true);

    const redirectPath = (from.query.redirect ?? to.fullPath) as string;

    return {
      ...router.resolve(decodeURIComponent(redirectPath)),
      replace: true,
    };
  });
}

function createRouterGuard(router: Router) {
  setupCommonGuard(router);
  setupAccessGuard(router);
}

export { createRouterGuard };
