import type { RouteRecordRaw } from 'vue-router';

import { generateAccessible } from '@vben/access';
import { useAccessStore } from '@vben/stores';
import { createRouter, createWebHistory } from 'vue-router';

import AppLayout from '@/layouts/AppLayout.vue';
import { getModuleTitleKey, getSectionTitleKey } from '@/i18n';
import { navigationSections } from '@/navigation';
import DashboardPage from '@/pages/DashboardPage.vue';
import ModulePage from '@/pages/ModulePage.vue';
import AgentRolesPage from '@/pages/agent-roles/index.vue';
import McpPage from '@/pages/mcp/index.vue';
import ProjectsPage from '@/pages/projects/index.vue';
import ProjectWorkspacePage from '@/pages/projects/workspace.vue';
import ProviderAccountsPage from '@/pages/provider-accounts/index.vue';
import SettingsPage from '@/pages/settings/index.vue';
import SkillsPage from '@/pages/skills/index.vue';
import TalentMarketPage from '@/pages/talent-market/index.vue';

const sectionIcons: Record<string, string> = {
  agents: 'lucide:bot',
  platform: 'lucide:settings-2',
};

const sectionOrders: Record<string, number> = {
  agents: 20,
  platform: 30,
};

const appRoutes: RouteRecordRaw[] = [
  {
    path: '/overview',
    name: 'overview',
    component: DashboardPage,
    meta: {
      affixTab: true,
      icon: 'lucide:layout-dashboard',
      keepAlive: true,
      order: 0,
      title: 'menu.overview',
    },
  },
  {
    path: '/projects',
    name: 'projects',
    component: ProjectsPage,
    meta: {
      icon: 'lucide:folder-kanban',
      keepAlive: true,
      order: 10,
      title: getModuleTitleKey('projects'),
    },
  },
  ...navigationSections.map((section) => ({
    path: `/${section.key}`,
    name: `section-${section.key}`,
    redirect: section.items[0]?.path ?? '/overview',
    meta: {
      icon: sectionIcons[section.key],
      order: sectionOrders[section.key] ?? 99,
      title: getSectionTitleKey(section.key),
    },
    children: section.items.map((module, index) => ({
      path: module.path,
      name: module.key,
        component:
          module.key === 'provider-accounts'
            ? ProviderAccountsPage
            : module.key === 'projects'
              ? ProjectsPage
            : module.key === 'agent-roles'
              ? AgentRolesPage
              : module.key === 'talent-market'
                ? TalentMarketPage
              : module.key === 'settings'
                ? SettingsPage
              : module.key === 'skills'
                ? SkillsPage
              : module.key === 'mcp'
                ? McpPage
              : ModulePage,
        props:
          module.key === 'provider-accounts' || module.key === 'projects' || module.key === 'agent-roles' || module.key === 'talent-market' || module.key === 'mcp' || module.key === 'skills' || module.key === 'settings'
            ? undefined
            : { moduleKey: module.key },
        meta: {
          icon: module.icon,
          keepAlive: module.key === 'provider-accounts' || module.key === 'projects' || module.key === 'agent-roles' || module.key === 'talent-market' || module.key === 'mcp' || module.key === 'skills' || module.key === 'settings',
          order: index + 1,
          title: getModuleTitleKey(module.key),
        },
    })),
  })),
  {
    path: '/projects/:id',
    name: 'project-workspace',
    component: ProjectWorkspacePage,
    meta: {
      fullPathKey: false,
      hideInMenu: true,
      icon: 'lucide:folder-kanban',
      title: getModuleTitleKey('projects'),
    },
  },
];

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'root',
    component: AppLayout,
    redirect: '/overview',
    children: [],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/overview',
    meta: {
      hideInMenu: true,
      hideInTab: true,
      noBasicLayout: true,
      title: 'menu.overview',
    },
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

let accessibleInitialized = false;

export async function setupAccessRoutes() {
  if (accessibleInitialized) {
    return;
  }

  const accessStore = useAccessStore();
  const { accessibleMenus, accessibleRoutes } = await generateAccessible(
    'frontend',
    {
      router,
      routes: appRoutes,
    },
  );

  accessStore.setAccessMenus(accessibleMenus);
  accessStore.setAccessRoutes(accessibleRoutes);
  accessStore.setIsAccessChecked(true);

  accessibleInitialized = true;
}
