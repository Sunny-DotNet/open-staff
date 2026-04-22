<script setup lang="ts">
import type {
  ProjectAgentDto,
  ProjectDto,
} from '@openstaff/api';

import {
  getApiProjects,
  getApiProjectsByProjectIdAgents,
  unwrapCollection,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, ref, watch } from 'vue';

import { t } from '@/i18n';

import type { McpServerView } from './api';
import ParameterEditor from './parameter-editor.vue';
import type { McpParameterValues } from './structured-values';
import {
  buildDefaultParameterValues,
  mergeParameterValues,
  parseParameterValues,
  resolveSelectedProfileId,
  stringifyParameterValues,
} from './structured-values';

import {
  createMcpBindingDraft,
  getProjectAgentBindings,
  replaceProjectAgentBindings,
} from './api';

type EditableBinding = {
  isEnabled: boolean;
  key: string;
  mcpServerId: string;
  parameterValues: McpParameterValues;
  selectedProfileId: string;
  toolFilter: string;
};

type ProjectAgentBindingView = {
  id?: string;
  mcpServerId?: string;
  toolFilter?: null | string;
  selectedProfileId?: null | string;
  parameterValues?: null | string;
  isEnabled?: boolean;
};

const props = defineProps<{
  servers: McpServerView[];
}>();

const selectedProjectId = ref('');
const selectedProjectAgentId = ref('');
const bindings = ref<EditableBinding[]>([]);
const loading = ref(false);

const projectsQuery = useQuery({
  queryKey: ['projects', 'mcp-bindings'],
  queryFn: async () => unwrapCollection(unwrapClientEnvelope(await getApiProjects())),
});

const projectAgentsQuery = useQuery({
  queryKey: ['projects', 'agents', 'mcp-bindings'],
  enabled: computed(() => !!selectedProjectId.value),
  queryFn: async () =>
    unwrapCollection(
      unwrapClientEnvelope(
        await getApiProjectsByProjectIdAgents({
          path: { projectId: selectedProjectId.value },
        }),
      ),
    ),
});

const saveMutation = useMutation({
  mutationFn: async ({
    projectAgentId,
    rows,
  }: {
    projectAgentId: string;
    rows: EditableBinding[];
  }) =>
      replaceProjectAgentBindings(
        projectAgentId,
        rows.map((row) => ({
          mcpServerId: row.mcpServerId,
          selectedProfileId: row.selectedProfileId || null,
          parameterValues: stringifyParameterValues(row.parameterValues),
          toolFilter: row.toolFilter || null,
          isEnabled: row.isEnabled,
        })),
    ),
  onSuccess: () => {
    message.success(t('mcp.bindingsSaveSuccess'));
  },
});

const projectOptions = computed(() =>
  (projectsQuery.data.value ?? []).map((project: ProjectDto) => ({
    label: project.name || project.id || '--',
    value: project.id || '',
  })),
);

const agentOptions = computed(() =>
  (projectAgentsQuery.data.value ?? []).map((agent: ProjectAgentDto) => ({
    label: agent.roleName || agent.id || '--',
    value: agent.id || '',
  })),
);

const serverOptions = computed(() =>
  props.servers
    .filter((server) => server.id)
    .map((server) => ({
      label: server.name || '--',
      value: server.id || '',
    })),
);

const serverById = computed(() =>
  new Map(
    props.servers
      .filter((server) => server.id)
      .map((server) => [server.id as string, server]),
  ),
);

watch(
  () => projectOptions.value,
  (options) => {
    if (!selectedProjectId.value && options.length > 0) {
      selectedProjectId.value = options[0]?.value ?? '';
    }
  },
  { immediate: true },
);

watch(
  () => selectedProjectId.value,
  (projectId) => {
    if (!projectId) {
      selectedProjectAgentId.value = '';
      bindings.value = [];
      return;
    }

    void projectAgentsQuery.refetch();
  },
);

watch(
  () => agentOptions.value,
  (options) => {
    if (options.length === 0) {
      selectedProjectAgentId.value = '';
      bindings.value = [];
      return;
    }

    if (!selectedProjectAgentId.value || !options.some((option) => option.value === selectedProjectAgentId.value)) {
      selectedProjectAgentId.value = options[0]?.value ?? '';
    }
  },
  { immediate: true },
);

watch(
  () => selectedProjectAgentId.value,
  (agentId) => {
    if (!agentId) {
      bindings.value = [];
      return;
    }

    void loadBindings(agentId);
  },
  { immediate: true },
);

