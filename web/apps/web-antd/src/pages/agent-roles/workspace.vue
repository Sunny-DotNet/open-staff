<script setup lang="ts">
import type {
  AgentRoleDto,
  AgentSoulDto,
  ConversationTaskOutput,
  ProviderAccountDto,
  SessionEventDto,
  VendorModelCatalogDto,
  VendorProviderConfigurationDto,
  VendorProviderConfigurationPropertyDto,
} from '@openstaff/api';

import { useMutation, useQuery } from '@tanstack/vue-query';
import type { ISubscription } from '@microsoft/signalr';
import {
  getApiAgentRolesById,
  getApiAgentRolesVendorByProviderTypeConfiguration,
  getApiAgentRolesVendorByProviderTypeModelCatalog,
  getApiProviderAccountsByIdModels,
  postApiAgentRoles,
  postApiAgentRolesByIdTestChat,
  putApiAgentRolesById,
  putApiAgentRolesVendorByProviderTypeConfiguration,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, onUnmounted, reactive, ref, watch } from 'vue';

import AgentConversationPanel from '@/components/AgentConversationPanel.vue';
import { appLocale } from '@/app-preferences';
import PermissionRequestModal from '@/components/PermissionRequestModal.vue';
import {
  applySessionConversationEvent,
  createLocalConversationMessageId,
  createSessionConversationState,
  removePendingAssistantPlaceholder,
  startAssistantPlaceholder,
  type SessionConversationMessage,
} from '@/components/session-conversation-stream';
import { useNotification } from '@/composables/useNotification';
import { usePermissionRequests } from '@/composables/usePermissionRequests';
import { t } from '@/i18n';
import { getJobTitleOptions, localizeJobTitle, normalizeJobTitleKey } from '@/utils/job-title';

import {
  createMcpBindingDraft,
  getAgentRoleBindings as getAgentRoleMcpBindings,
  getMcpServers,
  replaceAgentRoleBindings as replaceAgentRoleMcpBindings,
  type McpServerView,
} from '../mcp/api';
import { normalizeOptionalJson } from '../mcp/binding-utils';
import type { McpParameterValues } from '../mcp/structured-values';
import {
  buildDefaultParameterValues,
  mergeParameterValues,
  parseParameterValues,
  stringifyParameterValues,
} from '../mcp/structured-values';
import {
  getAgentRoleSkillBindings,
  getInstalledSkills,
  replaceAgentRoleSkillBindings,
  type AgentRoleSkillBindingDto,
  type InstalledSkillDto,
} from '../skills/api';
import McpBindingAccordionEditor from './McpBindingAccordionEditor.vue';
import SkillBindingListEditor from './SkillBindingListEditor.vue';
import SoulConfigSection from './SoulConfigSection.vue';
import {
  buildSoulPayloadFromForm,
  createEmptySoulForm,
  formatSoulDisplayValue,
  isSoulFormValueEqual,
  loadSoulOptions,
  normalizeSoulFormValue,
  soulDtoToFormValue,
} from './soul-options';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const SOURCE_VENDOR = 3;
const DEFAULT_OPEN_SECTIONS: string[] = [];

type VendorConfigValue = boolean | number | string | null;

interface EditableMcpBinding {
  localId: string;
  mcpServerId: string;
  mcpServerName: string;
  icon?: null | string;
  mode?: null | string;
  transportType?: null | string;
  toolFilter?: null | string;
  selectedProfileId?: null | string;
  parameterValues: McpParameterValues;
  isEnabled: boolean;
}

interface EditableSkillBinding extends AgentRoleSkillBindingDto {
  localId: string;
}

interface McpBindingIssue {
  toolFilter?: string;
}

const props = defineProps<{
  open: boolean;
  providers: ProviderAccountDto[];
  role: AgentRoleDto | null;
}>();

const emit = defineEmits<{
  (event: 'saved'): void;
  (event: 'update:open', value: boolean): void;
}>();

const { connected, streamSession, streamTask } = useNotification();
const currentTestSessionId = ref<null | string>(null);
const {
  activate: activatePermissionRequests,
  activePermissionRequest,
  deactivate: deactivatePermissionRequests,
  permissionRespondingRequestId,
  respondToPermissionRequest,
} = usePermissionRequests({
  sessionId: currentTestSessionId,
});

const conversationState = createSessionConversationState();
const currentSubscription = ref<ISubscription<any> | null>(null);
const currentRole = ref<AgentRoleDto | null>(null);
const roleLoading = ref(false);
const saving = ref(false);
const refreshing = ref(false);
const providerModelsLoading = ref(false);
const providerModelsError = ref<string | null>(null);
const providerModels = ref<Array<{ id?: string }>>([]);
const configurationLoading = ref(false);
const configurationError = ref<string | null>(null);
const catalogLoading = ref(false);
const catalogError = ref<string | null>(null);
const providerConfiguration = ref<null | VendorProviderConfigurationDto>(null);
const modelCatalog = ref<null | VendorModelCatalogDto>(null);
const selectedVendorModel = ref('');
const configValues = reactive<Record<string, VendorConfigValue>>({});
const mcpServers = ref<McpServerView[]>([]);
const mcpBindings = ref<EditableMcpBinding[]>([]);
const mcpLoading = ref(false);
const pendingMcpServerId = ref<string>();
const addingMcpBinding = ref(false);
const activeMcpBindingKeys = ref<string[]>([]);
const installedSkills = ref<InstalledSkillDto[]>([]);
const skillBindings = ref<EditableSkillBinding[]>([]);
const skillsLoading = ref(false);
const skillBindingsSupported = ref(true);
const skillBindingsWarning = ref<string | null>(null);
const pendingSkillInstallKey = ref<string>();
const addingSkillBinding = ref(false);
const fileInputRef = ref<HTMLInputElement | null>(null);
const modelSectionTab = ref('settings');
const activeSections = ref<string[]>([...DEFAULT_OPEN_SECTIONS]);
const roleSoul = ref(createEmptySoulForm());

const chatInput = ref('');
const chatLoading = ref(false);
const chatMessages = ref<SessionConversationMessage[]>([]);

const roleSnapshot = ref('');
const vendorSnapshot = ref('');
const mcpSnapshot = ref('');
const skillSnapshot = ref('');

const roleForm = reactive({
  avatar: '',
  config: '',
  description: '',
  jobTitle: '',
  modelName: '',
  modelProviderId: '',
  name: '',
  temperature: 0.7,
});

const saveMutation = useMutation({
  mutationFn: saveAllInternal,
  onSuccess: () => {
    message.success(t('role.workspaceSaved'));
  },
});

const soulOptionsQuery = useQuery({
  queryKey: ['agent-soul-options', computed(() => appLocale.value)],
  queryFn: () => loadSoulOptions(appLocale.value),
  staleTime: 5 * 60 * 1000,
});

