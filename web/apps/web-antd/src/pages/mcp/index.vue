<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

import AgentRoleBindingsEditor from './agent-role-bindings.vue';
import type {
  DeleteMcpServerResultDto,
  InstallMcpServerInput,
  McpCatalogEntryDto,
  McpLaunchProfileView,
  McpParameterSchemaItemView,
  McpServerConfigView,
  McpServerView,
  TestMcpConnectionResult,
} from './api';
import {
  checkMcpUninstall,
  createMcpConfig,
  createMcpServer,
  deleteMcpConfig,
  deleteMcpServer,
  getMcpConfigs,
  getMcpServers,
  getMcpSources,
  installMcpServer,
  repairMcpInstall,
  searchMcpCatalog,
  testDraftMcpConfig,
  testSavedMcpConfig,
  updateMcpConfig,
  updateMcpServer,
} from './api';
import ParameterEditor from './parameter-editor.vue';
import { parseMcpQuickCommand } from './quick-command';
import type { McpParameterValues } from './structured-values';
import {
  buildDefaultParameterValues,
  mergeParameterValues,
  parseParameterValues,
  resolveSelectedProfileId,
  stringifyParameterValues,
} from './structured-values';
import ProjectAgentBindingsEditor from './project-agent-bindings.vue';

type ServerEditorMode = 'create' | 'edit';
type ConfigEditorMode = 'create' | 'edit';

const catalogFilters = reactive({
  keyword: '',
  page: 1,
  pageSize: 20,
  sourceKey: '',
  transportType: '',
});

const serverFilters = reactive({
  enabledState: 'all',
  installedState: '',
  search: '',
  source: '',
});

const installDrawerOpen = ref(false);
const activeWorkbenchTab = ref('installed');
const selectedCatalogEntry = ref<null | McpCatalogEntryDto>(null);
const installForm = reactive<InstallMcpServerInput>({
  catalogEntryId: '',
  name: '',
  overwriteExisting: false,
  requestedVersion: '',
  selectedChannelId: '',
  sourceKey: '',
});

const serverEditorOpen = ref(false);
const serverEditorMode = ref<ServerEditorMode>('create');
const activeServer = ref<null | McpServerView>(null);
const serverForm = reactive({
  category: 'general',
  description: '',
  homepage: '',
  icon: '',
  isEnabled: true,
  name: '',
  npmPackage: '',
  pypiPackage: '',
  quickCommand: '',
  templateJson: '',
});
const quickCreateCommand = ref('');

const configEditorOpen = ref(false);
const configEditorMode = ref<ConfigEditorMode>('create');
const activeConfig = ref<null | McpServerConfigView>(null);
const configForm = reactive({
  description: '',
  isEnabled: true,
  mcpServerId: '',
  name: '',
  parameterValues: {} as McpParameterValues,
  selectedProfileId: '',
  transportType: 'stdio',
});

const deleteResultModalOpen = ref(false);
const deleteResult = ref<DeleteMcpServerResultDto | null>(null);
const connectionResultModalOpen = ref(false);
const connectionResultTitle = ref('');
const connectionResult = ref<TestMcpConnectionResult | null>(null);
const loadingSavedConfigId = ref<null | string>(null);

const sourcesQuery = useQuery({
  queryKey: ['mcp', 'sources'],
  queryFn: getMcpSources,
});

const catalogQuery = useQuery({
  queryKey: ['mcp', 'catalog', { ...catalogFilters }],
  queryFn: () => searchMcpCatalog({
    keyword: catalogFilters.keyword || undefined,
    page: catalogFilters.page,
    pageSize: catalogFilters.pageSize,
    sourceKey: catalogFilters.sourceKey || undefined,
    transportType: catalogFilters.transportType || undefined,
  }),
});

const serversQuery = useQuery({
  queryKey: ['mcp', 'servers', { ...serverFilters }],
  queryFn: () => getMcpServers({
    enabledState:
      serverFilters.enabledState === 'all'
        ? undefined
        : serverFilters.enabledState === 'enabled',
    installedState: serverFilters.installedState || undefined,
    search: serverFilters.search || undefined,
    source: serverFilters.source || undefined,
  }),
});

const configsQuery = useQuery({
  queryKey: ['mcp', 'configs'],
  queryFn: getMcpConfigs,
});

watch(
  () => ({ ...catalogFilters }),
  () => {
    void catalogQuery.refetch();
  },
  { deep: true },
);

watch(
  () => ({ ...serverFilters }),
  () => {
    void serversQuery.refetch();
  },
  { deep: true },
);

const installMutation = useMutation({
  mutationFn: (payload: InstallMcpServerInput) => installMcpServer(payload),
  onSuccess: async () => {
    message.success(t('mcp.installSuccess'));
    installDrawerOpen.value = false;
    await refreshAll();
  },
});

const saveServerMutation = useMutation({
  mutationFn: async () => {
    const payload = {
      category: serverForm.category.trim() || 'general',
      templateJson: serverForm.templateJson || activeServer.value?.templateJson || null,
      description: serverForm.description || null,
      homepage: serverForm.homepage || null,
      icon: serverForm.icon || null,
      isEnabled: serverForm.isEnabled,
      mode: activeServer.value?.mode || 'local',
      name: serverForm.name.trim(),
      npmPackage: serverForm.npmPackage || null,
      pypiPackage: serverForm.pypiPackage || null,
      transportType: activeServer.value?.transportType || 'stdio',
    };

    if (serverEditorMode.value === 'create') {
      return createMcpServer(payload);
    }

    if (!activeServer.value?.id) {
      throw new Error(t('mcp.serverIdMissing'));
    }

    return updateMcpServer(activeServer.value.id, payload);
  },
  onSuccess: async () => {
    message.success(
      serverEditorMode.value === 'create'
        ? t('mcp.serverCreateSuccess')
        : t('mcp.serverUpdateSuccess'),
    );
    serverEditorOpen.value = false;
    await refreshAll();
  },
});

