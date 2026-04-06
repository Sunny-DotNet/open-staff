import type { RouteRecordRaw } from 'vue-router';

import { $t } from '#/locales';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:layout-dashboard',
      order: -1,
      title: $t('openstaff.dashboard.title'),
    },
    name: 'OpenStaffDashboard',
    path: '/dashboard',
    component: () => import('#/views/openstaff/dashboard/index.vue'),
  },
  {
    meta: {
      icon: 'lucide:folder-kanban',
      order: 0,
      title: $t('openstaff.project.title'),
      hideChildrenInMenu: true,
    },
    name: 'ProjectRoot',
    path: '/projects',
    redirect: '/projects/list',
    children: [
      {
        name: 'ProjectList',
        path: '/projects/list',
        component: () => import('#/views/openstaff/projects/index.vue'),
        meta: {
          title: $t('openstaff.project.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'AgentChat',
        path: '/projects/:id/chat',
        component: () => import('#/views/openstaff/agent-chat/index.vue'),
        meta: {
          icon: 'lucide:message-square',
          title: $t('openstaff.agentChat.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'TaskBoard',
        path: '/projects/:id/tasks',
        component: () => import('#/views/openstaff/task-board/index.vue'),
        meta: {
          icon: 'lucide:kanban',
          title: $t('openstaff.taskBoard.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'ProjectSettings',
        path: '/projects/:id/settings',
        component: () =>
          import('#/views/openstaff/project-settings/index.vue'),
        meta: {
          icon: 'lucide:settings',
          title: '项目配置',
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'CodeDiff',
        path: '/projects/:id/diff',
        component: () => import('#/views/openstaff/code-diff/index.vue'),
        meta: {
          icon: 'lucide:git-compare',
          title: $t('openstaff.codeDiff.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'FileExplorer',
        path: '/projects/:id/files',
        component: () => import('#/views/openstaff/file-explorer/index.vue'),
        meta: {
          icon: 'lucide:folder-tree',
          title: $t('openstaff.fileExplorer.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
      {
        name: 'ProjectMonitor',
        path: '/projects/:id/monitor',
        component: () => import('#/views/openstaff/monitor/index.vue'),
        meta: {
          icon: 'lucide:activity',
          title: $t('openstaff.monitor.title'),
          hideInMenu: true,
          activePath: '/projects',
        },
      },
    ],
  },
  {
    meta: {
      icon: 'lucide:users',
      order: 50,
      title: $t('openstaff.staff.title'),
    },
    name: 'StaffManagement',
    path: '/staff',
    component: () => import('#/views/openstaff/agent-config/index.vue'),
  },
  {
    meta: {
      icon: 'lucide:puzzle',
      order: 55,
      title: 'MCP 市场',
    },
    name: 'McpMarket',
    path: '/mcp-market',
    component: () => import('#/views/openstaff/mcp-market/index.vue'),
  },
  {
    meta: {
      icon: 'lucide:cloud',
      order: 60,
      title: '供应商管理',
    },
    name: 'ProviderManagement',
    path: '/providers',
    component: () => import('#/views/openstaff/providers/index.vue'),
  },
  {
    meta: {
      icon: 'lucide:settings',
      order: 100,
      title: $t('openstaff.settings.title'),
    },
    name: 'OpenStaffSettings',
    path: '/settings',
    component: () => import('#/views/openstaff/settings/index.vue'),
  },
];

export default routes;