const enabledProviders = computed(() =>
  props.providers.filter((provider) => provider.isEnabled),
);

const roleId = computed(() => currentRole.value?.id ?? props.role?.id ?? '');
const providerType = computed(
  () => currentRole.value?.providerType ?? props.role?.providerType ?? '',
);
const isPersistedRole = computed(
  () => !!roleId.value && roleId.value !== EMPTY_GUID,
);
const isVendorRole = computed(
  () => !!providerType.value || (currentRole.value?.source ?? props.role?.source) === SOURCE_VENDOR,
);
const workspaceTitle = computed(
  () => roleForm.name.trim() || currentRole.value?.name || props.role?.name || t('role.unnamedRole'),
);
const canTestChat = computed(() => isPersistedRole.value && !roleLoading.value);
const currentModelName = computed(() =>
  isVendorRole.value ? selectedVendorModel.value : roleForm.modelName,
);
const configurationProperties = computed(
  () => providerConfiguration.value?.properties ?? [],
);
const vendorModels = computed(() => modelCatalog.value?.models ?? []);
const missingConfigurationFields = computed(
  () => modelCatalog.value?.missingConfigurationFields ?? [],
);
const availableMcpServers = computed(() => {
  const boundIds = new Set(mcpBindings.value.map((binding) => binding.mcpServerId));
  return mcpServers.value.filter(
    (server) => server.isEnabled && server.id && !boundIds.has(server.id),
  );
});
const availableSkills = computed(() => {
  const boundKeys = new Set(skillBindings.value.map((binding) => binding.skillInstallKey ?? ''));
  return installedSkills.value.filter(
    (skill) =>
      skill.installKey &&
      skill.status === 'installed' &&
      !boundKeys.has(skill.installKey),
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
          issue.toolFilter = '工具白名单必须是字符串数组 JSON';
        }
      } catch {
        issue.toolFilter = '工具白名单不是合法 JSON 数组';
      }
    }

    if (issue.toolFilter) {
      issues[binding.localId] = issue;
    }
  }

  return issues;
});

const basicSummary = computed(() =>
  [
    roleForm.name.trim() || workspaceTitle.value,
    localizeJobTitle(roleForm.jobTitle, roleForm.jobTitle)?.trim() || '',
  ]
    .filter(Boolean)
    .join(' / ') || '未配置',
);

const jobTitleOptions = computed(() => getJobTitleOptions(roleForm.jobTitle));

const modelSummary = computed(() => {
  if (isVendorRole.value) {
    return currentModelName.value || '未选择模型';
  }

  const providerName = enabledProviders.value.find(
    (provider) => provider.id === roleForm.modelProviderId,
  )?.name;

  return [providerName || roleForm.modelProviderId, currentModelName.value]
    .filter(Boolean)
    .join(' / ') || '使用角色默认模型';
});

const soulSummary = computed(() => {
  const parts: string[] = [];
  const catalog = soulOptionsQuery.data.value;
  if (roleSoul.value.traits.length) {
    parts.push(`${roleSoul.value.traits.length} 个特征`);
  }
  if (roleSoul.value.style.trim()) {
    parts.push(
      formatSoulDisplayValue(roleSoul.value.style.trim(), catalog?.styles),
    );
  }
  if (roleSoul.value.attitudes.length) {
    parts.push(`${roleSoul.value.attitudes.length} 个态度`);
  }

  return parts.join(' · ') || '未配置';
});

const mcpSummary = computed(() => {
  if (!mcpBindings.value.length) {
    return '未绑定';
  }

  const enabledCount = mcpBindings.value.filter((binding) => binding.isEnabled).length;
  const invalidCount = Object.keys(mcpBindingIssues.value).length;
  return [
    `${enabledCount}/${mcpBindings.value.length} 已启用`,
    invalidCount > 0 ? `${invalidCount} 项错误` : null,
  ]
    .filter(Boolean)
    .join(' · ');
});

const skillsSummary = computed(() => {
  if (!skillBindings.value.length) {
    return '未绑定';
  }

  const enabledCount = skillBindings.value.filter((binding) => binding.isEnabled).length;
  const missingCount = skillBindings.value.filter(
    (binding) => binding.resolutionStatus === 'missing',
  ).length;
  return [
    `${enabledCount}/${skillBindings.value.length} 已启用`,
    missingCount > 0 ? `${missingCount} 项缺失` : null,
  ]
    .filter(Boolean)
    .join(' · ');
});

const roleStateSignature = computed(() =>
  JSON.stringify({
    avatar: roleForm.avatar || null,
    config: buildPersistedConfigPreview(),
    description: roleForm.description.trim() || null,
    jobTitle: roleForm.jobTitle.trim() || null,
    modelName: currentModelName.value || null,
    modelProviderId: isVendorRole.value ? null : roleForm.modelProviderId || null,
    name: roleForm.name.trim() || null,
    soul: buildSoulPayload() ?? null,
  }),
);

const vendorStateSignature = computed(() =>
  JSON.stringify({
    configuration: buildConfigurationPayload(),
    modelName: selectedVendorModel.value || null,
  }),
);

const mcpStateSignature = computed(() =>
  JSON.stringify(
    mcpBindings.value.map((binding) => ({
      mcpServerId: binding.mcpServerId,
      toolFilter: normalizeOptionalJson(binding.toolFilter),
      selectedProfileId: binding.selectedProfileId || null,
      parameterValues: stringifyParameterValues(binding.parameterValues),
      isEnabled: binding.isEnabled,
    })),
  ),
);

const skillStateSignature = computed(() =>
  JSON.stringify(
    skillBindings.value.map((binding) => ({
      skillInstallKey: binding.skillInstallKey,
      skillId: binding.skillId,
      name: binding.name,
      displayName: binding.displayName,
      source: binding.source,
      owner: binding.owner,
      repo: binding.repo,
      githubUrl: binding.githubUrl,
      isEnabled: binding.isEnabled,
    })),
  ),
);

const hasUnsavedChanges = computed(() =>
  roleSnapshot.value !== roleStateSignature.value ||
  vendorSnapshot.value !== vendorStateSignature.value ||
  mcpSnapshot.value !== mcpStateSignature.value ||
  skillSnapshot.value !== skillStateSignature.value,
);

const MCP_MODE_LABELS: Record<string, { color: string; label: string }> = {
  local: { label: '本地', color: 'blue' },
  remote: { label: '远程', color: 'purple' },
  managed: { label: '受管', color: 'green' },
  unknown: { label: '未知', color: 'default' },
};

watch(
  () => props.open,
  async (open) => {
    if (!open) {
      currentTestSessionId.value = null;
      await deactivatePermissionRequests();
      disposeStream();
      return;
    }

    await activatePermissionRequests();
    await initializeWorkspace();
  },
  { immediate: true },
);