const saveConfigMutation = useMutation({
  mutationFn: async () => {
    const payload = {
      description: configForm.description || null,
      isEnabled: configForm.isEnabled,
      mcpServerId: configForm.mcpServerId,
      name: configForm.name.trim(),
      parameterValues: stringifyParameterValues(configForm.parameterValues),
      selectedProfileId: configForm.selectedProfileId || null,
    };

    if (configEditorMode.value === 'create') {
      return createMcpConfig(payload);
    }

    if (!activeConfig.value?.id) {
      throw new Error(t('mcp.configIdMissing'));
    }

    return updateMcpConfig(activeConfig.value.id, payload);
  },
  onSuccess: async () => {
    message.success(
      configEditorMode.value === 'create'
        ? t('mcp.configCreateSuccess')
        : t('mcp.configUpdateSuccess'),
    );
    configEditorOpen.value = false;
    await configsQuery.refetch();
  },
});

const sourceOptions = computed(() => [
  { label: t('mcp.allSources'), value: '' },
  ...(sourcesQuery.data.value ?? []).map((source) => ({
    label: source.displayName || source.sourceKey || '--',
    value: source.sourceKey || '',
  })),
]);

const serverOptions = computed(() =>
  (serversQuery.data.value ?? [])
    .filter((server) => server.id)
    .map((server) => ({
      label: server.name || '--',
      value: server.id || '',
    })),
);

const selectedConfigServer = computed(() =>
  (serversQuery.data.value ?? []).find((server) => server.id === configForm.mcpServerId),
);

watch(
  () => selectedConfigServer.value?.id,
  () => {
    const server = selectedConfigServer.value;
    const selectedProfileId = resolveSelectedProfileId(server, configForm.selectedProfileId);
    configForm.selectedProfileId = selectedProfileId;
    configForm.parameterValues = mergeParameterValues(
      buildDefaultParameterValues(server?.parameterSchema, selectedProfileId),
      configForm.parameterValues,
    );
    configForm.transportType = getSelectedProfileTransportType(server, selectedProfileId) || server?.transportType || 'stdio';
  },
);

watch(
  () => configForm.selectedProfileId,
  (selectedProfileId) => {
    const server = selectedConfigServer.value;
    if (!server) {
      return;
    }

    configForm.parameterValues = mergeParameterValues(
      buildDefaultParameterValues(server.parameterSchema, selectedProfileId),
      configForm.parameterValues,
    );
    configForm.transportType = getSelectedProfileTransportType(server, selectedProfileId) || server.transportType || 'stdio';
  },
);

const installedServers = computed(() =>
  (serversQuery.data.value ?? []).filter(
    (server) => (server.source || '').toLowerCase() !== 'custom',
  ),
);

const customServers = computed(() =>
  (serversQuery.data.value ?? []).filter(
    (server) => (server.source || '').toLowerCase() === 'custom',
  ),
);

const overviewItems = computed<AnalysisOverviewItem[]>(() => {
  const sources = sourcesQuery.data.value ?? [];
  const servers = serversQuery.data.value ?? [];
  const configs = configsQuery.data.value ?? [];
  const managedServers = servers.filter((server) => server.isManagedInstall).length;

  return [
    {
      title: t('mcp.catalogEntries'),
      icon: 'lucide:store',
      value: catalogQuery.data.value?.totalCount ?? 0,
      totalTitle: t('mcp.catalogSources'),
      totalValue: sources.length,
    },
    {
      title: t('mcp.serverRegistry'),
      icon: 'lucide:plug-zap',
      value: servers.length,
      totalTitle: t('mcp.managedInstalls'),
      totalValue: managedServers,
    },
    {
      title: t('mcp.configsTitle'),
      icon: 'lucide:settings-2',
      value: configs.length,
      totalTitle: t('mcp.enabledState'),
      totalValue: configs.filter((config) => config.isEnabled).length,
    },
    {
      title: t('mcp.boundScopes'),
      icon: 'lucide:link',
      value: 2,
      totalTitle: t('mcp.supportedScopes'),
      totalValue: 2,
    },
  ];
});

function getDefaultProfile(server: McpServerView) {
  return (server.profiles ?? []).find((profile) => profile.id === server.defaultProfileId)
    ?? server.profiles?.[0];
}

function getSelectedProfileTransportType(server?: null | McpServerView, profileId?: null | string) {
  if (!server) {
    return undefined;
  }

  return (server.profiles ?? []).find((profile) => profile.id === profileId)?.transportType;
}

function summarizeProfile(profile?: McpLaunchProfileView | null) {
  if (!profile) {
    return '--';
  }

  const parts = [profile.profileType || '--'];
  if (profile.runner) {
    parts.push(profile.runner);
  }

  if (profile.packageName) {
    parts.push(profile.packageName);
  } else if (profile.image) {
    parts.push(profile.image);
  } else if (profile.urlTemplate) {
    parts.push(profile.urlTemplate);
  }

  return parts.join(' · ');
}

function getVisibleParameterSchema(server?: McpServerView | null) {
  return (server?.parameterSchema ?? []).slice(0, 4);
}

function summarizeParameter(item: McpParameterSchemaItemView) {
  const name = item.label || item.key || '--';
  const type = item.type || 'string';
  return `${name} · ${type}`;
}