async function loadBindings(projectAgentId: string) {
  loading.value = true;

  try {
    const rows = await getProjectAgentBindings(projectAgentId);
    bindings.value = (rows as ProjectAgentBindingView[]).map((binding) => {
      const server = getServer(binding.mcpServerId || '');
      const selectedProfileId = resolveSelectedProfileId(server, binding.selectedProfileId);
      return {
        key: binding.id || crypto.randomUUID(),
        mcpServerId: binding.mcpServerId || '',
        toolFilter: binding.toolFilter || '',
        selectedProfileId,
        parameterValues: mergeParameterValues(
          buildDefaultParameterValues(server?.parameterSchema, selectedProfileId),
          parseParameterValues(binding.parameterValues),
        ),
        isEnabled: binding.isEnabled ?? true,
      };
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.loadBindingsFailed')));
    bindings.value = [];
  } finally {
    loading.value = false;
  }
}

function addBinding() {
  bindings.value = [
    ...bindings.value,
      {
        key: crypto.randomUUID(),
        mcpServerId: '',
        toolFilter: '',
        selectedProfileId: '',
        parameterValues: {},
        isEnabled: true,
      },
    ];
}

function removeBinding(key: string) {
  bindings.value = bindings.value.filter((binding) => binding.key !== key);
}

async function applyDraft(row: EditableBinding) {
  if (!selectedProjectAgentId.value || !row.mcpServerId) {
    message.warning(t('mcp.bindingDraftMissingServer'));
    return;
  }

  try {
    const draft = await createMcpBindingDraft({
      mcpServerId: row.mcpServerId,
      scope: 'project-agent',
      projectAgentRoleId: selectedProjectAgentId.value,
    });
    const server = getServer(row.mcpServerId);
    row.selectedProfileId = resolveSelectedProfileId(server, draft.selectedProfileId);
    row.parameterValues = mergeParameterValues(
      buildDefaultParameterValues(server?.parameterSchema, row.selectedProfileId),
      parseParameterValues(draft.parameterValues),
    );
    row.toolFilter = draft.toolFilter || '';
    row.isEnabled = draft.isEnabled ?? true;
    message.success(t('mcp.bindingDraftApplied'));
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.bindingDraftFailed')));
  }
}

async function saveBindings() {
  if (!selectedProjectAgentId.value) {
    message.warning(t('mcp.bindingSelectProjectAgent'));
    return;
  }

  if (bindings.value.some((binding) => !binding.mcpServerId)) {
    message.warning(t('mcp.bindingServerRequired'));
    return;
  }

  try {
    await saveMutation.mutateAsync({
      projectAgentId: selectedProjectAgentId.value,
      rows: bindings.value,
    });
    await loadBindings(selectedProjectAgentId.value);
  } catch (error) {
    message.error(getErrorMessage(error, t('mcp.bindingsSaveFailed')));
  }
}

function getErrorMessage(errorValue: unknown, fallback: string) {
  if (errorValue instanceof Error && errorValue.message) {
    return errorValue.message;
  }

  return fallback;
}

function getServer(serverId: string) {
  return serverById.value.get(serverId);
}

function handleServerChange(row: EditableBinding, serverId: string) {
  row.mcpServerId = serverId;
  const server = getServer(serverId);
  row.selectedProfileId = resolveSelectedProfileId(server);
  row.parameterValues = buildDefaultParameterValues(server?.parameterSchema, row.selectedProfileId);
}
</script>

<template>
  <section class="card-box p-4">
    <div class="mb-4 flex flex-col gap-3">
      <div>
        <h3 class="text-base font-semibold">{{ t('mcp.projectBindingsTitle') }}</h3>
        <p class="mt-1 text-xs text-muted-foreground">
          {{ t('mcp.projectBindingsDescription') }}
        </p>
      </div>

      <div class="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
        <a-space wrap>
          <a-select
            v-model:value="selectedProjectId"
            :options="projectOptions"
            style="width: 240px"
          />
          <a-select
            v-model:value="selectedProjectAgentId"
            :options="agentOptions"
            style="width: 260px"
          />
        </a-space>

        <a-space wrap>
          <a-button :loading="loading" @click="addBinding">
            {{ t('mcp.addBinding') }}
          </a-button>
          <a-button
            type="primary"
            :loading="saveMutation.isPending.value"
            @click="saveBindings"
          >
            {{ t('mcp.saveBindings') }}
          </a-button>
        </a-space>
      </div>
    </div>

    <a-empty
      v-if="!selectedProjectAgentId"
      :description="t('mcp.bindingSelectProjectAgent')"
    />

    <div
      v-else
      class="space-y-3"
    >
      <a-empty
        v-if="!loading && bindings.length === 0"
        :description="t('mcp.noBindings')"
      />

      <div
        v-for="row in bindings"
        :key="row.key"
        class="rounded-2xl border border-border/70 bg-background/75 p-4"
      >
        <div class="grid gap-3 lg:grid-cols-2">
          <a-select
            v-model:value="row.mcpServerId"
            :options="serverOptions"
            :placeholder="t('mcp.bindingServerPlaceholder')"
            @update:value="(value: string) => handleServerChange(row, value)"
          />
          <div class="flex items-center justify-between rounded-xl border border-border/60 px-3 py-2">
            <span class="text-sm text-muted-foreground">{{ t('mcp.enabledState') }}</span>
            <a-switch v-model:checked="row.isEnabled" />
          </div>
        </div>

        <div class="mt-3 grid gap-3 lg:grid-cols-2">
          <a-select
            v-model:value="row.selectedProfileId"
            :options="(getServer(row.mcpServerId)?.profiles ?? []).map((profile) => ({
              label: profile.displayName || profile.id || '--',
              value: profile.id || '',
            }))"
            :placeholder="t('mcp.defaultProfile')"
          />
          <a-textarea
            v-model:value="row.toolFilter"
            :auto-size="{ minRows: 4, maxRows: 8 }"
            :placeholder="t('mcp.toolFilterPlaceholder')"
          />
        </div>

        <div
          v-if="getServer(row.mcpServerId)?.parameterSchema?.length"
          class="mt-3 rounded-xl border border-border/60 bg-muted/30 px-3 py-2 text-xs text-muted-foreground"
        >
          <div class="font-medium text-foreground">
            {{ t('mcp.parameterSchemaTitle') }}
          </div>
          <ParameterEditor
            v-model="row.parameterValues"
            :schema="getServer(row.mcpServerId)?.parameterSchema ?? []"
            :selected-profile-id="row.selectedProfileId"
          />
        </div>

        <div class="mt-3 flex justify-end gap-2">
          <a-button @click="applyDraft(row)">
            {{ t('mcp.applyDraft') }}
          </a-button>
          <a-button danger @click="removeBinding(row.key)">
            {{ t('mcp.removeBinding') }}
          </a-button>
        </div>
      </div>
    </div>
  </section>
</template>