watch(
  () => [props.open, roleForm.modelProviderId, isVendorRole.value] as const,
  async ([open, modelProviderId, vendorRole]) => {
    if (!open || vendorRole) {
      providerModels.value = [];
      providerModelsError.value = null;
      providerModelsLoading.value = false;
      return;
    }

    if (!modelProviderId) {
      providerModels.value = [];
      providerModelsError.value = null;
      return;
    }

    await loadProviderModels(modelProviderId);
  },
  { immediate: true },
);

watch(
  [() => soulOptionsQuery.data.value, roleSoul],
  ([catalog, current]) => {
    if (!catalog) {
      return;
    }

    const normalized = normalizeSoulFormValue(current, catalog);
    if (!isSoulFormValueEqual(current, normalized)) {
      roleSoul.value = normalized;
    }
  },
  { immediate: true, deep: true },
);

onUnmounted(() => {
  void deactivatePermissionRequests();
  disposeStream();
});

async function initializeWorkspace() {
  refreshing.value = true;
  try {
    clearChat();
    activeSections.value = [...DEFAULT_OPEN_SECTIONS];
    modelSectionTab.value = 'settings';
    activeMcpBindingKeys.value = [];
    currentRole.value = props.role ? { ...props.role } : null;

    if (isPersistedRole.value) {
      await loadRoleDetail(roleId.value);
    } else {
      populateForm(currentRole.value);
      captureRoleSnapshot();
    }

    if (providerType.value) {
      await Promise.all([loadVendorConfiguration(), loadVendorCatalog()]);
    } else {
      resetVendorState();
      captureVendorSnapshot();
    }

    if (isPersistedRole.value) {
      await Promise.all([loadMcpState(roleId.value), loadSkillState(roleId.value)]);
    } else {
      resetBindingsState();
    }

    await loadMcpServers();
    await loadInstalledSkills();
    captureSnapshots();
  } finally {
    refreshing.value = false;
  }
}

async function loadRoleDetail(id: string) {
  roleLoading.value = true;
  try {
    const role = unwrapClientEnvelope(
      await getApiAgentRolesById({
        path: { id },
      }),
    );
    currentRole.value = role;
    populateForm(role);
  } finally {
    roleLoading.value = false;
  }
}

function populateForm(role: AgentRoleDto | null) {
  roleForm.avatar = role?.avatar ?? '';
  roleForm.config = prettyJson(role?.config ?? '');
  roleForm.description = role?.description ?? '';
  roleForm.jobTitle = normalizeJobTitleKey(role?.jobTitle) ?? role?.jobTitle ?? '';
  roleForm.modelName = role?.modelName ?? '';
  roleForm.modelProviderId = role?.modelProviderId ?? '';
  roleForm.name = role?.name ?? '';
  roleSoul.value = soulDtoToFormValue(role?.soul);
  roleForm.temperature = parseTemperatureFromConfig(role?.config);
  selectedVendorModel.value = role?.modelName ?? '';
}

async function loadProviderModels(modelProviderId: string) {
  providerModelsLoading.value = true;
  providerModelsError.value = null;

  try {
    providerModels.value = unwrapClientEnvelope(
      await getApiProviderAccountsByIdModels({
        path: { id: modelProviderId },
      }),
    );
  } catch (error) {
    providerModels.value = [];
    providerModelsError.value = getErrorMessage(error, t('role.actionFailed'));
  } finally {
    providerModelsLoading.value = false;
  }
}

async function loadVendorConfiguration() {
  if (!providerType.value) {
    return;
  }

  configurationLoading.value = true;
  configurationError.value = null;

  try {
    const snapshot = unwrapClientEnvelope(
      await getApiAgentRolesVendorByProviderTypeConfiguration({
        path: { providerType: providerType.value },
      }),
    );

    providerConfiguration.value = snapshot;
    clearConfigValues();

    for (const property of snapshot.properties ?? []) {
      if (!property.name) {
        continue;
      }

      configValues[property.name] = coerceConfigValue(
        property,
        snapshot.configuration?.[property.name],
      );
    }
  } catch (error) {
    configurationError.value = getErrorMessage(error, t('role.vendorConfigLoadFailed'));
  } finally {
    configurationLoading.value = false;
  }
}

async function loadVendorCatalog() {
  if (!providerType.value) {
    return;
  }

  catalogLoading.value = true;
  catalogError.value = null;

  try {
    modelCatalog.value = unwrapClientEnvelope(
      await getApiAgentRolesVendorByProviderTypeModelCatalog({
        path: { providerType: providerType.value },
      }),
    );

    if (!selectedVendorModel.value) {
      selectedVendorModel.value = currentRole.value?.modelName ?? '';
    }
  } catch (error) {
    modelCatalog.value = null;
    catalogError.value = getErrorMessage(error, t('role.vendorCatalogLoadFailed'));
  } finally {
    catalogLoading.value = false;
  }
}

async function loadMcpServers() {
  mcpServers.value = await getMcpServers({});
}

async function loadMcpState(agentRoleId: string) {
  mcpLoading.value = true;
  try {
    const bindings = await getAgentRoleMcpBindings(agentRoleId);
    mcpBindings.value = bindings.map((binding, index) => ({
      localId: `${binding.mcpServerId ?? 'mcp'}-${index}`,
      mcpServerId: binding.mcpServerId ?? '',
      mcpServerName: binding.mcpServerName ?? '未命名 MCP',
      icon: binding.icon ?? null,
      mode: binding.mode ?? null,
      transportType: binding.transportType ?? null,
      toolFilter: binding.toolFilter ?? null,
      selectedProfileId: binding.selectedProfileId ?? null,
      parameterValues: mergeParameterValues(
        buildDefaultParameterValues(
          mcpServers.value.find((server) => server.id === binding.mcpServerId)?.parameterSchema,
          binding.selectedProfileId,
        ),
        parseParameterValues(binding.parameterValues),
      ),
      isEnabled: binding.isEnabled ?? true,
    }));
  } finally {
    mcpLoading.value = false;
  }
}

async function loadInstalledSkills() {
  installedSkills.value = await getInstalledSkills();
}

async function loadSkillState(agentRoleId: string) {
  skillsLoading.value = true;
  try {
    const bindings = await getAgentRoleSkillBindings(agentRoleId);
    skillBindingsSupported.value = true;
    skillBindingsWarning.value = null;
    skillBindings.value = bindings.map((binding, index) => ({
      ...binding,
      localId: `${binding.skillInstallKey ?? 'skill'}-${index}`,
    }));
  } catch (error) {
    const text = getErrorMessage(error, '');
    if (text.includes('404')) {
      skillBindingsSupported.value = false;
      skillBindingsWarning.value = '当前运行中的后端尚未加载角色 Skill 绑定接口，重启宿主后即可使用该部分功能。';
      skillBindings.value = [];
      return;
    }

    throw error;
  } finally {
    skillsLoading.value = false;
  }
}