function describeParameterDefault(item: McpParameterSchemaItemView) {
  if (item.projectOverrideValueSource === 'project-workspace') {
    return t('mcp.parameterDefaultProjectWorkspace');
  }

  switch (item.defaultValueSource) {
    case 'host-temp-directory': {
      return t('mcp.parameterDefaultHostTemp');
    }
    case 'user-input': {
      return t('mcp.parameterDefaultUserInput');
    }
    case 'template-default': {
      if (item.defaultValue === null || item.defaultValue === undefined || item.defaultValue === '') {
        return t('mcp.parameterDefaultNone');
      }

      return `${t('mcp.parameterDefaultTemplate')}: ${String(item.defaultValue)}`;
    }
    default: {
      if (item.defaultValue === null || item.defaultValue === undefined || item.defaultValue === '') {
        return t('mcp.parameterDefaultNone');
      }

      return String(item.defaultValue);
    }
  }
}

async function refreshAll() {
  await Promise.all([
    sourcesQuery.refetch(),
    catalogQuery.refetch(),
    serversQuery.refetch(),
    configsQuery.refetch(),
  ]);
}

function openInstallDrawer(entry: McpCatalogEntryDto) {
  selectedCatalogEntry.value = entry;
  installForm.catalogEntryId = entry.entryId || '';
  installForm.sourceKey = entry.sourceKey || '';
  installForm.name = entry.displayName || entry.name || '';
  installForm.requestedVersion = entry.version || '';
  installForm.selectedChannelId = entry.installChannels?.[0]?.channelId || '';
  installForm.overwriteExisting = false;
  installDrawerOpen.value = true;
}

