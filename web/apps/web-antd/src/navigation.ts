export type ModuleDefinition = {
  key: string;
  title: string;
  path: string;
  icon: string;
  section: 'workspace' | 'agents' | 'platform';
  controller: string;
  description: string;
  endpoints: string[];
  status: 'live' | 'scaffolded';
};

export const navigationModules: ModuleDefinition[] = [
  {
    key: 'projects',
    title: 'Projects',
    path: '/projects',
    icon: 'lucide:folder-kanban',
    section: 'workspace',
    controller: 'ProjectsController',
    description: '项目、README、导入导出、初始化、启动与项目内子资源。',
    endpoints: ['/api/projects', '/api/projects/{id}', '/api/projects/import', '/api/projects/{id}/start'],
    status: 'live',
  },
  {
    key: 'sessions',
    title: 'Sessions',
    path: '/sessions',
    icon: 'lucide:messages-square',
    section: 'workspace',
    controller: 'SessionsController',
    description: '会话列表、消息流、事件流与 frame/message 明细。',
    endpoints: ['/api/sessions', '/api/sessions/{sessionId}', '/hubs/notification'],
    status: 'scaffolded',
  },
  {
    key: 'tasks',
    title: 'Tasks',
    path: '/tasks',
    icon: 'lucide:list-todo',
    section: 'workspace',
    controller: 'TasksController',
    description: '项目内任务、批量状态更新、恢复与时间线视图。',
    endpoints: ['/api/projects/{projectId}/tasks', '/api/projects/{projectId}/tasks/{taskId}/timeline'],
    status: 'scaffolded',
  },
  {
    key: 'agents',
    title: 'Agents',
    path: '/agents',
    icon: 'lucide:bot',
    section: 'agents',
    controller: 'AgentsController',
    description: '项目 Agent 列表、发消息与事件明细。',
    endpoints: ['/api/projects/{projectId}/agents', '/api/projects/{projectId}/agents/{agentId}/message'],
    status: 'scaffolded',
  },
  {
    key: 'agent-roles',
    title: 'Agent Roles',
    path: '/agent-roles',
    icon: 'lucide:badge-check',
    section: 'agents',
    controller: 'AgentRolesController',
    description: '角色 CRUD、测试聊天、vendor 配置与模型目录。',
    endpoints: ['/api/agent-roles', '/api/agent-roles/{id}/test-chat', '/api/agent-roles/vendor/{providerType}/models'],
    status: 'live',
  },
  {
    key: 'talent-market',
    title: 'Talent Market',
    path: '/talent-market',
    icon: 'lucide:briefcase-business',
    section: 'agents',
    controller: 'TalentMarketController',
    description: '远程角色市场搜索、预览聘用与本地导入。',
    endpoints: ['/api/talent-market/sources', '/api/talent-market/search', '/api/talent-market/preview-hire', '/api/talent-market/hire'],
    status: 'live',
  },
  {
    key: 'provider-accounts',
    title: 'Provider Accounts',
    path: '/provider-accounts',
    icon: 'lucide:key-round',
    section: 'platform',
    controller: 'ProviderAccountsController',
    description: '供应商账户 CRUD、配置加载/保存、模型列表与设备授权。',
    endpoints: ['/api/provider-accounts', '/api/provider-accounts/providers', '/api/provider-accounts/{id}/models'],
    status: 'live',
  },
  {
    key: 'settings',
    title: 'Settings',
    path: '/settings',
    icon: 'lucide:settings-2',
    section: 'platform',
    controller: 'SettingsController',
    description: '系统与应用设置读取、保存。',
    endpoints: ['/api/settings', '/api/settings/system'],
    status: 'live',
  },
  {
    key: 'skills',
    title: 'Skills',
    path: '/skills',
    icon: 'lucide:sparkles',
    section: 'agents',
    controller: 'SkillsController',
    description: 'skills.sh catalog、技能详情、受管安装列表、安装与卸载。',
    endpoints: ['/api/skills/catalog', '/api/skills/sources', '/api/skills/installed', '/api/skills/install', '/api/skills/uninstall'],
    status: 'live',
  },
  {
    key: 'mcp',
    title: 'MCP',
    path: '/mcp',
    icon: 'lucide:plug-zap',
    section: 'agents',
    controller: 'McpServersController',
    description: 'MCP catalog、受管安装、服务定义、配置测试与作用域绑定。',
    endpoints: ['/api/mcp/sources', '/api/mcp/catalog/search', '/api/mcp/install', '/api/mcp/servers', '/api/mcp/configs'],
    status: 'live',
  },
  {
    key: 'marketplace',
    title: 'Marketplace',
    path: '/marketplace',
    icon: 'lucide:store',
    section: 'platform',
    controller: 'MarketplaceController',
    description: '插件与资源市场。',
    endpoints: ['/api/marketplace'],
    status: 'scaffolded',
  },
  {
    key: 'monitor',
    title: 'Monitor',
    path: '/monitor',
    icon: 'lucide:activity',
    section: 'platform',
    controller: 'MonitorController',
    description: '系统健康、全局统计、项目事件与项目统计。',
    endpoints: ['/api/monitor/health', '/api/monitor/stats', '/api/monitor/projects/{projectId}/events'],
    status: 'scaffolded',
  },
  {
    key: 'permission-requests',
    title: 'Permission Requests',
    path: '/permission-requests',
    icon: 'lucide:shield-alert',
    section: 'platform',
    controller: 'PermissionRequestsController',
    description: '审批与通知收件箱。',
    endpoints: ['/api/permission-requests/listeners', '/api/permission-requests/{requestId}/responses'],
    status: 'scaffolded',
  },
  {
    key: 'files',
    title: 'Files',
    path: '/files',
    icon: 'lucide:folder-search-2',
    section: 'workspace',
    controller: 'FilesController',
    description: '项目文件、内容读取与 diff 能力。',
    endpoints: ['/api/projects/{projectId}/files', '/api/projects/{projectId}/files/content', '/api/projects/{projectId}/files/diff'],
    status: 'scaffolded',
  },
  {
    key: 'model-data',
    title: 'Model Data',
    path: '/model-data',
    icon: 'lucide:database',
    section: 'platform',
    controller: 'ModelDataController',
    description: 'models.dev 供应商与模型索引、刷新状态。',
    endpoints: ['/api/models-dev/providers', '/api/models-dev/status'],
    status: 'scaffolded',
  },
];

export const navigationSections = [
  {
    key: 'agents',
    title: 'Agents',
    description: '角色、技能、MCP 与执行能力。',
    items: navigationModules.filter(
      (module) => module.section === 'agents' && module.key !== 'agents',
    ),
  },
  {
    key: 'platform',
    title: 'Platform',
    description: 'Provider 账户与平台设置。',
    items: navigationModules.filter(
      (module) =>
        module.section === 'platform'
        && ['provider-accounts', 'settings'].includes(module.key),
    ),
  },
] as const;

export function getModuleByKey(key: string) {
  return navigationModules.find((module) => module.key === key);
}