function resetVendorState() {
  providerConfiguration.value = null;
  modelCatalog.value = null;
  configurationError.value = null;
  catalogError.value = null;
  selectedVendorModel.value = '';
  clearConfigValues();
}

function resetBindingsState() {
  mcpBindings.value = [];
  skillBindings.value = [];
  skillBindingsSupported.value = true;
  skillBindingsWarning.value = null;
  pendingMcpServerId.value = undefined;
  pendingSkillInstallKey.value = undefined;
  activeMcpBindingKeys.value = [];
  captureMcpSnapshot();
  captureSkillSnapshot();
}

function captureRoleSnapshot() {
  roleSnapshot.value = roleStateSignature.value;
}

function captureVendorSnapshot() {
  vendorSnapshot.value = vendorStateSignature.value;
}

function captureMcpSnapshot() {
  mcpSnapshot.value = mcpStateSignature.value;
}

function captureSkillSnapshot() {
  skillSnapshot.value = skillStateSignature.value;
}

function captureSnapshots() {
  captureRoleSnapshot();
  captureVendorSnapshot();
  captureMcpSnapshot();
  captureSkillSnapshot();
}

async function refreshWorkspace() {
  try {
    await initializeWorkspace();
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

async function handleSaveAll() {
  if (saveMutation.isPending.value) {
    return;
  }

  try {
    await saveMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

async function saveAllInternal() {
  if (!validateWorkspace()) {
    return;
  }

  saving.value = true;
  try {
    if (isVendorRole.value && providerType.value) {
      await saveVendorConfiguration();
    }

    const savedRole = await persistRole();
    currentRole.value = savedRole;

    if (savedRole.id) {
      await Promise.all([
        persistMcpBindings(savedRole.id),
        persistSkillBindings(savedRole.id),
      ]);
      await loadRoleDetail(savedRole.id);
      await Promise.all([
        loadMcpState(savedRole.id),
        loadSkillState(savedRole.id),
      ]);
    }

    await loadInstalledSkills();
    await loadMcpServers();
    captureSnapshots();
    emit('saved');
  } finally {
    saving.value = false;
  }
}

function validateWorkspace() {
  if (!roleForm.name.trim() && !workspaceTitle.value.trim()) {
    message.error(t('role.validationName'));
    return false;
  }

  if (!isValidJson(roleForm.config)) {
    message.error(t('role.validationConfigJson'));
    return false;
  }

  const firstMcpIssue = Object.values(mcpBindingIssues.value)[0];
  if (firstMcpIssue?.toolFilter) {
    message.error(firstMcpIssue.toolFilter ?? t('role.actionFailed'));
    return false;
  }

  if (isVendorRole.value) {
    if (!selectedVendorModel.value) {
      message.error(t('role.validationModel'));
      return false;
    }

    if (!validateConfiguration()) {
      return false;
    }
  }

  return true;
}

async function saveVendorConfiguration() {
  if (!providerType.value) {
    return;
  }

  const snapshot = unwrapClientEnvelope(
    await putApiAgentRolesVendorByProviderTypeConfiguration({
      body: {
        configuration: buildConfigurationPayload(),
      },
      path: { providerType: providerType.value },
    }),
  );

  providerConfiguration.value = snapshot;
  await loadVendorCatalog();
}

async function persistRole() {
  if (isPersistedRole.value) {
    const body = {
      avatar: roleForm.avatar,
      config: buildPersistedConfig(),
      description: roleForm.description.trim(),
      jobTitle: roleForm.jobTitle.trim(),
      modelName: currentModelName.value ?? '',
      modelProviderId: isVendorRole.value ? undefined : (roleForm.modelProviderId ?? ''),
      name: roleForm.name.trim() || workspaceTitle.value,
      soul: buildSoulPayload(),
    };

    return unwrapClientEnvelope(
      await putApiAgentRolesById({
        body,
        path: { id: roleId.value },
      }),
    );
  }

  const body = {
    avatar: roleForm.avatar || undefined,
    config: buildPersistedConfig() || undefined,
    description: roleForm.description.trim() || undefined,
    jobTitle: roleForm.jobTitle.trim() || undefined,
    modelName: currentModelName.value || undefined,
    modelProviderId: isVendorRole.value ? undefined : roleForm.modelProviderId || undefined,
    name: roleForm.name.trim() || workspaceTitle.value,
    providerType: providerType.value || undefined,
    roleType: currentRole.value?.roleType || providerType.value || undefined,
    source: currentRole.value?.source ?? props.role?.source,
    soul: buildSoulPayload(),
  };

  return unwrapClientEnvelope(
    await postApiAgentRoles({
      body,
    }),
  );
}

async function persistMcpBindings(agentRoleId: string) {
  await replaceAgentRoleMcpBindings(
    agentRoleId,
    mcpBindings.value.map((binding) => ({
      mcpServerId: binding.mcpServerId,
      toolFilter: normalizeOptionalJson(binding.toolFilter),
      selectedProfileId: binding.selectedProfileId || null,
      parameterValues: stringifyParameterValues(binding.parameterValues),
      isEnabled: binding.isEnabled,
    })),
  );
}

async function persistSkillBindings(agentRoleId: string) {
  if (!skillBindingsSupported.value) {
    return;
  }

  await replaceAgentRoleSkillBindings(
    agentRoleId,
    skillBindings.value.map((binding) => ({
      skillInstallKey: binding.skillInstallKey ?? '',
      skillId: binding.skillId ?? '',
      name: binding.name ?? '',
      displayName: binding.displayName ?? '',
      source: binding.source ?? '',
      owner: binding.owner ?? '',
      repo: binding.repo ?? '',
      githubUrl: binding.githubUrl ?? undefined,
      isEnabled: binding.isEnabled ?? true,
    })),
  );
}

async function addMcpBinding() {
  if (!pendingMcpServerId.value) {
    return;
  }

  addingMcpBinding.value = true;
  try {
    const server = mcpServers.value.find((item) => item.id === pendingMcpServerId.value);
    if (!server?.id) {
      return;
    }

    const draft = await createMcpBindingDraft({
      agentRoleId: roleId.value || undefined,
      mcpServerId: server.id,
      scope: 'agent-role',
    });

    const localId = `${server.id}-${Date.now()}`;
    mcpBindings.value.push({
      localId,
      mcpServerId: server.id,
      mcpServerName: server.name ?? '未命名 MCP',
      icon: server.icon ?? null,
      mode: server.mode ?? null,
      transportType: server.transportType ?? null,
      toolFilter: draft.toolFilter ?? null,
      selectedProfileId: draft.selectedProfileId ?? null,
      parameterValues: mergeParameterValues(
        buildDefaultParameterValues(server.parameterSchema, draft.selectedProfileId),
        parseParameterValues(draft.parameterValues),
      ),
      isEnabled: draft.isEnabled ?? true,
    });
    activeMcpBindingKeys.value = [...activeMcpBindingKeys.value, localId];
    pendingMcpServerId.value = undefined;
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

async function addSkillBinding() {
  if (!pendingSkillInstallKey.value) {
    return;
  }

  const installed = availableSkills.value.find(
    (item) => item.installKey === pendingSkillInstallKey.value,
  );
  if (!installed) {
    return;
  }

  addingSkillBinding.value = true;
  try {
    skillBindings.value.push({
      ...toSkillBinding(installed),
      localId: `${installed.installKey}-${Date.now()}`,
    });
    pendingSkillInstallKey.value = undefined;
  } finally {
    addingSkillBinding.value = false;
  }
}

function removeSkillBinding(skillInstallKey: string) {
  skillBindings.value = skillBindings.value.filter(
    (binding) => binding.skillInstallKey !== skillInstallKey,
  );
}

async function sendTestMessage() {
  if (!canTestChat.value || !chatInput.value.trim() || chatLoading.value) {
    return;
  }

  const userMessage = chatInput.value.trim();
  chatMessages.value.push({
    id: createLocalConversationMessageId('local'),
    role: 'user',
    content: userMessage,
    timestamp: new Date().toISOString(),
  });
  chatInput.value = '';
  chatLoading.value = true;

  try {
    const response = unwrapClientEnvelope(
      await postApiAgentRolesByIdTestChat({
        body: {
          message: userMessage,
          override: buildOverridePayload(),
        },
        path: { id: roleId.value },
      }),
    );

    const taskId = response.taskId;
    if (!taskId) {
      throw new Error(t('role.testChatStartFailed'));
    }

    currentTestSessionId.value = response.sessionId ?? null;
    disposeStream();
    startAssistantPlaceholder(chatMessages.value, conversationState);
    currentSubscription.value = await connectTestConversationStream(
      response,
      taskId,
      (event) => {
        const reduced = applySessionConversationEvent(
          chatMessages.value,
          conversationState,
          event,
        );
        if (reduced.clearSending) {
          chatLoading.value = false;
        }
      },
      () => {
        chatLoading.value = false;
        currentSubscription.value = null;
        conversationState.pendingAssistantId = null;
      },
      (error) => {
        chatLoading.value = false;
        currentSubscription.value = null;
        removePendingAssistantPlaceholder(chatMessages.value, conversationState);
        chatMessages.value.push({
          id: createLocalConversationMessageId('assistant-error'),
          role: 'assistant',
          content: `❌ ${error.message}`,
          timestamp: new Date().toISOString(),
        });
      },
    );
  } catch (error) {
    removePendingAssistantPlaceholder(chatMessages.value, conversationState);
    chatLoading.value = false;
    message.error(getErrorMessage(error, t('role.testChatStartFailed')));
  }
}

async function connectTestConversationStream(
  response: ConversationTaskOutput,
  fallbackTaskId: string,
  onEvent: (event: SessionEventDto) => void,
  onComplete: () => void,
  onError: (error: Error) => void,
) {
  if (response.sessionId) {
    return await streamSession(response.sessionId, onEvent, onComplete, onError);
  }

  return await streamTask(fallbackTaskId, onEvent, onComplete, onError);
}

function buildOverridePayload() {
  return {
    description: roleForm.description.trim() || undefined,
    modelName: currentModelName.value || undefined,
    modelProviderId: isVendorRole.value ? undefined : roleForm.modelProviderId || undefined,
    name: roleForm.name.trim() || undefined,
    soul: buildSoulPayload(),
    temperature: roleForm.temperature,
  };
}

function buildSoulPayload(): AgentSoulDto | undefined {
  return buildSoulPayloadFromForm(roleSoul.value);
}

function buildPersistedConfig() {
  const trimmed = roleForm.config.trim();
  const parsed = trimmed ? JSON.parse(trimmed) : {};
  if (typeof parsed !== 'object' || Array.isArray(parsed) || parsed === null) {
    throw new Error(t('role.validationConfigJson'));
  }

  const modelParameters = parsed.modelParameters;
  const nextModelParameters =
    modelParameters && typeof modelParameters === 'object' && !Array.isArray(modelParameters)
      ? { ...modelParameters }
      : {};

  nextModelParameters.temperature = roleForm.temperature;
  parsed.modelParameters = nextModelParameters;
  return JSON.stringify(parsed, null, 2);
}

function buildPersistedConfigPreview() {
  try {
    return buildPersistedConfig();
  } catch {
    return roleForm.config;
  }
}

function clearChat() {
  disposeStream();
  currentTestSessionId.value = null;
  chatLoading.value = false;
  chatMessages.value = [];
  conversationState.pendingAssistantId = null;
}

function disposeStream() {
  currentSubscription.value?.dispose();
  currentSubscription.value = null;
}

function closeModal() {
  emit('update:open', false);
}

function openAvatarPicker() {
  fileInputRef.value?.click();
}

function clearAvatar() {
  roleForm.avatar = '';
}

function onAvatarFileChange(event: Event) {
  const input = event.target as HTMLInputElement | null;
  const file = input?.files?.[0];
  if (!file) {
    return;
  }

  const reader = new FileReader();
  reader.onerror = () => {
    if (input) {
      input.value = '';
    }
    message.error(t('role.avatarReadFailed'));
  };
  reader.onload = () => {
    const result = reader.result;
    if (typeof result !== 'string') {
      message.error(t('role.avatarReadFailed'));
      return;
    }

    const image = new Image();
    image.onerror = () => {
      if (input) {
        input.value = '';
      }
      message.error(t('role.avatarReadFailed'));
    };
    image.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = 128;
      canvas.height = 128;
      const context = canvas.getContext('2d');
      if (!context) {
        message.error(t('role.avatarReadFailed'));
        return;
      }

      context.drawImage(image, 0, 0, 128, 128);
      roleForm.avatar = canvas.toDataURL('image/png');
      if (input) {
        input.value = '';
      }
    };
    image.src = result;
  };
  reader.readAsDataURL(file);
}

function prettyJson(value: string) {
  if (!value.trim()) {
    return '';
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function parseTemperatureFromConfig(config?: null | string) {
  if (!config) {
    return 0.7;
  }

  try {
    const parsed = JSON.parse(config) as {
      modelParameters?: { temperature?: number | string };
    };
    const temperature = Number(parsed.modelParameters?.temperature ?? 0.7);
    return Number.isNaN(temperature) ? 0.7 : temperature;
  } catch {
    return 0.7;
  }
}

function isValidJson(value: string) {
  if (!value.trim()) {
    return true;
  }

  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}

function clearConfigValues() {
  for (const key of Object.keys(configValues)) {
    delete configValues[key];
  }
}

function coerceConfigValue(
  property: VendorProviderConfigurationPropertyDto,
  value: unknown,
): VendorConfigValue {
  const candidate = value ?? property.defaultValue;

  switch (property.fieldType) {
    case 'boolean':
      if (typeof candidate === 'boolean') {
        return candidate;
      }
      if (typeof candidate === 'string') {
        return candidate.toLowerCase() === 'true';
      }
      if (typeof candidate === 'number') {
        return candidate !== 0;
      }
      return false;
    case 'double':
    case 'int64':
      if (typeof candidate === 'number') {
        return candidate;
      }
      if (typeof candidate === 'string' && candidate.trim()) {
        const parsed = Number(candidate);
        return Number.isNaN(parsed) ? null : parsed;
      }
      return candidate == null ? null : Number(candidate);
    default:
      return candidate == null ? '' : String(candidate);
  }
}

function normalizeConfigValue(
  property: VendorProviderConfigurationPropertyDto,
  value?: VendorConfigValue,
): VendorConfigValue {
  switch (property.fieldType) {
    case 'boolean':
      return Boolean(value);
    case 'double':
    case 'int64':
      return value == null || value === '' ? null : Number(value);
    default:
      return value == null ? '' : String(value);
  }
}

function formatConfigLabel(name?: string) {
  if (!name) {
    return '--';
  }

  return name
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[_-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function validateConfiguration() {
  for (const property of configurationProperties.value) {
    if (!property.name || !property.required) {
      continue;
    }

    const value = normalizeConfigValue(property, configValues[property.name]);
    if (property.fieldType === 'boolean') {
      continue;
    }

    if (value == null || value === '') {
      message.error(
        t('role.validationVendorField', {
          field: formatConfigLabel(property.name),
        }),
      );
      return false;
    }
  }

  return true;
}

function buildConfigurationPayload() {
  const payload: Record<string, VendorConfigValue> = {};

  for (const property of configurationProperties.value) {
    if (!property.name) {
      continue;
    }

    payload[property.name] = normalizeConfigValue(property, configValues[property.name]);
  }

  return payload;
}

function toSkillBinding(installed: InstalledSkillDto): EditableSkillBinding {
  return {
    id: undefined,
    agentRoleId: roleId.value,
    skillInstallKey: installed.installKey,
    skillId: installed.skillId,
    name: installed.name,
    displayName: installed.displayName,
    source: installed.source,
    owner: installed.owner,
    repo: installed.repo,
    githubUrl: installed.githubUrl,
    isEnabled: true,
    resolutionStatus: 'resolved',
    resolutionMessage: null,
    installRootPath: installed.installRootPath,
    createdAt: undefined,
    updatedAt: undefined,
    localId: installed.installKey ?? `${Date.now()}`,
  };
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <a-modal
    :open="open"
    :title="`${t('role.workspaceTitle')} · ${workspaceTitle}`"
    :footer="null"
    :width="1480"
    wrap-class-name="agent-role-workspace-modal"
    destroy-on-close
    @cancel="closeModal"
  >
    <div class="workspace-shell">
      <section class="workspace-chat">
        <AgentConversationPanel
          v-model="chatInput"
          :agent-avatar="roleForm.avatar || currentRole?.avatar"
          :agent-name="workspaceTitle"
          :clearable="true"
          :clear-disabled="!chatMessages.length && !chatLoading"
          :empty-description="canTestChat ? t('role.testChatEmpty') : t('role.workspaceNeedSave')"
          :input-disabled="!canTestChat"
          :messages="chatMessages"
          :send-disabled="!canTestChat"
          :sending="chatLoading"
          :subtitle="connected ? t('role.signalrConnected') : t('role.signalrConnecting')"
          :subtitle-status="connected ? 'online' : 'connecting'"
          @clear="clearChat"
          @send="sendTestMessage"
        >
          <template #banner>
            <a-alert
              v-if="!canTestChat"
              type="info"
              show-icon
              :message="t('role.workspaceNeedSave')"
            />
          </template>
        </AgentConversationPanel>
      </section>

      <section class="workspace-config">
        <div class="workspace-toolbar">
          <div>
            <div class="workspace-toolbar-title">{{ t('role.workspaceConfigTitle') }}</div>
            <div class="workspace-toolbar-description">
              {{ t('role.workspaceConfigDescription') }}
            </div>
          </div>
          <a-space wrap>
            <a-tag v-if="hasUnsavedChanges" color="gold">
              {{ t('role.workspaceDirty') }}
            </a-tag>
            <a-button
              :loading="refreshing"
              @click="refreshWorkspace"
            >
              {{ t('common.refresh') }}
            </a-button>
            <a-button
              type="primary"
              :loading="saving || saveMutation.isPending.value"
              @click="handleSaveAll"
            >
              {{ t('role.save') }}
            </a-button>
          </a-space>
        </div>

        <div class="workspace-config-scroll">
          <a-collapse
            v-model:activeKey="activeSections"
            class="workspace-sections"
            :bordered="false"
          >
          <a-collapse-panel key="basic">
            <template #header>
              <div class="section-header">
                <span>{{ t('role.basicSection') }}</span>
                <span class="section-summary">{{ basicSummary }}</span>
              </div>
            </template>

            <div class="section-body">
              <div class="avatar-row">
                <div class="avatar-preview">
                  <img
                    v-if="roleForm.avatar"
                    :src="roleForm.avatar"
                    alt=""
                    class="avatar-image"
                  />
                  <span v-else class="avatar-fallback">🧠</span>
                </div>

                <div class="avatar-actions">
                  <a-space>
                    <a-button size="small" @click="openAvatarPicker">
                      {{ t('role.uploadAvatar') }}
                    </a-button>
                    <a-button v-if="roleForm.avatar" danger size="small" @click="clearAvatar">
                      {{ t('role.removeAvatar') }}
                    </a-button>
                  </a-space>
                  <div class="avatar-hint">
                    {{ t('role.avatarHint') }}
                  </div>
                  <input
                    ref="fileInputRef"
                    accept="image/png,image/jpeg,image/webp"
                    class="hidden"
                    type="file"
                    @change="onAvatarFileChange"
                  />
                </div>
              </div>

              <a-form layout="vertical">
                <div class="grid gap-4 md:grid-cols-2">
                  <a-form-item :label="t('role.name')" required>
                    <a-input
                      v-model:value="roleForm.name"
                      :placeholder="t('role.namePlaceholder')"
                    />
                  </a-form-item>
                  <a-form-item :label="t('role.jobTitle')">
                    <a-select
                      v-model:value="roleForm.jobTitle"
                      allow-clear
                      show-search
                      :options="jobTitleOptions"
                      option-filter-prop="label"
                      :placeholder="t('role.jobTitleSelectPlaceholder')"
                    />
                  </a-form-item>
                </div>
                <a-form-item :label="t('role.description')">
                  <a-textarea
                    v-model:value="roleForm.description"
                    :rows="3"
                    :placeholder="t('role.descriptionPlaceholder')"
                  />
                </a-form-item>
              </a-form>
            </div>
          </a-collapse-panel>

          <a-collapse-panel key="model">
            <template #header>
              <div class="section-header">
                <span>{{ t('role.modelSection') }}</span>
                <span class="section-summary">{{ modelSummary }}</span>
              </div>
            </template>

            <div class="section-body">
              <a-tabs v-model:activeKey="modelSectionTab">
                <a-tab-pane key="settings" :tab="t('role.modelSettingsTab')">
                  <a-form layout="vertical">
                    <div v-if="isVendorRole" class="grid gap-4 md:grid-cols-2">
                      <a-form-item :label="t('role.model')">
                        <a-select
                          v-model:value="selectedVendorModel"
                          allow-clear
                          show-search
                          :loading="catalogLoading"
                          :options="
                            vendorModels.map((model) => ({
                              label: model.name || model.id,
                              value: model.id,
                            }))
                          "
                          :placeholder="t('role.modelPlaceholder')"
                        />
                      </a-form-item>
                      <a-form-item :label="t('role.modelCatalogStatus')">
                        <a-input
                          :value="modelCatalog?.status || '--'"
                          disabled
                        />
                      </a-form-item>
                    </div>

                    <div v-else class="grid gap-4 md:grid-cols-2">
                      <a-form-item :label="t('role.modelProvider')">
                        <a-select
                          v-model:value="roleForm.modelProviderId"
                          allow-clear
                          :options="
                            enabledProviders.map((provider) => ({
                              label: provider.name ?? provider.id ?? '--',
                              value: provider.id ?? '',
                            }))
                          "
                          :placeholder="t('role.modelProviderPlaceholder')"
                        />
                      </a-form-item>
                      <a-form-item :label="t('role.model')">
                        <a-select
                          v-model:value="roleForm.modelName"
                          allow-clear
                          show-search
                          :disabled="!roleForm.modelProviderId"
                          :loading="providerModelsLoading"
                          :options="
                            providerModels.map((model) => ({
                              label: model.id ?? '--',
                              value: model.id ?? '',
                            }))
                          "
                          :placeholder="t('role.modelPlaceholder')"
                        />
                      </a-form-item>
                    </div>

                    <a-alert
                      v-if="providerModelsError"
                      type="warning"
                      show-icon
                      class="mb-4"
                      :message="providerModelsError"
                    />

                    <a-form-item :label="t('role.temperature')">
                      <a-slider
                        v-model:value="roleForm.temperature"
                        :min="0"
                        :max="2"
                        :step="0.1"
                      />
                    </a-form-item>
                  </a-form>
                </a-tab-pane>
                <a-tab-pane key="raw-config" :tab="t('role.rawConfigTab')">
                  <a-form layout="vertical">
                    <a-form-item :label="t('role.rawConfig')">
                      <a-textarea
                        v-model:value="roleForm.config"
                        :rows="10"
                        :placeholder="t('role.configPlaceholder')"
                      />
                    </a-form-item>
                  </a-form>
                </a-tab-pane>
              </a-tabs>
            </div>
          </a-collapse-panel>

          <a-collapse-panel key="soul">
            <template #header>
              <div class="section-header">
                <span>{{ t('role.soulSection') }}</span>
                <span class="section-summary">{{ soulSummary }}</span>
              </div>
            </template>

            <div class="section-body">
              <a-form layout="vertical">
                <SoulConfigSection v-model="roleSoul" />
              </a-form>
            </div>
          </a-collapse-panel>

          <a-collapse-panel v-if="isVendorRole" key="vendor">
            <template #header>
              <div class="section-header">
                <span>{{ t('role.vendorConfigSection') }}</span>
                <span class="section-summary">
                  {{ providerConfiguration?.displayName || providerType || '--' }}
                </span>
              </div>
            </template>

            <div class="section-body">
              <a-alert
                v-if="configurationError"
                type="warning"
                show-icon
                class="mb-4"
                :message="configurationError"
              />
              <a-alert
                v-if="catalogError"
                type="warning"
                show-icon
                class="mb-4"
                :message="catalogError"
              />
              <a-alert
                v-if="missingConfigurationFields.length"
                type="info"
                show-icon
                class="mb-4"
                :message="t('role.vendorCatalogNeedsConfig')"
                :description="`${t('role.missingFields')}：${missingConfigurationFields.join(', ')}`"
              />

              <a-form layout="vertical">
                <div v-if="configurationProperties.length" class="grid gap-4 md:grid-cols-2">
                  <a-form-item
                    v-for="property in configurationProperties"
                    :key="property.name"
                    :label="formatConfigLabel(property.name)"
                    :required="property.required"
                  >
                    <a-switch
                      v-if="property.fieldType === 'boolean'"
                      v-model:checked="configValues[property.name ?? '']"
                    />
                    <a-input-number
                      v-else-if="property.fieldType === 'double' || property.fieldType === 'int64'"
                      v-model:value="configValues[property.name ?? '']"
                      style="width: 100%"
                    />
                    <a-input
                      v-else
                      v-model:value="configValues[property.name ?? '']"
                    />
                  </a-form-item>
                </div>
                <a-empty
                  v-else-if="!configurationLoading"
                  :description="t('role.noVendorConfig')"
                />
              </a-form>
            </div>
          </a-collapse-panel>

          <a-collapse-panel key="mcp">
            <template #header>
              <div class="section-header">
                <span>MCP</span>
                <span class="section-summary">{{ mcpSummary }}</span>
              </div>
            </template>

            <div class="section-body">
              <a-alert
                v-if="!isPersistedRole"
                type="info"
                show-icon
                class="mb-4"
                :message="t('role.workspaceNeedSave')"
              />
              <McpBindingAccordionEditor
                v-model:activeKeys="activeMcpBindingKeys"
                v-model:pendingServerId="pendingMcpServerId"
                :adding="addingMcpBinding"
                :available-servers="availableMcpServers"
                :server-catalog="mcpServers"
                :bindings="mcpBindings"
                :issues="mcpBindingIssues"
                :loading="mcpLoading"
                :mode-labels="MCP_MODE_LABELS"
                @add="addMcpBinding"
                @remove="removeMcpBinding"
              />
            </div>
          </a-collapse-panel>

          <a-collapse-panel key="skills">
            <template #header>
              <div class="section-header">
                <span>Skills</span>
                <span class="section-summary">{{ skillsSummary }}</span>
              </div>
            </template>

            <div class="section-body">
              <a-alert
                v-if="!isPersistedRole"
                type="info"
                show-icon
                class="mb-4"
                :message="t('role.workspaceNeedSave')"
              />
              <SkillBindingListEditor
                v-model:pendingSkillInstallKey="pendingSkillInstallKey"
                :adding="addingSkillBinding"
                :available-skills="availableSkills"
                :bindings="skillBindings"
                :loading="skillsLoading"
                @add="addSkillBinding"
                @remove="removeSkillBinding"
              />
              <a-alert
                v-if="skillBindingsWarning"
                type="warning"
                show-icon
                class="mt-4"
                :message="skillBindingsWarning"
              />
            </div>
          </a-collapse-panel>
          </a-collapse>
        </div>
      </section>
    </div>

    <PermissionRequestModal
      :request="activePermissionRequest"
      :responding-request-id="permissionRespondingRequestId"
      @respond="respondToPermissionRequest"
    />
  </a-modal>
</template>

<style scoped>
.workspace-shell {
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(0, 0.95fr);
  gap: 16px;
  height: 74vh;
  color: hsl(var(--foreground));
}

.workspace-chat,
.workspace-config {
  min-height: 0;
}

.workspace-chat {
  overflow: hidden;
  border: 1px solid hsl(var(--border));
  border-radius: 20px;
  background: hsl(var(--card) / 0.38);
}

.workspace-config {
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: hidden;
  padding: 4px 4px 4px 0;
}

.workspace-toolbar {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 16px;
  border: 1px solid hsl(var(--border));
  border-radius: 18px;
  background: hsl(var(--card) / 0.82);
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: 2;
  backdrop-filter: blur(10px);
}

.workspace-toolbar-title {
  font-size: 15px;
  font-weight: 600;
}

.workspace-toolbar-description {
  margin-top: 4px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.6;
}

.workspace-config-scroll {
  min-height: 0;
  flex: 1;
  overflow: auto;
  padding-right: 4px;
}

.workspace-sections {
  background: transparent;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  width: 100%;
  padding-right: 12px;
}

.section-summary {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.section-body {
  padding-top: 8px;
}

.avatar-row {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-bottom: 16px;
}

.avatar-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 72px;
  height: 72px;
  overflow: hidden;
  border: 1px dashed hsl(var(--border));
  border-radius: 18px;
  background: hsl(var(--muted) / 0.45);
}

.avatar-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-fallback {
  font-size: 28px;
}

.avatar-actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.avatar-hint {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

@media (max-width: 1200px) {
  .workspace-shell {
    grid-template-columns: 1fr;
    height: auto;
  }

  .workspace-chat {
    min-height: 560px;
  }
}
</style>

<style>
.agent-role-workspace-modal .ant-modal-content,
.agent-role-workspace-modal .ant-modal-header,
.agent-role-workspace-modal .ant-modal-body {
  background: hsl(var(--background)) !important;
  color: hsl(var(--foreground)) !important;
}

.agent-role-workspace-modal .ant-modal-header {
  border-bottom-color: hsl(var(--border)) !important;
}

.agent-role-workspace-modal .ant-modal-title,
.agent-role-workspace-modal .ant-modal-close,
.agent-role-workspace-modal .ant-modal-close-x,
.agent-role-workspace-modal .workspace-shell,
.agent-role-workspace-modal .workspace-toolbar-title,
.agent-role-workspace-modal .section-header,
.agent-role-workspace-modal .ant-collapse-header,
.agent-role-workspace-modal .ant-collapse-expand-icon,
.agent-role-workspace-modal .ant-tabs-tab-btn,
.agent-role-workspace-modal .ant-form-item-label > label,
.agent-role-workspace-modal .ant-empty-description,
.agent-role-workspace-modal .ant-alert-message,
.agent-role-workspace-modal .ant-alert-description,
.agent-role-workspace-modal .ant-empty-normal,
.agent-role-workspace-modal .ant-typography {
  color: hsl(var(--foreground)) !important;
}

.agent-role-workspace-modal .workspace-toolbar-description,
.agent-role-workspace-modal .section-summary,
.agent-role-workspace-modal .ant-form-item-extra,
.agent-role-workspace-modal .ant-form-item-explain,
.agent-role-workspace-modal .ant-tabs-tab:not(.ant-tabs-tab-active) .ant-tabs-tab-btn,
.agent-role-workspace-modal .ant-select-selection-placeholder,
.agent-role-workspace-modal .ant-empty-description,
.agent-role-workspace-modal .ant-alert-description,
.agent-role-workspace-modal .ant-typography-secondary {
  color: hsl(var(--muted-foreground)) !important;
}

.agent-role-workspace-modal .ant-collapse,
.agent-role-workspace-modal .ant-collapse-item,
.agent-role-workspace-modal .ant-collapse-content {
  background: transparent !important;
  border-color: hsl(var(--border)) !important;
}

.agent-role-workspace-modal .ant-tabs-nav::before,
.agent-role-workspace-modal .ant-collapse > .ant-collapse-item,
.agent-role-workspace-modal .ant-collapse-content-box,
.agent-role-workspace-modal .ant-alert {
  border-color: hsl(var(--border)) !important;
}

.agent-role-workspace-modal .ant-input,
.agent-role-workspace-modal .ant-input-number,
.agent-role-workspace-modal .ant-input-number-input,
.agent-role-workspace-modal .ant-input-affix-wrapper,
.agent-role-workspace-modal .ant-select-selector,
.agent-role-workspace-modal textarea.ant-input {
  background: hsl(var(--card) / 0.75) !important;
  border-color: hsl(var(--border)) !important;
  color: hsl(var(--foreground)) !important;
}

.agent-role-workspace-modal .ant-input::placeholder,
.agent-role-workspace-modal .ant-input-number-input::placeholder,
.agent-role-workspace-modal textarea.ant-input::placeholder {
  color: hsl(var(--muted-foreground)) !important;
}

.agent-role-workspace-modal .ant-input-number-handler-wrap,
.agent-role-workspace-modal .ant-input-clear-icon,
.agent-role-workspace-modal .ant-select-arrow,
.agent-role-workspace-modal .ant-select-clear {
  color: hsl(var(--muted-foreground)) !important;
}

.agent-role-workspace-modal .ant-input[disabled],
.agent-role-workspace-modal .ant-input-number-disabled,
.agent-role-workspace-modal .ant-input-affix-wrapper-disabled,
.agent-role-workspace-modal .ant-select-disabled .ant-select-selector {
  background: hsl(var(--muted) / 0.4) !important;
  color: hsl(var(--muted-foreground)) !important;
}
</style>