async function submitInstall() {
  if (!installForm.sourceKey || !installForm.catalogEntryId) {
    message.warning(t('mcp.installSelectionRequired'));
    return;
  }

  try {
    await installMutation.mutateAsync({
      catalogEntryId: installForm.catalogEntryId,
      name: installForm.name || undefined,
      overwriteExisting: installForm.overwriteExisting,
      requestedVersion: installForm.requestedVersion || undefined,
      selectedChannelId: installForm.selectedChannelId || undefined,
      sourceKey: installForm.sourceKey,
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.installFailed')));
  }
}

function openServerCreate() {
  serverEditorMode.value = 'create';
  activeServer.value = null;
  Object.assign(serverForm, {
    category: 'general',
    description: '',
    homepage: '',
    icon: '',
    isEnabled: true,
    name: '',
    npmPackage: '',
    pypiPackage: '',
    quickCommand: '',
    templateJson: '',
  });
  serverEditorOpen.value = true;
}

function openServerEdit(server: McpServerView) {
  serverEditorMode.value = 'edit';
  activeServer.value = server;
  Object.assign(serverForm, {
    category: server.category || 'general',
    description: server.description || '',
    homepage: server.homepage || '',
    icon: server.icon || '',
    isEnabled: server.isEnabled ?? true,
    name: server.name || '',
    npmPackage: server.npmPackage || '',
    pypiPackage: server.pypiPackage || '',
    quickCommand: '',
    templateJson: server.templateJson || '',
  });
  serverEditorOpen.value = true;
}

function openQuickCommandCreate() {
  openServerCreate();
  serverForm.quickCommand = quickCreateCommand.value.trim();
  if (serverForm.quickCommand) {
    applyQuickCommandToServerForm(false);
  }
}

function applyQuickCommandToServerForm(notify = true) {
  if (!serverForm.quickCommand.trim()) {
    if (notify) {
      message.warning(t('mcp.quickCommandRequired'));
    }
    return false;
  }

  try {
    const parsed = parseMcpQuickCommand(serverForm.quickCommand);
    serverForm.templateJson = parsed.templateJson;
    serverForm.npmPackage = parsed.npmPackage || '';
    serverForm.pypiPackage = parsed.pypiPackage || '';
    if (!serverForm.name.trim()) {
      serverForm.name = parsed.suggestedName;
    }

    if (notify) {
      message.success(t('mcp.quickCommandApplied'));
    }
    return true;
  } catch (error) {
    message.error(getQuickCommandErrorMessage(error));
    return false;
  }
}

async function submitServer() {
  if (serverForm.quickCommand.trim() && !serverForm.templateJson.trim()) {
    const applied = applyQuickCommandToServerForm(false);
    if (!applied) {
      return;
    }
  }

  if (!serverForm.name.trim()) {
    message.warning(t('mcp.serverNameRequired'));
    return;
  }

  if (!serverForm.templateJson.trim() && !activeServer.value?.templateJson) {
    message.warning(t('mcp.quickCommandRequired'));
    return;
  }

  try {
    await saveServerMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.serverSaveFailed')));
  }
}

async function toggleServerEnabled(server: McpServerView, isEnabled: boolean) {
  if (!server.id) {
    return;
  }

  try {
    await updateMcpServer(server.id, { isEnabled });
    await serversQuery.refetch();
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.serverSaveFailed')));
  }
}

async function inspectServer(server: McpServerView) {
  if (!server.id) {
    return;
  }

  try {
    deleteResult.value = {
      action: 'inspect',
      ...(await checkMcpUninstall(server.id)),
      message: t('mcp.uninstallInspection'),
      serverId: server.id,
    };
    deleteResultModalOpen.value = true;
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.inspectFailed')));
  }
}

async function deleteServerAction(server: McpServerView) {
  if (!server.id) {
    return;
  }

  try {
    const result = await deleteMcpServer(server.id);
    if (result.deleted) {
      message.success(result.message || t('mcp.serverDeleteSuccess'));
      await refreshAll();
      return;
    }

    deleteResult.value = result;
    deleteResultModalOpen.value = true;
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.serverDeleteFailed')));
  }
}

async function repairServer(server: McpServerView) {
  if (!server.id) {
    return;
  }

  try {
    const result = await repairMcpInstall(server.id);
    message.success(result.message || t('mcp.repairSuccess'));
    await refreshAll();
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.repairFailed')));
  }
}

function openConfigCreate() {
  configEditorMode.value = 'create';
  activeConfig.value = null;
  const defaultServerId = serverOptions.value[0]?.value || '';
  const defaultServer = (serversQuery.data.value ?? []).find((server) => server.id === defaultServerId);
  const selectedProfileId = resolveSelectedProfileId(defaultServer);
  Object.assign(configForm, {
    description: '',
    isEnabled: true,
    mcpServerId: defaultServerId,
    name: '',
    parameterValues: buildDefaultParameterValues(defaultServer?.parameterSchema, selectedProfileId),
    selectedProfileId,
    transportType: getSelectedProfileTransportType(defaultServer, selectedProfileId) || defaultServer?.transportType || 'stdio',
  });
  configEditorOpen.value = true;
}

function openConfigEdit(config: McpServerConfigView) {
  configEditorMode.value = 'edit';
  activeConfig.value = config;
  const server = (serversQuery.data.value ?? []).find((item) => item.id === config.mcpServerId);
  const selectedProfileId = resolveSelectedProfileId(server, config.selectedProfileId);
  Object.assign(configForm, {
    description: config.description || '',
    isEnabled: config.isEnabled ?? true,
    mcpServerId: config.mcpServerId || '',
    name: config.name || '',
    parameterValues: mergeParameterValues(
      buildDefaultParameterValues(server?.parameterSchema, selectedProfileId),
      parseParameterValues(config.parameterValues),
    ),
    selectedProfileId,
    transportType: getSelectedProfileTransportType(server, selectedProfileId) || config.transportType || 'stdio',
  });
  configEditorOpen.value = true;
}

async function submitConfig() {
  if (!configForm.name.trim()) {
    message.warning(t('mcp.configNameRequired'));
    return;
  }

  if (!configForm.mcpServerId) {
    message.warning(t('mcp.configServerRequired'));
    return;
  }

  try {
    await saveConfigMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.configSaveFailed')));
  }
}

async function runDraftConfigTest() {
  try {
    connectionResult.value = await testDraftMcpConfig({
      mcpServerId: configForm.mcpServerId,
      parameterValues: stringifyParameterValues(configForm.parameterValues) || undefined,
      selectedProfileId: configForm.selectedProfileId || undefined,
    });
    connectionResultTitle.value = t('mcp.draftTestTitle');
    connectionResultModalOpen.value = true;
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.testFailed')));
  }
}

async function fetchSavedConfigTools(config: McpServerConfigView) {
  if (!config.id) {
    return;
  }

  loadingSavedConfigId.value = config.id;
  try {
    connectionResult.value = await testSavedMcpConfig(config.id);
    connectionResultTitle.value = `${t('mcp.fetchToolsTitle')} · ${config.name || '--'}`;
    connectionResultModalOpen.value = true;
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.fetchToolsFailed')));
  } finally {
    if (loadingSavedConfigId.value === config.id) {
      loadingSavedConfigId.value = null;
    }
  }
}

async function removeConfig(config: McpServerConfigView) {
  if (!config.id) {
    return;
  }

  try {
    await deleteMcpConfig(config.id);
    message.success(t('mcp.configDeleteSuccess'));
    await configsQuery.refetch();
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.configDeleteFailed')));
  }
}

function getErrorMessage(errorValue: unknown, fallback: string) {
  if (errorValue instanceof Error && errorValue.message) {
    return errorValue.message;
  }

  return fallback;
}

function getQuickCommandErrorMessage(errorValue: unknown) {
  if (errorValue instanceof Error && errorValue.message === 'EMPTY_COMMAND') {
    return t('mcp.quickCommandRequired');
  }

  return getErrorMessage(errorValue, t('mcp.quickCommandParseFailed'));
}

const transportOptions = [
  { label: 'stdio', value: 'stdio' },
  { label: 'http', value: 'http' },
  { label: 'sse', value: 'sse' },
  { label: 'streamable-http', value: 'streamable-http' },
];

const installedServerSourceOptions = computed(() => [
  { label: t('mcp.allSources'), value: '' },
  ...sourceOptions.value.slice(1),
  { label: 'builtin', value: 'builtin' },
  { label: 'marketplace', value: 'marketplace' },
]);
</script>

<template>
  <Page :title="t('mcp.title')" content-class="space-y-5">
    <template #extra>
      <a-space>
        <a-button
          :loading="sourcesQuery.isFetching.value || catalogQuery.isFetching.value || serversQuery.isFetching.value || configsQuery.isFetching.value"
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button v-if="activeWorkbenchTab === 'custom'" type="primary" @click="openServerCreate">
          {{ t('mcp.createCustomServer') }}
        </a-button>
        <a-button v-if="activeWorkbenchTab === 'installed'" @click="openConfigCreate">
          {{ t('mcp.createConfig') }}
        </a-button>
      </a-space>
    </template>

    <AnalysisOverview :items="overviewItems" />

    <a-tabs v-model:activeKey="activeWorkbenchTab">
      <a-tab-pane :tab="t('mcp.installedTab')" key="installed">
        <div class="space-y-5">
          <section class="card-box p-4">
            <div class="mb-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h3 class="text-base font-semibold">{{ t('mcp.serverRegistry') }}</h3>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ t('mcp.serverRegistryDescription') }}
                </p>
              </div>
              <a-space wrap>
                <a-select
                  v-model:value="serverFilters.source"
                  :options="installedServerSourceOptions"
                  style="width: 180px"
                />
                <a-select
                  v-model:value="serverFilters.installedState"
                  :options="[
                    { label: t('common.all'), value: '' },
                    { label: t('mcp.managedInstalls'), value: 'managed' },
                    { label: t('mcp.notInstalledTag'), value: 'not-installed' },
                    { label: 'Ready', value: 'Ready' },
                    { label: 'Failed', value: 'Failed' },
                  ]"
                  style="width: 180px"
                />
                <a-select
                  v-model:value="serverFilters.enabledState"
                  :options="[
                    { label: t('common.all'), value: 'all' },
                    { label: t('mcp.enabledState'), value: 'enabled' },
                    { label: t('mcp.disabledState'), value: 'disabled' },
                  ]"
                  style="width: 180px"
                />
                <a-input
                  v-model:value="serverFilters.search"
                  allow-clear
                  :placeholder="t('mcp.serverSearchPlaceholder')"
                  style="width: 260px"
                />
              </a-space>
            </div>

            <a-empty
              v-if="!serversQuery.isLoading.value && installedServers.length === 0"
              :description="t('mcp.installedServersEmpty')"
            />

            <div class="grid gap-4 xl:grid-cols-2">
              <div
                v-for="server in installedServers"
                :key="server.id"
                class="rounded-2xl border border-border/70 bg-background/75 p-4"
              >
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <div class="text-sm font-semibold">
                      {{ server.name || '--' }}
                    </div>
                    <div class="mt-1 text-xs text-muted-foreground">
                      {{ server.source || '--' }} · {{ server.transportType || '--' }} · {{ server.mode || '--' }}
                    </div>
                  </div>
                  <a-space size="small">
                    <a-tag :color="server.isManagedInstall ? 'success' : 'default'">
                      {{ server.isManagedInstall ? t('mcp.managedTag') : t('mcp.definitionTag') }}
                    </a-tag>
                    <a-switch
                      :checked="server.isEnabled"
                      @update:checked="(checked: boolean) => toggleServerEnabled(server, checked)"
                    />
                  </a-space>
                </div>

                <p class="mt-3 text-sm text-muted-foreground">
                  {{ server.description || t('mcp.noDescription') }}
                </p>

                <div class="mt-3 grid gap-2 text-xs text-muted-foreground md:grid-cols-2">
                  <div>{{ t('mcp.configCount') }}: {{ server.configCount ?? 0 }}</div>
                  <div>{{ t('mcp.installState') }}: {{ server.installedState || '--' }}</div>
                  <div>{{ t('mcp.installVersion') }}: {{ server.installedVersion || '--' }}</div>
                  <div>{{ t('mcp.channelType') }}: {{ server.installChannelType || '--' }}</div>
                </div>

                <div class="mt-3 space-y-2 text-xs text-muted-foreground">
                  <div>
                    {{ t('mcp.defaultProfile') }}:
                    {{ summarizeProfile(getDefaultProfile(server)) }}
                  </div>
                  <div v-if="(server.parameterSchema?.length ?? 0) > 0">
                    <div class="mb-2">{{ t('mcp.parameterSchemaTitle') }}</div>
                    <div class="flex flex-wrap gap-2">
                      <a-tooltip
                        v-for="item in getVisibleParameterSchema(server)"
                        :key="`${server.id}-${item.key}`"
                        :title="describeParameterDefault(item)"
                      >
                        <a-tag color="blue">
                          {{ summarizeParameter(item) }}
                        </a-tag>
                      </a-tooltip>
                    </div>
                  </div>
                </div>

                <div
                  v-if="server.lastInstallError"
                  class="mt-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-600"
                >
                  {{ server.lastInstallError }}
                </div>

                <div class="mt-4 flex flex-wrap justify-end gap-2">
                  <a-button @click="openServerEdit(server)">
                    {{ t('mcp.edit') }}
                  </a-button>
                  <a-button @click="inspectServer(server)">
                    {{ t('mcp.inspectDelete') }}
                  </a-button>
                  <a-button
                    :disabled="!server.isManagedInstall"
                    @click="repairServer(server)"
                  >
                    {{ t('mcp.repair') }}
                  </a-button>
                  <a-button danger @click="deleteServerAction(server)">
                    {{ t('mcp.delete') }}
                  </a-button>
                </div>
              </div>
            </div>
          </section>

          <section class="card-box p-4">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h3 class="text-base font-semibold">{{ t('mcp.configsTitle') }}</h3>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ t('mcp.configsDescription') }}
                </p>
              </div>
              <a-button @click="openConfigCreate">
                {{ t('mcp.createConfig') }}
              </a-button>
            </div>

            <a-empty
              v-if="!configsQuery.isLoading.value && (configsQuery.data.value?.length ?? 0) === 0"
              :description="t('mcp.configsEmpty')"
            />

            <div class="grid gap-4 xl:grid-cols-2">
              <div
                v-for="config in configsQuery.data.value ?? []"
                :key="config.id"
                class="rounded-2xl border border-border/70 bg-background/75 p-4"
              >
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <div class="text-sm font-semibold">
                      {{ config.name || '--' }}
                    </div>
                    <div class="mt-1 text-xs text-muted-foreground">
                      {{ config.mcpServerName || '--' }} · {{ config.transportType || '--' }}
                    </div>
                  </div>
                  <a-tag :color="config.isEnabled ? 'success' : 'default'">
                    {{ config.isEnabled ? t('mcp.enabledState') : t('mcp.disabledState') }}
                  </a-tag>
                </div>

                <p class="mt-3 text-sm text-muted-foreground">
                  {{ config.description || t('mcp.noDescription') }}
                </p>

                <div class="mt-3 flex flex-wrap gap-2 text-xs text-muted-foreground">
                  <a-tag v-if="config.hasEnvironmentVariables" color="blue">
                    ENV
                  </a-tag>
                  <a-tag v-if="config.hasAuthConfig" color="purple">
                    Auth
                  </a-tag>
                </div>

                <div class="mt-4 flex flex-wrap justify-end gap-2">
                  <a-button
                    :aria-label="t('mcp.fetchTools')"
                    :auto-insert-space="false"
                    :loading="loadingSavedConfigId === config.id"
                    @click="fetchSavedConfigTools(config)"
                  >
                    {{ t('mcp.fetchTools') }}
                  </a-button>
                  <a-button @click="openConfigEdit(config)">
                    {{ t('mcp.edit') }}
                  </a-button>
                  <a-button danger @click="removeConfig(config)">
                    {{ t('mcp.delete') }}
                  </a-button>
                </div>
              </div>
            </div>
          </section>

          <AgentRoleBindingsEditor :servers="serversQuery.data.value ?? []" />
          <ProjectAgentBindingsEditor :servers="serversQuery.data.value ?? []" />
        </div>
      </a-tab-pane>

      <a-tab-pane :tab="t('mcp.marketTab')" key="market">
        <section class="card-box p-4">
          <div class="mb-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 class="text-base font-semibold">{{ t('mcp.catalogTitle') }}</h3>
              <p class="mt-1 text-xs text-muted-foreground">
                {{ t('mcp.catalogDescription') }}
              </p>
            </div>
            <a-space wrap>
              <a-select
                v-model:value="catalogFilters.sourceKey"
                :options="sourceOptions"
                style="width: 200px"
              />
              <a-select
                v-model:value="catalogFilters.transportType"
                :options="[{ label: t('common.all'), value: '' }, ...transportOptions]"
                style="width: 180px"
              />
              <a-input
                v-model:value="catalogFilters.keyword"
                allow-clear
                :placeholder="t('mcp.catalogSearchPlaceholder')"
                style="width: 260px"
              />
            </a-space>
          </div>

          <a-empty
            v-if="!catalogQuery.isLoading.value && (catalogQuery.data.value?.items?.length ?? 0) === 0"
            :description="t('mcp.catalogEmpty')"
          />

          <div class="grid gap-4 xl:grid-cols-2">
            <div
              v-for="entry in catalogQuery.data.value?.items ?? []"
              :key="`${entry.sourceKey}-${entry.entryId}`"
              class="rounded-2xl border border-border/70 bg-background/75 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <div class="text-sm font-semibold">
                    {{ entry.displayName || entry.name || '--' }}
                  </div>
                  <div class="mt-1 text-xs text-muted-foreground">
                    {{ entry.sourceKey || '--' }} · {{ entry.category || '--' }}
                  </div>
                </div>
                <a-tag :color="entry.isInstalled ? 'success' : 'default'">
                  {{ entry.isInstalled ? t('mcp.installedTag') : t('mcp.notInstalledTag') }}
                </a-tag>
              </div>

              <p class="mt-3 text-sm text-muted-foreground">
                {{ entry.description || t('mcp.noDescription') }}
              </p>

              <div class="mt-3 flex flex-wrap gap-2">
                <a-tag v-for="transport in entry.transportTypes ?? []" :key="transport">
                  {{ transport }}
                </a-tag>
                <a-tag
                  v-for="channel in entry.installChannels ?? []"
                  :key="channel.channelId"
                  color="blue"
                >
                  {{ channel.channelType }}{{ channel.packageIdentifier ? ` · ${channel.packageIdentifier}` : '' }}
                </a-tag>
              </div>

              <div class="mt-4 flex justify-end">
                <a-button
                  type="primary"
                  :disabled="!(entry.installChannels?.length)"
                  @click="openInstallDrawer(entry)"
                >
                  {{ t('mcp.install') }}
                </a-button>
              </div>
            </div>
          </div>
        </section>
      </a-tab-pane>

      <a-tab-pane :tab="t('mcp.customTab')" key="custom">
        <div class="space-y-5">
          <section class="card-box p-4">
            <div class="mb-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h3 class="text-base font-semibold">{{ t('mcp.quickCommandTitle') }}</h3>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ t('mcp.quickCommandDescription') }}
                </p>
              </div>
              <a-space>
                <a-button type="primary" @click="openQuickCommandCreate">
                  {{ t('mcp.quickCreateButton') }}
                </a-button>
                <a-button @click="openServerCreate">
                  {{ t('mcp.createBlankCustomServer') }}
                </a-button>
              </a-space>
            </div>

            <a-input
              v-model:value="quickCreateCommand"
              allow-clear
              :placeholder="t('mcp.quickCommandPlaceholder')"
              @press-enter="openQuickCommandCreate"
            />

            <div class="mt-3 flex flex-wrap gap-2">
              <a-tag
                class="cursor-pointer"
                color="blue"
                @click="quickCreateCommand = 'npx @playwright/mcp@latest'"
              >
                npx @playwright/mcp@latest
              </a-tag>
              <a-tag
                class="cursor-pointer"
                color="purple"
                @click="quickCreateCommand = 'uvx mcp-server-fetch'"
              >
                uvx mcp-server-fetch
              </a-tag>
            </div>
          </section>

          <section class="card-box p-4">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h3 class="text-base font-semibold">{{ t('mcp.customServersTitle') }}</h3>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ t('mcp.customServersDescription') }}
                </p>
              </div>
              <a-button type="primary" @click="openServerCreate">
                {{ t('mcp.createCustomServer') }}
              </a-button>
            </div>

            <a-empty
              v-if="!serversQuery.isLoading.value && customServers.length === 0"
              :description="t('mcp.customServersEmpty')"
            />

            <div class="grid gap-4 xl:grid-cols-2">
              <div
                v-for="server in customServers"
                :key="server.id"
                class="rounded-2xl border border-border/70 bg-background/75 p-4"
              >
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <div class="text-sm font-semibold">
                      {{ server.name || '--' }}
                    </div>
                    <div class="mt-1 text-xs text-muted-foreground">
                      {{ server.transportType || '--' }} · {{ server.mode || '--' }}
                    </div>
                  </div>
                  <a-switch
                    :checked="server.isEnabled"
                    @update:checked="(checked: boolean) => toggleServerEnabled(server, checked)"
                  />
                </div>

                <p class="mt-3 text-sm text-muted-foreground">
                  {{ server.description || t('mcp.noDescription') }}
                </p>

                <div class="mt-3 flex flex-wrap gap-2 text-xs text-muted-foreground">
                  <a-tag v-if="server.npmPackage" color="blue">
                    npm · {{ server.npmPackage }}
                  </a-tag>
                  <a-tag v-if="server.pypiPackage" color="purple">
                    pypi · {{ server.pypiPackage }}
                  </a-tag>
                  <a-tag>
                    {{ t('mcp.configCount') }}: {{ server.configCount ?? 0 }}
                  </a-tag>
                </div>

                <div class="mt-3 space-y-2 text-xs text-muted-foreground">
                  <div>
                    {{ t('mcp.defaultProfile') }}:
                    {{ summarizeProfile(getDefaultProfile(server)) }}
                  </div>
                  <div v-if="(server.parameterSchema?.length ?? 0) > 0">
                    <div class="mb-2">{{ t('mcp.parameterSchemaTitle') }}</div>
                    <div class="flex flex-wrap gap-2">
                      <a-tooltip
                        v-for="item in getVisibleParameterSchema(server)"
                        :key="`${server.id}-${item.key}`"
                        :title="describeParameterDefault(item)"
                      >
                        <a-tag color="blue">
                          {{ summarizeParameter(item) }}
                        </a-tag>
                      </a-tooltip>
                    </div>
                  </div>
                </div>

                <div class="mt-4 flex flex-wrap justify-end gap-2">
                  <a-button @click="openServerEdit(server)">
                    {{ t('mcp.edit') }}
                  </a-button>
                  <a-button @click="inspectServer(server)">
                    {{ t('mcp.inspectDelete') }}
                  </a-button>
                  <a-button danger @click="deleteServerAction(server)">
                    {{ t('mcp.delete') }}
                  </a-button>
                </div>
              </div>
            </div>
          </section>
        </div>
      </a-tab-pane>
    </a-tabs>

    <a-drawer
      :open="installDrawerOpen"
      :title="t('mcp.installDrawerTitle')"
      :width="560"
      destroy-on-close
      @close="installDrawerOpen = false"
    >
      <div class="space-y-4">
        <div>
          <div class="text-sm font-semibold">
            {{ selectedCatalogEntry?.displayName || selectedCatalogEntry?.name || '--' }}
          </div>
          <div class="mt-1 text-xs text-muted-foreground">
            {{ selectedCatalogEntry?.sourceKey || '--' }}
          </div>
        </div>

        <a-form layout="vertical">
          <a-form-item :label="t('mcp.installName')">
            <a-input v-model:value="installForm.name" />
          </a-form-item>
          <a-form-item :label="t('mcp.installChannel')">
            <a-select
              v-model:value="installForm.selectedChannelId"
              :options="(selectedCatalogEntry?.installChannels ?? []).map((channel) => ({
                label: `${channel.channelType || '--'}${channel.packageIdentifier ? ` · ${channel.packageIdentifier}` : ''}`,
                value: channel.channelId || '',
              }))"
            />
          </a-form-item>
          <a-form-item :label="t('mcp.requestedVersionLabel')">
            <a-input v-model:value="installForm.requestedVersion" />
          </a-form-item>
          <div class="flex items-center justify-between rounded-xl border border-border/60 px-3 py-2">
            <span class="text-sm text-muted-foreground">{{ t('mcp.overwriteExisting') }}</span>
            <a-switch v-model:checked="installForm.overwriteExisting" />
          </div>
        </a-form>
      </div>

      <template #extra>
        <a-space>
          <a-button @click="installDrawerOpen = false">
            {{ t('common.cancel') }}
          </a-button>
          <a-button
            type="primary"
            :loading="installMutation.isPending.value"
            @click="submitInstall"
          >
            {{ t('mcp.install') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-drawer
      :open="serverEditorOpen"
      :title="serverEditorMode === 'create' ? t('mcp.serverCreateTitle') : t('mcp.serverEditTitle')"
      :width="720"
      destroy-on-close
      @close="serverEditorOpen = false"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('mcp.quickCommandLabel')">
          <a-space-compact style="width: 100%">
            <a-input
              v-model:value="serverForm.quickCommand"
              :placeholder="t('mcp.quickCommandPlaceholder')"
            />
            <a-button @click="applyQuickCommandToServerForm()">
              {{ t('mcp.applyQuickCommand') }}
            </a-button>
          </a-space-compact>
          <div class="mt-2 text-xs text-muted-foreground">
            {{ t('mcp.quickCommandHelp') }}
          </div>
        </a-form-item>
        <div class="grid gap-3 lg:grid-cols-2">
          <a-form-item :label="t('mcp.serverName')">
            <a-input v-model:value="serverForm.name" />
          </a-form-item>
          <a-form-item :label="t('mcp.serverCategory')">
            <a-input v-model:value="serverForm.category" />
          </a-form-item>
        </div>
        <a-form-item :label="t('mcp.serverHomepage')">
          <a-input v-model:value="serverForm.homepage" />
        </a-form-item>
        <a-form-item :label="t('mcp.serverDescription')">
          <a-textarea v-model:value="serverForm.description" :auto-size="{ minRows: 3, maxRows: 6 }" />
        </a-form-item>
        <div class="grid gap-3 lg:grid-cols-2">
          <a-form-item :label="t('mcp.serverNpmPackage')">
            <a-input v-model:value="serverForm.npmPackage" />
          </a-form-item>
          <a-form-item :label="t('mcp.serverPypiPackage')">
            <a-input v-model:value="serverForm.pypiPackage" />
          </a-form-item>
        </div>
        <div
          v-if="activeServer && (activeServer.profiles?.length || activeServer.parameterSchema?.length)"
          class="rounded-xl border border-border/60 bg-muted/30 px-3 py-3 text-xs text-muted-foreground"
        >
          <div v-if="activeServer.profiles?.length">
            <span class="font-medium text-foreground">{{ t('mcp.defaultProfile') }}</span>
            <span> · {{ summarizeProfile(getDefaultProfile(activeServer)) }}</span>
          </div>
          <div v-if="(activeServer.parameterSchema?.length ?? 0) > 0" class="mt-2 space-y-1">
            <div>{{ t('mcp.parameterSchemaTitle') }}</div>
            <div v-for="item in activeServer.parameterSchema ?? []" :key="`${activeServer.id}-${item.key}`">
              <span class="font-medium text-foreground">{{ summarizeParameter(item) }}</span>
              <span> · {{ describeParameterDefault(item) }}</span>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-between rounded-xl border border-border/60 px-3 py-2">
          <span class="text-sm text-muted-foreground">{{ t('mcp.enabledState') }}</span>
          <a-switch v-model:checked="serverForm.isEnabled" />
        </div>
      </a-form>

      <template #extra>
        <a-space>
          <a-button @click="serverEditorOpen = false">
            {{ t('common.cancel') }}
          </a-button>
          <a-button
            type="primary"
            :loading="saveServerMutation.isPending.value"
            @click="submitServer"
          >
            {{ t('common.save') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-drawer
      :open="configEditorOpen"
      :title="configEditorMode === 'create' ? t('mcp.configCreateTitle') : t('mcp.configEditTitle')"
      :width="720"
      destroy-on-close
      @close="configEditorOpen = false"
    >
        <a-form layout="vertical">
          <a-form-item :label="t('mcp.serverName')">
            <a-select v-model:value="configForm.mcpServerId" :options="serverOptions" />
          </a-form-item>
          <div
            v-if="selectedConfigServer"
            class="mb-4 rounded-xl border border-border/60 bg-muted/30 px-3 py-3 text-xs text-muted-foreground"
          >
            <div class="font-medium text-foreground">
              {{ t('mcp.defaultProfile') }}: {{ summarizeProfile(getDefaultProfile(selectedConfigServer)) }}
            </div>
          </div>
          <div class="grid gap-3 lg:grid-cols-2">
            <a-form-item :label="t('mcp.configName')">
              <a-input v-model:value="configForm.name" />
            </a-form-item>
            <a-form-item :label="t('mcp.defaultProfile')">
              <a-select
                v-model:value="configForm.selectedProfileId"
                :options="(selectedConfigServer?.profiles ?? []).map((profile) => ({
                  label: profile.displayName || profile.id || '--',
                  value: profile.id || '',
                }))"
              />
            </a-form-item>
          </div>
        <a-form-item :label="t('mcp.serverDescription')">
          <a-input v-model:value="configForm.description" />
        </a-form-item>
        <ParameterEditor
          v-model="configForm.parameterValues"
          :schema="selectedConfigServer?.parameterSchema ?? []"
          :selected-profile-id="configForm.selectedProfileId"
        />
        <div class="flex items-center justify-between rounded-xl border border-border/60 px-3 py-2">
          <span class="text-sm text-muted-foreground">{{ t('mcp.enabledState') }}</span>
          <a-switch v-model:checked="configForm.isEnabled" />
        </div>
      </a-form>

      <template #extra>
        <a-space>
          <a-button @click="runDraftConfigTest">
            {{ t('mcp.testDraft') }}
          </a-button>
          <a-button @click="configEditorOpen = false">
            {{ t('common.cancel') }}
          </a-button>
          <a-button
            type="primary"
            :loading="saveConfigMutation.isPending.value"
            @click="submitConfig"
          >
            {{ t('common.save') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-modal
      v-model:open="deleteResultModalOpen"
      :footer="null"
      :title="t('mcp.uninstallResultTitle')"
      width="640px"
    >
      <div class="space-y-4 text-sm">
        <a-alert
          :message="deleteResult?.message || '--'"
          :type="deleteResult?.deleted ? 'success' : 'warning'"
          show-icon
        />
        <div
          v-if="(deleteResult?.blockingReasons?.length ?? 0) > 0"
          class="space-y-2"
        >
          <div class="font-medium">{{ t('mcp.blockingReasons') }}</div>
          <ul class="list-disc pl-5 text-muted-foreground">
            <li v-for="item in deleteResult?.blockingReasons ?? []" :key="item">
              {{ item }}
            </li>
          </ul>
        </div>
      </div>
    </a-modal>

    <a-modal
      v-model:open="connectionResultModalOpen"
      :footer="null"
      :title="connectionResultTitle"
      width="720px"
    >
      <div class="space-y-4">
        <a-alert
          :message="connectionResult?.message || '--'"
          :type="connectionResult?.success ? 'success' : 'error'"
          show-icon
        />
        <div
          v-if="(connectionResult?.tools?.length ?? 0) > 0"
          class="space-y-3"
        >
          <div
            v-for="tool in connectionResult?.tools ?? []"
            :key="tool.name"
            class="rounded-2xl border border-border/70 bg-background/75 p-4"
          >
            <div class="font-medium">{{ tool.name || '--' }}</div>
            <div class="mt-1 text-xs text-muted-foreground">
              {{ tool.description || t('mcp.noDescription') }}
            </div>
            <pre class="mt-3 overflow-auto rounded-xl bg-muted/60 p-3 text-xs">{{ tool.inputSchema || '{}' }}</pre>
          </div>
        </div>
        <a-empty
          v-else-if="connectionResult?.success"
          :description="t('mcp.noToolsFound')"
        />
      </div>
    </a-modal>
  </Page>
</template>
