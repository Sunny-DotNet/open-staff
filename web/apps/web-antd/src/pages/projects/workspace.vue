<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';
import type {
  AgentDto,
  AgentRoleDto,
  ProjectAgentMcpBindingDto,
  ProjectAgentMcpBindingInput,
  ProjectDto,
  ProjectStatsDto,
  ProviderAccountDto,
  SessionDto,
} from '@openstaff/api';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { VbenIcon } from '@vben-core/shadcn-ui';
import {
  getApiAgentRoles,
  getApiMcpProjectAgentBindingsByProjectAgentId,
  getApiMcpServers,
  getApiMonitorProjectsByProjectIdStats,
  getApiProjectsById,
  getApiProjectsByProjectIdAgents,
  getApiProviderAccounts,
  getApiSessionsByProjectByProjectId,
  postApiProjectsByIdExport,
  postApiProjectsByIdInitialize,
  postApiProjectsByIdStart,
  postApiMcpBindingDraft,
  putApiMcpProjectAgentBindingsByProjectAgentId,
  putApiProjectsById,
  putApiProjectsByProjectIdAgents,
  unwrapCollection,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { computed, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { t } from '@/i18n';
import { localizeJobTitle, normalizeJobTitleKey } from '@/utils/job-title';

import type { McpServerView } from '../mcp/api';

import McpBindingAccordionEditor from '../agent-roles/McpBindingAccordionEditor.vue';
import ProjectBrainstormTab from './ProjectBrainstormTab.vue';
import ProjectChatTab from './ProjectChatTab.vue';
import ProjectFilesTab from './ProjectFilesTab.vue';
import ProjectMonitorTab from './ProjectMonitorTab.vue';
import ProjectTasksTab from './ProjectTasksTab.vue';
import { normalizeOptionalJson } from '../mcp/binding-utils';
import type { McpParameterValues } from '../mcp/structured-values';
import {
  buildDefaultParameterValues,
  mergeParameterValues,
  parseParameterValues,
  stringifyParameterValues,
} from '../mcp/structured-values';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const MCP_MODE_LABELS = {
  local: { color: 'blue', label: '本地' },
  remote: { color: 'purple', label: '远程' },
  unknown: { color: 'default', label: '未知' },
};

interface EditableMcpBinding {
  localId: string;
  mcpServerId: string;
  mcpServerName: string;
  icon?: null | string;
  isEnabled: boolean;
  mode?: null | string;
  selectedProfileId?: null | string;
  parameterValues: McpParameterValues;
  toolFilter?: null | string;
  transportType?: null | string;
}

interface McpBindingIssue {
  toolFilter?: string;
}

type WorkspaceTabKey =
  | 'brainstorm'
  | 'chat'
  | 'files'
  | 'monitor'
  | 'overview'
  | 'settings'
  | 'tasks';

interface WorkspaceTabCard {
  description: string;
  icon: string;
  key: Exclude<WorkspaceTabKey, 'overview'>;
  title: string;
}

const route = useRoute();
const router = useRouter();

const activeTab = ref<WorkspaceTabKey>('overview');
const selectedAgentRoleIds = ref<string[]>([]);
const selectedProjectAgentId = ref('');
const pendingMcpServerId = ref<string>();
const activeMcpBindingKeys = ref<string[]>([]);
const mcpBindings = ref<EditableMcpBinding[]>([]);
const mcpBindingsError = ref('');
const mcpBindingsLoadedFor = ref('');
const mcpBindingsLoading = ref(false);
const addingMcpBinding = ref(false);

const form = reactive({
  defaultModelName: '',
  defaultProviderId: undefined as string | undefined,
  description: '',
  extraConfig: '',
  language: 'zh-CN',
  name: '',
});

const projectId = computed(() => String(route.params.id ?? ''));

const projectQuery = useQuery({
  queryKey: ['projects', 'detail', projectId],
  enabled: computed(() => !!projectId.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsById({
        path: { id: projectId.value },
      }),
    ),
});

const providerAccountsQuery = useQuery({
  queryKey: ['provider-accounts', 'project-workspace'],
  queryFn: async () =>
    unwrapCollection(unwrapClientEnvelope(await getApiProviderAccounts())),
});

const agentRolesQuery = useQuery({
  queryKey: ['agent-roles', 'project-workspace'],
  queryFn: async () => unwrapClientEnvelope(await getApiAgentRoles()),
});

const projectAgentsQuery = useQuery({
  queryKey: ['projects', 'agents', projectId],
  enabled: computed(() => !!projectId.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdAgents({
        path: { projectId: projectId.value },
      }),
    ),
});

const statsQuery = useQuery({
  queryKey: ['projects', 'stats', projectId],
  enabled: computed(() => !!projectId.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiMonitorProjectsByProjectIdStats({
        path: { projectId: projectId.value },
      }),
    ),
});

const sessionsQuery = useQuery({
  queryKey: ['projects', 'sessions', projectId],
  enabled: computed(() => !!projectId.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiSessionsByProjectByProjectId({
        path: { projectId: projectId.value },
      }),
    ),
});

const mcpServersQuery = useQuery({
  queryKey: ['mcp', 'servers', 'project-workspace'],
  queryFn: async () =>
    (unwrapClientEnvelope(await getApiMcpServers()) as McpServerView[]).map((server) => ({
      ...server,
    })),
});

const project = computed<ProjectDto | null>(() => projectQuery.data.value ?? null);
const providers = computed<ProviderAccountDto[]>(() => providerAccountsQuery.data.value ?? []);
const roles = computed<AgentRoleDto[]>(() => agentRolesQuery.data.value ?? []);
const projectAgents = computed<AgentDto[]>(() => projectAgentsQuery.data.value ?? []);
const stats = computed<ProjectStatsDto | null>(() => statsQuery.data.value ?? null);
const sessions = computed<SessionDto[]>(() => sessionsQuery.data.value ?? []);
const mcpServers = computed<McpServerView[]>(() => mcpServersQuery.data.value ?? []);

const pageTitle = computed(
  () => project.value?.name?.trim() || t('project.workspaceTitle'),
);

const providerOptions = computed(() =>
  providers.value
    .filter((provider) => provider.id)
    .map((provider) => ({
      label: provider.name ?? provider.protocolType ?? provider.id ?? '--',
      value: provider.id ?? '',
    })),
);

const assignableRoleOptions = computed(() =>
  roles.value
    .filter((role) => role.id && role.id !== EMPTY_GUID && !role.isVirtual)
    .map((role) => ({
      label:
        role.name ||
        localizeJobTitle(role.jobTitle, role.jobTitle) ||
        role.roleType ||
        role.id ||
        t('role.unnamedRole'),
      value: role.id ?? '',
    })),
);

const availableMcpServers = computed(() => {
  const boundIds = new Set(mcpBindings.value.map((binding) => binding.mcpServerId));
  return mcpServers.value.filter(
    (server) => server.isEnabled && server.id && !boundIds.has(server.id),
  );
});

const mcpBindingIssues = computed<Record<string, McpBindingIssue>>(() => {
  const issues: Record<string, McpBindingIssue> = {};

  for (const binding of mcpBindings.value) {
    const issue: McpBindingIssue = {};

    if (binding.toolFilter?.trim()) {
      try {
        const parsed = JSON.parse(binding.toolFilter);
        if (!Array.isArray(parsed) || parsed.some((item) => typeof item !== 'string')) {
          issue.toolFilter = t('project.invalidToolFilterShape');
        }
      } catch {
        issue.toolFilter = t('project.invalidToolFilter');
      }
    }

    if (issue.toolFilter) {
      issues[binding.localId] = issue;
    }
  }

  return issues;
});

const isSettingsTabActive = computed(() => activeTab.value === 'settings');
const isBrainstormStage = computed(
  () => project.value?.phase !== 'running' && project.value?.phase !== 'completed',
);
const visibleWorkspaceTabs = computed<WorkspaceTabKey[]>(() =>
  isBrainstormStage.value
    ? ['overview', 'brainstorm', 'settings']
    : ['overview', 'chat', 'tasks', 'files', 'monitor', 'settings'],
);

const projectOverviewItems = computed<AnalysisOverviewItem[]>(() => [
  {
    title: t('project.assignedAgents'),
    icon: 'lucide:users-round',
    value: projectAgents.value.length,
    totalTitle: t('project.checkpoints'),
    totalValue: toNumber(stats.value?.checkpoints),
  },
  {
    title: t('project.sessionsCount'),
    icon: 'lucide:messages-square',
    value: sessions.value.length,
    totalTitle: t('project.runningTasks'),
    totalValue: countStatus(stats.value?.tasksByStatus, 'in_progress'),
  },
  {
    title: t('project.todoTasks'),
    icon: 'lucide:list-todo',
    value: countStatus(stats.value?.tasksByStatus, 'pending'),
    totalTitle: t('project.doneTasks'),
    totalValue: countStatus(stats.value?.tasksByStatus, 'done'),
  },
  {
    title: t('project.eventTypes'),
    icon: 'lucide:activity',
    value: Object.keys(stats.value?.eventsByType ?? {}).length,
    totalTitle: t('project.recentEvents'),
    totalValue: stats.value?.recentEvents?.length ?? 0,
  },
]);

const projectChatHeaderAgents = computed(() => {
  const roleMap = new Map(
    roles.value
      .filter((role) => role.id)
      .map((role) => [role.id as string, role]),
  );

  return projectAgents.value.map((agent, index) => {
    const role = agent.agentRoleId ? roleMap.get(agent.agentRoleId) : undefined;
    return {
      avatar: role?.avatar ?? undefined,
      jobTitle: role?.jobTitle ?? undefined,
      key: agent.id ?? `${agent.agentRoleId ?? 'agent'}-${index}`,
      label:
        role?.name
        || agent.roleName
        || role?.jobTitle
        || role?.roleType
        || t('role.unnamedRole'),
      projectAgentRoleId: agent.id ?? undefined,
      role: role ?? null,
    };
  });
});

function matchesSecretaryRole(value?: null | string) {
  const normalized = normalizeJobTitleKey(value) ?? value?.trim().toLowerCase();
  if (!normalized) {
    return false;
  }

  return normalized.includes('secretary') || normalized.includes('秘书');
}

const globalBrainstormRole = computed(() =>
  roles.value.find((role) =>
    matchesSecretaryRole(role.name)
    || matchesSecretaryRole(role.jobTitle)
    || matchesSecretaryRole(role.roleType),
  ) ?? null,
);

const brainstormHeaderAgent = computed(() => {
  const projectSecretaryAgent = projectChatHeaderAgents.value.find((agent) =>
    matchesSecretaryRole(agent.label)
    || matchesSecretaryRole(agent.jobTitle)
    || matchesSecretaryRole(agent.role?.name)
    || matchesSecretaryRole(agent.role?.jobTitle)
    || matchesSecretaryRole(agent.role?.roleType),
  );

  if (projectSecretaryAgent) {
    return projectSecretaryAgent;
  }

  if (globalBrainstormRole.value) {
    return {
      avatar: globalBrainstormRole.value.avatar ?? undefined,
      jobTitle: globalBrainstormRole.value.jobTitle ?? undefined,
      key: globalBrainstormRole.value.id ?? 'global-brainstorm-agent',
      label:
        globalBrainstormRole.value.name
        || globalBrainstormRole.value.jobTitle
        || globalBrainstormRole.value.roleType
        || t('project.brainstormAgentName'),
      role: globalBrainstormRole.value,
    };
  }

  return projectChatHeaderAgents.value[0] ?? null;
});

const moduleEntryCards = computed(() => {
  const cards: WorkspaceTabCard[] = [
    {
      description: t('project.brainstormDescription'),
      icon: 'lucide:lightbulb',
      key: 'brainstorm',
      title: t('project.brainstorm'),
    },
    {
      description: t('project.chatDescription'),
      icon: 'lucide:messages-square',
      key: 'chat',
      title: t('project.chat'),
    },
    {
      description: t('project.tasksDescription'),
      icon: 'lucide:list-todo',
      key: 'tasks',
      title: t('project.tasks'),
    },
    {
      description: t('project.filesDescription'),
      icon: 'lucide:folder-open',
      key: 'files',
      title: t('project.files'),
    },
    {
      description: t('project.monitorDescription'),
      icon: 'lucide:activity',
      key: 'monitor',
      title: t('project.monitor'),
    },
    {
      description: t('project.settingsDescription'),
      icon: 'lucide:sliders-horizontal',
      key: 'settings',
      title: t('project.settings'),
    },
  ];

  return cards.filter((item) => visibleWorkspaceTabs.value.includes(item.key));
});

const saveProjectMutation = useMutation({
  mutationFn: async () => {
    if (!projectId.value) {
      throw new Error(t('project.validationProject'));
    }

    const extraConfig = form.extraConfig.trim();
    if (extraConfig) {
      JSON.parse(extraConfig);
    }

    return unwrapClientEnvelope(
      await putApiProjectsById({
        body: {
          defaultModelName: form.defaultModelName.trim() || null,
          defaultProviderId: form.defaultProviderId || null,
          description: form.description.trim() || null,
          extraConfig: extraConfig || null,
          language: form.language || null,
          name: form.name.trim(),
        },
        path: { id: projectId.value },
      }),
    );
  },
  onSuccess: async () => {
    message.success(t('project.updateSuccess'));
    await projectQuery.refetch();
  },
});

const saveAgentsMutation = useMutation({
  mutationFn: async () => {
    if (!projectId.value) {
      throw new Error(t('project.validationProject'));
    }

    return putApiProjectsByProjectIdAgents({
      body: {
        agentRoleIds: selectedAgentRoleIds.value,
      },
      path: { projectId: projectId.value },
    });
  },
  onSuccess: async () => {
    message.success(t('project.membersSaveSuccess'));
    await Promise.all([projectAgentsQuery.refetch(), statsQuery.refetch()]);
  },
});

const saveMcpBindingsMutation = useMutation({
  mutationFn: async () => {
    if (!selectedProjectAgentId.value) {
      throw new Error(t('project.validationProjectAgent'));
    }

    const firstIssue = Object.values(mcpBindingIssues.value)[0];
    if (firstIssue?.toolFilter) {
      throw new Error(firstIssue.toolFilter ?? t('project.actionFailed'));
    }

    return putApiMcpProjectAgentBindingsByProjectAgentId({
      body: mcpBindings.value.map<ProjectAgentMcpBindingInput>((binding) => ({
        isEnabled: binding.isEnabled,
        mcpServerId: binding.mcpServerId,
        selectedProfileId: binding.selectedProfileId || null,
        parameterValues: stringifyParameterValues(binding.parameterValues),
        toolFilter: normalizeOptionalJson(binding.toolFilter),
      })),
        path: { projectAgentId: selectedProjectAgentId.value },
    });
  },
  onSuccess: async () => {
    message.success(t('project.bindingsSaveSuccess'));
    await loadMcpBindings(selectedProjectAgentId.value);
  },
});

const initializeProjectMutation = useMutation({
  mutationFn: async () => {
    if (!projectId.value) {
      throw new Error(t('project.validationProject'));
    }

    return unwrapClientEnvelope(
      await postApiProjectsByIdInitialize({
        path: { id: projectId.value },
      }),
    );
  },
  onSuccess: async () => {
    message.success(t('project.initializeSuccess'));
    await refreshAll();
  },
});

const startProjectMutation = useMutation({
  mutationFn: async () => {
    if (!projectId.value) {
      throw new Error(t('project.validationProject'));
    }

    return unwrapClientEnvelope(
      await postApiProjectsByIdStart({
        path: { id: projectId.value },
      }),
    );
  },
  onSuccess: async () => {
    message.success(t('project.startSuccess'));
    await refreshAll();
  },
});

const exportProjectMutation = useMutation({
  mutationFn: async () => {
    if (!projectId.value) {
      throw new Error(t('project.validationProject'));
    }

    return postApiProjectsByIdExport({
      parseAs: 'blob',
      path: { id: projectId.value },
    });
  },
  onSuccess: (result) => {
    const fallbackName = `${project.value?.name || 'project'}.openstaff`;
    const fileName = resolveExportFileName(
      result.response.headers.get('Content-Disposition'),
      fallbackName,
    );
    const blob = result.data instanceof Blob ? result.data : new Blob([]);
    downloadBlob(blob, fileName);
    message.success(t('project.exportSuccess'));
  },
});

watch(
  () => [route.query.tab, visibleWorkspaceTabs.value.join('|')] as const,
  ([tabValue]) => {
    const nextTab = normalizeWorkspaceTab(Array.isArray(tabValue) ? tabValue[0] : tabValue);
    if (nextTab !== activeTab.value) {
      activeTab.value = nextTab;
    }
  },
  { immediate: true },
);

watch(
  () => project.value,
  (value) => {
    form.name = value?.name ?? '';
    form.description = value?.description ?? '';
    form.language = value?.language ?? 'zh-CN';
    form.defaultProviderId = value?.defaultProviderId ?? undefined;
    form.defaultModelName = value?.defaultModelName ?? '';
    form.extraConfig = value?.extraConfig ?? '';
  },
  { immediate: true },
);

watch(
  () => projectAgents.value,
  (value) => {
    selectedAgentRoleIds.value = value.flatMap((agent) =>
      agent.agentRoleId ? [agent.agentRoleId] : [],
    );

    if (!value.some((agent) => agent.id === selectedProjectAgentId.value)) {
      selectedProjectAgentId.value = value[0]?.id ?? '';
    }
  },
  { immediate: true },
);

watch(
  () => [activeTab.value, selectedProjectAgentId.value] as const,
  async ([tab, value]) => {
    if (tab !== 'settings') {
      return;
    }

    await ensureMcpBindingsLoaded(value);
  },
  { immediate: true },
);

async function ensureMcpBindingsLoaded(projectAgentId: string, force = false) {
  if (!projectAgentId) {
    mcpBindings.value = [];
    mcpBindingsLoadedFor.value = '';
    mcpBindingsError.value = '';
    return;
  }

  if (!force && mcpBindingsLoadedFor.value === projectAgentId && !mcpBindingsError.value) {
    return;
  }

  await loadMcpBindings(projectAgentId);
}

async function loadMcpBindings(projectAgentId: string) {
  if (!projectAgentId) {
    mcpBindings.value = [];
    mcpBindingsLoadedFor.value = '';
    mcpBindingsError.value = '';
    return;
  }

  mcpBindingsLoading.value = true;
  mcpBindingsError.value = '';

  try {
    const rows = unwrapClientEnvelope(
      await getApiMcpProjectAgentBindingsByProjectAgentId({
        path: { projectAgentId },
      }),
    ) as ProjectAgentMcpBindingDto[];

    mcpBindings.value = rows.map((binding, index) => ({
      icon: binding.icon ?? null,
      isEnabled: binding.isEnabled ?? true,
      localId: binding.id ?? `${binding.mcpServerId ?? 'mcp'}-${index}`,
      mcpServerId: binding.mcpServerId ?? '',
      mcpServerName: binding.mcpServerName ?? t('project.unnamedMcp'),
      mode: binding.mode ?? null,
      selectedProfileId: binding.selectedProfileId ?? null,
      parameterValues: mergeParameterValues(
        buildDefaultParameterValues(
          mcpServers.value.find((server) => server.id === binding.mcpServerId)?.parameterSchema,
          binding.selectedProfileId,
        ),
        parseParameterValues(binding.parameterValues),
      ),
      toolFilter: binding.toolFilter ?? null,
      transportType: binding.transportType ?? null,
    }));
    mcpBindingsLoadedFor.value = projectAgentId;
    activeMcpBindingKeys.value = [];
  } catch (error) {
    mcpBindings.value = [];
    mcpBindingsLoadedFor.value = '';
    activeMcpBindingKeys.value = [];
    mcpBindingsError.value = getErrorMessage(error, t('project.bindingsLoadFailed'));
    message.error(mcpBindingsError.value);
  } finally {
    mcpBindingsLoading.value = false;
  }
}

async function refreshAll() {
  await Promise.all([
    projectQuery.refetch(),
    projectAgentsQuery.refetch(),
    statsQuery.refetch(),
    sessionsQuery.refetch(),
    mcpServersQuery.refetch(),
  ]);

  if (isSettingsTabActive.value && selectedProjectAgentId.value) {
    await ensureMcpBindingsLoaded(selectedProjectAgentId.value, true);
  }
}

async function saveProject() {
  if (!form.name.trim()) {
    message.error(t('project.validationName'));
    return;
  }

  try {
    await saveProjectMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function saveMembers() {
  try {
    await saveAgentsMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function saveMcpBindings() {
  try {
    await saveMcpBindingsMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function initializeProject() {
  try {
    await initializeProjectMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function startProject() {
  try {
    await startProjectMutation.mutateAsync();
    activeTab.value = 'chat';
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function exportProject() {
  try {
    await exportProjectMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function addMcpBinding() {
  if (!pendingMcpServerId.value || !selectedProjectAgentId.value) {
    return;
  }

  addingMcpBinding.value = true;

  try {
    const server = availableMcpServers.value.find((item) => item.id === pendingMcpServerId.value);
    if (!server?.id) {
      return;
    }

    const draft = unwrapClientEnvelope(
      await postApiMcpBindingDraft({
        body: {
          mcpServerId: server.id,
          projectAgentRoleId: selectedProjectAgentId.value,
          scope: 'project-agent',
        },
      }),
    );

    const localId = `${server.id}-${Date.now()}`;
    mcpBindings.value.push({
      icon: server.icon ?? null,
      isEnabled: draft.isEnabled ?? true,
      localId,
      mcpServerId: server.id,
      mcpServerName: server.name ?? t('project.unnamedMcp'),
      mode: server.mode ?? null,
      selectedProfileId: draft.selectedProfileId ?? null,
      parameterValues: mergeParameterValues(
        buildDefaultParameterValues(server.parameterSchema, draft.selectedProfileId),
        parseParameterValues(draft.parameterValues),
      ),
      toolFilter: draft.toolFilter ?? null,
      transportType: server.transportType ?? null,
    });
    activeMcpBindingKeys.value = [localId];
    pendingMcpServerId.value = undefined;
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  } finally {
    addingMcpBinding.value = false;
  }
}

function removeMcpBinding(mcpServerId: string) {
  mcpBindings.value = mcpBindings.value.filter((binding) => binding.mcpServerId !== mcpServerId);
  activeMcpBindingKeys.value = activeMcpBindingKeys.value.filter((key) =>
    mcpBindings.value.some((binding) => binding.localId === key),
  );
}

function goBack() {
  void router.push('/projects');
}

function openWorkspaceTab(tab: Exclude<WorkspaceTabKey, 'overview'>) {
  activeTab.value = tab;
}

function resolveProviderName(providerId: null | string | undefined) {
  if (!providerId) {
    return '--';
  }

  const provider = providers.value.find((item) => item.id === providerId);
  return provider?.name ?? provider?.protocolType ?? providerId;
}

function resolveStatusColor(status: null | string | undefined) {
  switch (status) {
    case 'active':
      return 'success';
    case 'paused':
      return 'warning';
    case 'completed':
      return 'blue';
    case 'archived':
      return 'default';
    default:
      return 'processing';
  }
}

function resolvePhaseColor(phase: null | string | undefined) {
  switch (phase) {
    case 'ready_to_start':
      return 'gold';
    case 'running':
      return 'processing';
    case 'completed':
      return 'success';
    default:
      return 'default';
  }
}

function formatDate(value: null | string | undefined) {
  if (!value) {
    return '--';
  }

  return dayjs(value).format('YYYY-MM-DD HH:mm');
}

function toNumber(value: number | string | null | undefined) {
  if (typeof value === 'number') {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? 0 : parsed;
  }

  return 0;
}

function countStatus(
  source: null | Record<string, number | string> | undefined,
  key: string,
) {
  return toNumber(source?.[key]);
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

function resolveExportFileName(
  disposition: null | string,
  fallback: string,
) {
  if (!disposition) {
    return fallback;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const asciiMatch = /filename="?([^";]+)"?/i.exec(disposition);
  return asciiMatch?.[1] ?? fallback;
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

function normalizeWorkspaceTab(value: unknown): WorkspaceTabKey {
  if (typeof value === 'string' && visibleWorkspaceTabs.value.includes(value as WorkspaceTabKey)) {
    return value as WorkspaceTabKey;
  }

  return visibleWorkspaceTabs.value.includes('overview')
    ? 'overview'
    : visibleWorkspaceTabs.value[0] ?? 'overview';
}
</script>

<template>
  <Page
    auto-content-height
    :title="pageTitle"
    content-class="flex min-h-0 flex-col gap-5"
  >
    <template #extra>
      <a-space>
        <a-button @click="goBack">
          {{ t('project.backToList') }}
        </a-button>
        <a-button
          :loading="
            projectQuery.isFetching.value ||
            projectAgentsQuery.isFetching.value ||
            statsQuery.isFetching.value
          "
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button
          :loading="initializeProjectMutation.isPending.value"
          @click="initializeProject"
        >
          {{ t('project.initialize') }}
        </a-button>
        <a-button
          type="primary"
          :disabled="project?.phase !== 'ready_to_start'"
          :loading="startProjectMutation.isPending.value"
          @click="startProject"
        >
          {{ t('project.start') }}
        </a-button>
        <a-button
          :loading="exportProjectMutation.isPending.value"
          @click="exportProject"
        >
          {{ t('project.export') }}
        </a-button>
      </a-space>
    </template>

    <a-alert
      v-if="projectQuery.isError.value"
      show-icon
      type="error"
      :message="t('common.requestFailed', { status: 'project' })"
    />

    <template v-if="project">
      <a-tabs v-model:activeKey="activeTab" class="project-tabs">
        <a-tab-pane key="overview" :tab="t('project.overview')">
          <div class="space-y-5">
            <section v-if="!isBrainstormStage" class="card-box p-5">
              <div class="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                <div class="min-w-0">
                  <div class="flex flex-wrap items-center gap-2">
                    <h2 class="text-2xl font-semibold tracking-tight text-foreground">
                      {{ project.name || t('project.unnamed') }}
                    </h2>
                    <a-tag :color="resolveStatusColor(project.status)">
                      {{ t(`project.statusMap.${project.status || 'initializing'}`) }}
                    </a-tag>
                    <a-tag :color="resolvePhaseColor(project.phase)">
                      {{ t(`project.phaseMap.${project.phase || 'brainstorming'}`) }}
                    </a-tag>
                  </div>
                  <p class="mt-3 text-sm text-muted-foreground">
                    {{ project.description || t('project.noDescription') }}
                  </p>
                </div>

                <div class="grid gap-3 sm:grid-cols-2 xl:min-w-[420px]">
                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.workspacePath') }}</div>
                    <div class="mt-2 break-all text-sm font-medium text-foreground">
                      {{ project.workspacePath || '--' }}
                    </div>
                  </div>
                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.provider') }}</div>
                    <div class="mt-2 text-sm font-medium text-foreground">
                      {{ resolveProviderName(project.defaultProviderId) }}
                    </div>
                    <div class="mt-1 text-xs text-muted-foreground">
                      {{ project.defaultModelName || '--' }}
                    </div>
                  </div>
                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.language') }}</div>
                    <div class="mt-2 text-sm font-medium text-foreground">
                      {{ project.language || '--' }}
                    </div>
                  </div>
                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.updatedAt') }}</div>
                    <div class="mt-2 text-sm font-medium text-foreground">
                      {{ formatDate(project.updatedAt) }}
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <AnalysisOverview :items="projectOverviewItems" />

            <section class="card-box p-5">
              <div class="mb-4 flex items-center justify-between">
                <h3 class="text-base font-semibold">{{ t('project.moduleEntries') }}</h3>
                <span class="text-xs text-muted-foreground">
                  {{ moduleEntryCards.length }}
                </span>
              </div>

              <div
                class="grid gap-3 md:grid-cols-2"
                :class="isBrainstormStage ? 'xl:grid-cols-2' : 'xl:grid-cols-5'"
              >
                <button
                  v-for="module in moduleEntryCards"
                  :key="module.key"
                  type="button"
                  class="rounded-2xl border border-border/70 bg-background/75 p-4 transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-sm"
                  @click="openWorkspaceTab(module.key)"
                >
                  <div class="flex items-center gap-2 text-sm font-medium text-foreground">
                    <VbenIcon :icon="module.icon" class="size-4 text-primary" />
                    {{ module.title }}
                  </div>
                  <div class="mt-2 text-xs text-muted-foreground">
                    {{ module.description }}
                  </div>
                </button>
              </div>
            </section>

            <div class="grid gap-5 xl:grid-cols-[0.95fr_1.05fr]">
              <section class="card-box p-5">
                <div class="mb-4 flex items-center justify-between">
                  <h3 class="text-base font-semibold">{{ t('project.summary') }}</h3>
                </div>

                <div class="space-y-4 text-sm">
                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.extraConfig') }}</div>
                    <pre class="mt-2 whitespace-pre-wrap break-all text-xs text-foreground">{{
                      project.extraConfig || '--'
                    }}</pre>
                  </div>

                  <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
                    <div class="text-xs text-muted-foreground">{{ t('project.assignedAgents') }}</div>
                    <div class="mt-3 flex flex-wrap gap-2">
                      <a-tag
                        v-for="agent in projectAgents"
                        :key="agent.id"
                        :color="agent.status === 'running' ? 'processing' : 'default'"
                      >
                        {{
                          agent.agentRole?.name ||
                          agent.roleName ||
                          agent.id
                        }}
                      </a-tag>
                      <span
                        v-if="projectAgents.length === 0"
                        class="text-xs text-muted-foreground"
                      >
                        {{ t('project.noMembers') }}
                      </span>
                    </div>
                  </div>
                </div>
              </section>

              <section class="card-box p-5">
                <div class="mb-4 flex items-center justify-between">
                  <h3 class="text-base font-semibold">{{ t('project.recentSessions') }}</h3>
                  <span class="text-xs text-muted-foreground">
                    {{ sessions.length }}
                  </span>
                </div>

                <a-empty
                  v-if="sessions.length === 0 && !sessionsQuery.isLoading.value"
                  :description="t('project.noSessions')"
                />

                <div v-else class="space-y-3">
                  <div
                    v-for="session in sessions.slice(0, 6)"
                    :key="session.id"
                    class="rounded-2xl border border-border/70 bg-background/75 px-4 py-3"
                  >
                    <div class="flex items-start justify-between gap-3">
                      <div class="min-w-0">
                        <div class="truncate text-sm font-medium text-foreground">
                          {{ session.scene || session.status || session.id }}
                        </div>
                        <div class="mt-1 truncate text-xs text-muted-foreground">
                          {{ session.input || '--' }}
                        </div>
                      </div>
                      <div class="shrink-0 text-xs text-muted-foreground">
                        {{ formatDate(session.createdAt) }}
                      </div>
                    </div>
                  </div>
                </div>
              </section>
            </div>
          </div>
        </a-tab-pane>

        <a-tab-pane
          v-if="visibleWorkspaceTabs.includes('brainstorm')"
          key="brainstorm"
          :tab="t('project.brainstorm')"
        >
          <ProjectBrainstormTab
            v-if="activeTab === 'brainstorm'"
            :brainstorm-agent="brainstormHeaderAgent"
            :project="project"
            :project-id="projectId"
            @refresh="refreshAll"
          />
        </a-tab-pane>

        <a-tab-pane
          v-if="visibleWorkspaceTabs.includes('chat')"
          key="chat"
          :tab="t('project.chat')"
        >
          <ProjectChatTab
            v-if="activeTab === 'chat'"
            :header-agents="projectChatHeaderAgents"
            :project="project"
            :project-id="projectId"
            :providers="providers"
          />
        </a-tab-pane>

        <a-tab-pane
          v-if="visibleWorkspaceTabs.includes('tasks')"
          key="tasks"
          :tab="t('project.tasks')"
        >
          <ProjectTasksTab
            v-if="activeTab === 'tasks'"
            :project-agents="projectAgents"
            :project-id="projectId"
          />
        </a-tab-pane>

        <a-tab-pane
          v-if="visibleWorkspaceTabs.includes('files')"
          key="files"
          :tab="t('project.files')"
        >
          <ProjectFilesTab v-if="activeTab === 'files'" :project-id="projectId" />
        </a-tab-pane>

        <a-tab-pane
          v-if="visibleWorkspaceTabs.includes('monitor')"
          key="monitor"
          :tab="t('project.monitor')"
        >
          <ProjectMonitorTab v-if="activeTab === 'monitor'" :project-id="projectId" />
        </a-tab-pane>

        <a-tab-pane key="settings" :tab="t('project.settings')">
          <div class="space-y-5">
            <section class="card-box p-5">
              <div class="mb-4 flex items-center justify-between gap-4">
                <div>
                  <h3 class="text-base font-semibold">{{ t('project.basicInfoTitle') }}</h3>
                  <p class="mt-1 text-sm text-muted-foreground">
                    {{ t('project.basicInfoDescription') }}
                  </p>
                </div>
                <a-button
                  type="primary"
                  :loading="saveProjectMutation.isPending.value"
                  @click="saveProject"
                >
                  {{ t('project.saveProject') }}
                </a-button>
              </div>

              <a-form layout="vertical">
                <a-form-item :label="t('project.name')" required>
                  <a-input v-model:value="form.name" />
                </a-form-item>

                <a-form-item :label="t('project.description')">
                  <a-textarea
                    v-model:value="form.description"
                    :auto-size="{ minRows: 3, maxRows: 6 }"
                  />
                </a-form-item>

                <div class="grid gap-4 lg:grid-cols-3">
                  <a-form-item :label="t('project.language')">
                    <a-select
                      v-model:value="form.language"
                      :options="[
                        { label: 'zh-CN', value: 'zh-CN' },
                        { label: 'en-US', value: 'en-US' },
                      ]"
                    />
                  </a-form-item>

                  <a-form-item :label="t('project.provider')">
                    <a-select
                      v-model:value="form.defaultProviderId"
                      allow-clear
                      :options="providerOptions"
                      :placeholder="t('project.providerPlaceholder')"
                    />
                  </a-form-item>

                  <a-form-item :label="t('project.model')">
                    <a-input v-model:value="form.defaultModelName" />
                  </a-form-item>
                </div>

                <a-form-item :label="t('project.extraConfig')">
                  <a-textarea
                    v-model:value="form.extraConfig"
                    :auto-size="{ minRows: 4, maxRows: 8 }"
                    :placeholder="t('project.extraConfigPlaceholder')"
                  />
                </a-form-item>
              </a-form>
            </section>

            <section class="card-box p-5">
              <div class="mb-4 flex items-center justify-between gap-4">
                <div>
                  <h3 class="text-base font-semibold">{{ t('project.membersTitle') }}</h3>
                  <p class="mt-1 text-sm text-muted-foreground">
                    {{ t('project.membersDescription') }}
                  </p>
                </div>
                <a-button
                  type="primary"
                  :loading="saveAgentsMutation.isPending.value"
                  @click="saveMembers"
                >
                  {{ t('project.saveMembers') }}
                </a-button>
              </div>

              <a-select
                v-model:value="selectedAgentRoleIds"
                mode="multiple"
                :options="assignableRoleOptions"
                :placeholder="t('project.rolePlaceholder')"
                style="width: 100%"
              />

              <div class="mt-4 flex flex-wrap gap-2">
                <a-tag
                  v-for="agent in projectAgents"
                  :key="agent.id"
                  :color="agent.status === 'running' ? 'processing' : 'default'"
                >
                  {{
                    agent.agentRole?.name ||
                    agent.roleName ||
                    agent.id
                  }}
                  <span class="ml-1 text-xs opacity-70">{{ agent.status || '--' }}</span>
                </a-tag>
              </div>
            </section>

            <section class="card-box p-5">
              <div class="mb-4 flex items-center justify-between gap-4">
                <div>
                  <h3 class="text-base font-semibold">{{ t('project.mcpTitle') }}</h3>
                  <p class="mt-1 text-sm text-muted-foreground">
                    {{ t('project.mcpDescription') }}
                  </p>
                </div>
                <a-space>
                  <a-select
                    v-model:value="selectedProjectAgentId"
                    :options="
                      projectAgents
                        .filter((agent) => agent.id)
                        .map((agent) => ({
                          label:
                            agent.agentRole?.name ||
                            agent.roleName ||
                            agent.id ||
                            '--',
                          value: agent.id ?? '',
                        }))
                    "
                    style="width: 220px"
                    :placeholder="t('project.projectAgentPlaceholder')"
                  />
                  <a-button
                    type="primary"
                    :disabled="!selectedProjectAgentId"
                    :loading="saveMcpBindingsMutation.isPending.value || mcpBindingsLoading"
                    @click="saveMcpBindings"
                  >
                    {{ t('project.saveBindings') }}
                  </a-button>
                </a-space>
              </div>

              <a-empty
                v-if="projectAgents.length === 0"
                :description="t('project.noMembers')"
              />

              <a-alert
                v-else-if="mcpBindingsError"
                class="mb-4"
                show-icon
                type="error"
                :message="t('project.bindingsLoadFailed')"
                :description="mcpBindingsError"
              />

              <McpBindingAccordionEditor
                v-else
                v-model:active-keys="activeMcpBindingKeys"
                v-model:pending-server-id="pendingMcpServerId"
                :adding="addingMcpBinding"
                :available-servers="availableMcpServers"
                :server-catalog="mcpServers"
                :bindings="mcpBindings"
                :issues="mcpBindingIssues"
                :loading="mcpBindingsLoading"
                :mode-labels="MCP_MODE_LABELS"
                @add="addMcpBinding"
                @remove="removeMcpBinding"
              />
            </section>
          </div>
        </a-tab-pane>
      </a-tabs>
    </template>
  </Page>
</template>

<style scoped>
.project-tabs {
  /* 这里不再手算视口高度，而是直接吃掉 Page 剩余的可用空间。
     这样顶部工具栏、面包屑、宿主布局高度变化时，项目页签会自动跟着剩余空间伸缩。 */
  min-height: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.project-tabs :deep(.ant-tabs-content-holder) {
  min-height: 0;
  flex: 1;
}

.project-tabs :deep(.ant-tabs-content) {
  height: 100%;
}

.project-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
}

.project-tabs :deep(.ant-tabs-tabpane-active) {
  display: flex;
  min-height: 0;
  flex-direction: column;
}

</style>
