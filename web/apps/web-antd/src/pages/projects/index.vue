<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';
import type { ProjectDto } from '@openstaff/api';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import {
  deleteApiProjectsById,
  getApiProjects,
  getApiProviderAccounts,
  postApiProjectsByIdExport,
  postApiProjectsByIdInitialize,
  postApiProjectsByIdStart,
  postApiProjectsImport,
  unwrapCollection,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';

import { t } from '@/i18n';

import ProjectEdit from './edit.vue';

const router = useRouter();

const searchText = ref('');
const statusFilter = ref('all');
const phaseFilter = ref('all');
const editorOpen = ref(false);
const editorMode = ref<'create' | 'edit'>('create');
const activeProject = ref<null | ProjectDto>(null);
const importInputRef = ref<HTMLInputElement | null>(null);

const projectsQuery = useQuery({
  queryKey: ['projects', 'list'],
  queryFn: async () => unwrapCollection(unwrapClientEnvelope(await getApiProjects())),
});

const providerAccountsQuery = useQuery({
  queryKey: ['provider-accounts', 'projects'],
  queryFn: async () =>
    unwrapCollection(unwrapClientEnvelope(await getApiProviderAccounts())),
});

const projects = computed(() => projectsQuery.data.value ?? []);
const providers = computed(() => providerAccountsQuery.data.value ?? []);
const filteredProjects = computed(() =>
  projects.value.filter((project) => {
    const keyword = searchText.value.trim().toLowerCase();
    const matchesKeyword =
      !keyword ||
      project.name?.toLowerCase().includes(keyword) ||
      project.description?.toLowerCase().includes(keyword) ||
      project.language?.toLowerCase().includes(keyword) ||
      resolveProviderName(project.defaultProviderId).toLowerCase().includes(keyword);
    const matchesStatus =
      statusFilter.value === 'all' || (project.status ?? '') === statusFilter.value;
    const matchesPhase =
      phaseFilter.value === 'all' || (project.phase ?? '') === phaseFilter.value;

    return matchesKeyword && matchesStatus && matchesPhase;
  }),
);

const overviewItems = computed<AnalysisOverviewItem[]>(() => [
  {
    title: t('project.totalProjects'),
    icon: 'lucide:folder-kanban',
    value: projects.value.length,
    totalTitle: t('common.all'),
    totalValue: filteredProjects.value.length,
  },
  {
    title: t('project.readyProjects'),
    icon: 'lucide:rocket',
    value: projects.value.filter((project) => project.phase === 'ready_to_start').length,
    totalTitle: t('project.activeProjects'),
    totalValue: projects.value.filter((project) => project.status === 'active').length,
  },
  {
    title: t('project.runningProjects'),
    icon: 'lucide:activity',
    value: projects.value.filter((project) => project.phase === 'running').length,
    totalTitle: t('project.completedProjects'),
    totalValue: projects.value.filter((project) => project.phase === 'completed').length,
  },
  {
    title: t('project.configuredProviders'),
    icon: 'lucide:key-round',
    value: providers.value.length,
    totalTitle: t('project.languages'),
    totalValue: new Set(projects.value.map((project) => project.language ?? '')).size,
  },
]);

const projectColumns = computed(() => [
  { title: t('project.name'), dataIndex: 'name', key: 'name' },
  { title: t('project.status'), dataIndex: 'status', key: 'status', width: 130 },
  { title: t('project.phase'), dataIndex: 'phase', key: 'phase', width: 150 },
  { title: t('project.language'), dataIndex: 'language', key: 'language', width: 110 },
  { title: t('project.provider'), dataIndex: 'defaultProviderId', key: 'provider', width: 220 },
  { title: t('project.updatedAt'), dataIndex: 'updatedAt', key: 'updatedAt', width: 180 },
  { title: t('project.actions'), key: 'actions', width: 420 },
]);

const deleteProjectMutation = useMutation({
  mutationFn: async (id: string) =>
    deleteApiProjectsById({
      path: { id },
    }),
  onSuccess: async () => {
    message.success(t('project.deleteSuccess'));
    await refreshAll();
  },
});

const initializeProjectMutation = useMutation({
  mutationFn: async (id: string) =>
    unwrapClientEnvelope(
      await postApiProjectsByIdInitialize({
        path: { id },
      }),
    ),
  onSuccess: async () => {
    message.success(t('project.initializeSuccess'));
    await refreshAll();
  },
});

const startProjectMutation = useMutation({
  mutationFn: async (id: string) =>
    unwrapClientEnvelope(
      await postApiProjectsByIdStart({
        path: { id },
      }),
    ),
  onSuccess: async () => {
    message.success(t('project.startSuccess'));
    await refreshAll();
  },
});

const importProjectMutation = useMutation({
  mutationFn: async (file: File) =>
    unwrapClientEnvelope(
      await postApiProjectsImport({
        body: { file },
      }),
    ),
  onSuccess: async (project) => {
    message.success(t('project.importSuccess'));
    await refreshAll();
    if (project.id) {
      await router.push(`/projects/${project.id}`);
    }
  },
});

const exportProjectMutation = useMutation({
  mutationFn: async ({
    id,
    name,
  }: {
    id: string;
    name: string;
  }) =>
    postApiProjectsByIdExport({
      parseAs: 'blob',
      path: { id },
    }).then((result) => ({ name, ...result })),
  onSuccess: ({ data, name, response }) => {
    const fileName = resolveExportFileName(
      response.headers.get('Content-Disposition'),
      `${name || 'project'}.openstaff`,
    );
    const blob = data instanceof Blob ? data : new Blob([]);
    downloadBlob(blob, fileName);
    message.success(t('project.exportSuccess'));
  },
});

async function refreshAll() {
  await Promise.all([projectsQuery.refetch(), providerAccountsQuery.refetch()]);
}

function openCreate() {
  editorMode.value = 'create';
  activeProject.value = null;
  editorOpen.value = true;
}

function openEdit(project: ProjectDto) {
  editorMode.value = 'edit';
  activeProject.value = project;
  editorOpen.value = true;
}

function openWorkspace(project: ProjectDto) {
  if (!project.id) {
    message.error(t('project.validationProject'));
    return;
  }

  void router.push(`/projects/${project.id}`);
}

async function removeProject(project: ProjectDto) {
  if (!project.id) {
    message.error(t('project.validationProject'));
    return;
  }

  try {
    await deleteProjectMutation.mutateAsync(project.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function initializeProject(project: ProjectDto) {
  if (!project.id) {
    message.error(t('project.validationProject'));
    return;
  }

  try {
    await initializeProjectMutation.mutateAsync(project.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function startProject(project: ProjectDto) {
  if (!project.id) {
    message.error(t('project.validationProject'));
    return;
  }

  try {
    await startProjectMutation.mutateAsync(project.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function exportProject(project: ProjectDto) {
  if (!project.id) {
    message.error(t('project.validationProject'));
    return;
  }

  try {
    await exportProjectMutation.mutateAsync({
      id: project.id,
      name: project.name ?? 'project',
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

function triggerImport() {
  importInputRef.value?.click();
}

async function handleImportChange(event: Event) {
  const input = event.target as HTMLInputElement | null;
  const file = input?.files?.[0];

  if (!file) {
    return;
  }

  try {
    await importProjectMutation.mutateAsync(file);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  } finally {
    if (input) {
      input.value = '';
    }
  }
}

function handleSaved() {
  void refreshAll();
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
</script>

<template>
  <Page :title="t('project.title')" content-class="space-y-5">
    <template #extra>
      <a-space>
        <input
          ref="importInputRef"
          accept=".openstaff"
          class="hidden"
          type="file"
          @change="handleImportChange"
        />

        <a-button
          :loading="projectsQuery.isFetching.value || providerAccountsQuery.isFetching.value"
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button
          :loading="importProjectMutation.isPending.value"
          @click="triggerImport"
        >
          {{ t('project.import') }}
        </a-button>
        <a-button type="primary" @click="openCreate">
          {{ t('project.create') }}
        </a-button>
      </a-space>
    </template>

    <AnalysisOverview :items="overviewItems" />

    <section class="card-box p-4">
      <div class="flex flex-col gap-3 md:flex-row">
        <a-input
          v-model:value="searchText"
          allow-clear
          :placeholder="t('project.searchPlaceholder')"
          style="width: 280px"
        />
        <a-select
          v-model:value="statusFilter"
          :options="[
            { label: t('project.allStatus'), value: 'all' },
            { label: t('project.statusInitializing'), value: 'initializing' },
            { label: t('project.statusActive'), value: 'active' },
            { label: t('project.statusPaused'), value: 'paused' },
            { label: t('project.statusCompleted'), value: 'completed' },
            { label: t('project.statusArchived'), value: 'archived' },
          ]"
          style="width: 180px"
        />
        <a-select
          v-model:value="phaseFilter"
          :options="[
            { label: t('project.allPhases'), value: 'all' },
            { label: t('project.phaseBrainstorming'), value: 'brainstorming' },
            { label: t('project.phaseReadyToStart'), value: 'ready_to_start' },
            { label: t('project.phaseRunning'), value: 'running' },
            { label: t('project.phaseCompleted'), value: 'completed' },
          ]"
          style="width: 200px"
        />
      </div>
    </section>

    <a-alert
      v-if="projectsQuery.isError.value"
      type="error"
      show-icon
      :message="t('common.requestFailed', { status: 'projects' })"
    />

    <section class="card-box overflow-hidden">
      <div class="border-b border-border/70 px-5 py-4">
        <h3 class="text-base font-semibold">{{ t('project.title') }}</h3>
      </div>

      <a-table
        :columns="projectColumns"
        :data-source="filteredProjects"
        :loading="projectsQuery.isLoading.value"
        :pagination="{ pageSize: 8, showSizeChanger: false }"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <div v-if="column.key === 'name'" class="py-1">
            <div class="font-medium text-foreground">
              {{ record.name || t('project.unnamed') }}
            </div>
            <div class="mt-1 text-xs text-muted-foreground">
              {{ record.description || record.id }}
            </div>
          </div>

          <template v-else-if="column.key === 'status'">
            <a-tag :color="resolveStatusColor(record.status)">
              {{ t(`project.statusMap.${record.status || 'initializing'}`) }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'phase'">
            <a-tag :color="resolvePhaseColor(record.phase)">
              {{ t(`project.phaseMap.${record.phase || 'brainstorming'}`) }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'language'">
            {{ record.language || '--' }}
          </template>

          <template v-else-if="column.key === 'provider'">
            <div class="py-1">
              <div class="font-medium text-foreground">
                {{ resolveProviderName(record.defaultProviderId) }}
              </div>
              <div class="text-xs text-muted-foreground">
                {{ record.defaultModelName || '--' }}
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'updatedAt'">
            {{ formatDate(record.updatedAt) }}
          </template>

          <template v-else-if="column.key === 'actions'">
            <a-space wrap>
              <a-button size="small" type="link" @click="openWorkspace(record)">
                {{ t('project.open') }}
              </a-button>
              <a-button size="small" type="link" @click="openEdit(record)">
                {{ t('project.edit') }}
              </a-button>
              <a-button
                size="small"
                type="link"
                :loading="
                  initializeProjectMutation.isPending.value &&
                  initializeProjectMutation.variables.value === record.id
                "
                @click="initializeProject(record)"
              >
                {{ t('project.initialize') }}
              </a-button>
              <a-button
                size="small"
                type="link"
                :disabled="record.phase !== 'ready_to_start'"
                :loading="
                  startProjectMutation.isPending.value &&
                  startProjectMutation.variables.value === record.id
                "
                @click="startProject(record)"
              >
                {{ t('project.start') }}
              </a-button>
              <a-button
                size="small"
                type="link"
                :loading="
                  exportProjectMutation.isPending.value &&
                  exportProjectMutation.variables.value?.id === record.id
                "
                @click="exportProject(record)"
              >
                {{ t('project.export') }}
              </a-button>
              <a-popconfirm
                :ok-text="t('project.delete')"
                :title="t('project.deleteConfirm')"
                @confirm="removeProject(record)"
              >
                <a-button
                  danger
                  size="small"
                  type="link"
                  :loading="
                    deleteProjectMutation.isPending.value &&
                    deleteProjectMutation.variables.value === record.id
                  "
                >
                  {{ t('project.delete') }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </section>

    <ProjectEdit
      v-model:editor-open="editorOpen"
      :mode="editorMode"
      :project="activeProject"
      :providers="providers"
      @saved="handleSaved"
    />
  </Page>
</template>
