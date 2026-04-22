<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';
import type { ProjectDto } from '@openstaff/api';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { VbenIcon } from '@vben-core/shadcn-ui';
import { useQuery } from '@tanstack/vue-query';
import {
  getApiMonitorStats,
  getApiProjects,
  getApiProviderAccounts,
  getApiProviderAccountsProviders,
  unwrapCollection,
  unwrapClientEnvelope,
} from '@openstaff/api';
import dayjs from 'dayjs';
import { computed } from 'vue';
import { useRouter } from 'vue-router';

import { getModuleTitle, getSectionTitle, t } from '@/i18n';
import { navigationModules, navigationSections } from '@/navigation';

const router = useRouter();

const monitorStatsQuery = useQuery({
  queryKey: ['monitor', 'stats'],
  queryFn: async () => unwrapClientEnvelope(await getApiMonitorStats()),
});

const projectsQuery = useQuery({
  queryKey: ['projects', 'overview'],
  queryFn: async () => unwrapCollection(unwrapClientEnvelope(await getApiProjects())),
});

const providersQuery = useQuery({
  queryKey: ['provider-accounts', 'providers', 'overview'],
  queryFn: async () => unwrapClientEnvelope(await getApiProviderAccountsProviders()),
});

const providerAccountsQuery = useQuery({
  queryKey: ['provider-accounts', 'accounts', 'overview'],
  queryFn: async () =>
    unwrapCollection(unwrapClientEnvelope(await getApiProviderAccounts())),
});

const providers = computed(() => providersQuery.data.value ?? []);
const projects = computed(() => projectsQuery.data.value ?? []);
const providerAccounts = computed(() => providerAccountsQuery.data.value ?? []);
const monitorStats = computed(() => monitorStatsQuery.data.value);
const liveModulesCount = computed(
  () => navigationModules.filter((module) => module.status === 'live').length,
);
const scaffoldedModulesCount = computed(
  () => navigationModules.filter((module) => module.status === 'scaffolded').length,
);
const visibleModulesCount = computed(() =>
  navigationSections.reduce((count, section) => count + section.items.length, 0),
);
const hasDataError = computed(
  () =>
    monitorStatsQuery.isError.value ||
    projectsQuery.isError.value ||
    providersQuery.isError.value ||
    providerAccountsQuery.isError.value,
);
const isRefreshing = computed(
  () =>
    monitorStatsQuery.isFetching.value ||
    projectsQuery.isFetching.value ||
    providersQuery.isFetching.value ||
    providerAccountsQuery.isFetching.value,
);

const overviewItems = computed<AnalysisOverviewItem[]>(() => [
  {
    title: t('dashboard.projects'),
    icon: 'lucide:folder-kanban',
    value: projects.value.length,
    totalTitle: t('common.planned'),
    totalValue: scaffoldedModulesCount.value,
  },
  {
    title: t('dashboard.accounts'),
    icon: 'lucide:key-round',
    value: providerAccounts.value.length,
    totalTitle: t('dashboard.providers'),
    totalValue: providers.value.length,
  },
  {
    title: t('dashboard.sessions'),
    icon: 'lucide:messages-square',
    value: toNumber(monitorStats.value?.sessions),
    totalTitle: t('dashboard.endpointModules'),
    totalValue: toNumber(monitorStats.value?.completedTasks),
  },
  {
    title: t('dashboard.agents'),
    icon: 'lucide:bot',
    value: toNumber(monitorStats.value?.agents),
    totalTitle: t('dashboard.liveModules'),
    totalValue: liveModulesCount.value,
  },
]);

const quickNavItems = computed(() =>
  ['projects', 'provider-accounts', 'agent-roles', 'skills', 'mcp', 'settings']
    .map((key) => navigationModules.find((module) => module.key === key))
    .flatMap((module) => (module ? [module] : [])),
);

const recentSessions = computed(() => monitorStats.value?.recentSessions ?? []);
const recentProjects = computed(() =>
  [...projects.value]
    .sort((left, right) => {
      const leftValue = left.updatedAt ?? left.createdAt ?? '';
      const rightValue = right.updatedAt ?? right.createdAt ?? '';
      return rightValue.localeCompare(leftValue);
    })
    .slice(0, 6),
);

async function refreshAll() {
  await Promise.all([
    monitorStatsQuery.refetch(),
    projectsQuery.refetch(),
    providersQuery.refetch(),
    providerAccountsQuery.refetch(),
  ]);
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

function formatDate(value: string | null | undefined) {
  if (!value) {
    return '--';
  }

  return dayjs(value).format('MM-DD HH:mm');
}

function resolveProviderName(providerId: null | string | undefined) {
  if (!providerId) {
    return '--';
  }

  const provider = providerAccounts.value.find((item) => item.id === providerId);
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

function openProject(project: ProjectDto) {
  if (!project.id) {
    return;
  }

  void router.push(`/projects/${project.id}`);
}
</script>

<template>
  <Page :title="t('dashboard.title')" content-class="space-y-5">
    <template #extra>
      <a-space>
        <a-button :loading="isRefreshing" @click="refreshAll">
          {{ t('common.refresh') }}
        </a-button>
        <a-button type="primary" @click="router.push('/provider-accounts')">
          {{ t('dashboard.openProviders') }}
        </a-button>
      </a-space>
    </template>

    <a-alert
      v-if="hasDataError"
      show-icon
      type="warning"
      :message="t('common.requestFailed', { status: 'overview' })"
    />

    <AnalysisOverview :items="overviewItems" />

    <div class="grid gap-5 xl:grid-cols-[1.08fr_0.92fr]">
      <section class="card-box p-5">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-base font-semibold">{{ t('dashboard.projects') }}</h3>
          <a-button type="link" @click="router.push('/projects')">
            {{ t('project.open') }}
          </a-button>
        </div>

        <a-empty
          v-if="recentProjects.length === 0 && !projectsQuery.isLoading.value"
          :description="t('project.empty')"
        />

        <div v-else class="space-y-3">
          <button
            v-for="project in recentProjects"
            :key="project.id"
            class="w-full rounded-2xl border border-border/70 bg-background/75 px-4 py-3 text-left transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-sm"
            type="button"
            @click="openProject(project)"
          >
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <div class="truncate text-sm font-medium text-foreground">
                  {{ project.name || t('project.unnamed') }}
                </div>
                <div class="mt-1 truncate text-xs text-muted-foreground">
                  {{ project.description || project.workspacePath || project.id }}
                </div>
              </div>
              <div class="shrink-0 text-xs text-muted-foreground">
                {{ formatDate(project.updatedAt || project.createdAt) }}
              </div>
            </div>

            <div class="mt-3 flex flex-wrap gap-2">
              <a-tag :color="resolveStatusColor(project.status)">
                {{ t(`project.statusMap.${project.status || 'initializing'}`) }}
              </a-tag>
              <a-tag :color="resolvePhaseColor(project.phase)">
                {{ t(`project.phaseMap.${project.phase || 'brainstorming'}`) }}
              </a-tag>
              <span class="rounded-full bg-background px-2.5 py-1 text-xs text-muted-foreground">
                {{ resolveProviderName(project.defaultProviderId) }}
              </span>
            </div>
          </button>
        </div>
      </section>

      <section class="card-box p-5">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-base font-semibold">{{ t('dashboard.recentSessions') }}</h3>
          <span class="text-xs text-muted-foreground">
            {{ recentSessions.length }}
          </span>
        </div>

        <a-empty
          v-if="recentSessions.length === 0 && !monitorStatsQuery.isLoading.value"
          :description="t('dashboard.noSessions')"
        />

        <div v-else class="space-y-3">
          <div
            v-for="session in recentSessions.slice(0, 6)"
            :key="session.id"
            class="rounded-2xl border border-border/70 bg-background/75 px-4 py-3"
          >
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <div class="truncate text-sm font-medium text-foreground">
                  {{ session.scene || session.projectName || session.id }}
                </div>
                <div class="mt-1 truncate text-xs text-muted-foreground">
                  {{ session.projectName || '--' }}
                </div>
              </div>
              <div class="shrink-0 text-xs text-muted-foreground">
                {{ formatDate(session.createdAt) }}
              </div>
            </div>
            <div class="mt-3 flex flex-wrap gap-2 text-xs">
              <span class="rounded-full bg-background px-2.5 py-1 text-muted-foreground">
                {{ t('dashboard.recentStatus') }}: {{ session.status || '--' }}
              </span>
              <span
                class="max-w-full truncate rounded-full bg-background px-2.5 py-1 text-muted-foreground"
              >
                {{ t('dashboard.recentInput') }}: {{ session.input || '--' }}
              </span>
            </div>
          </div>
        </div>
      </section>
    </div>

    <section class="card-box p-5">
      <div class="mb-4 flex items-center justify-between">
        <h3 class="text-base font-semibold">{{ t('dashboard.quickAccess') }}</h3>
        <span class="text-xs text-muted-foreground">
          {{ quickNavItems.length }}
        </span>
      </div>

      <div class="grid grid-cols-2 gap-3 md:grid-cols-3">
        <RouterLink
          v-for="module in quickNavItems"
          :key="module.key"
          :to="module.path"
          class="group rounded-2xl border border-border/70 bg-background/75 p-4 transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-sm"
        >
          <div
            class="flex size-10 items-center justify-center rounded-xl bg-primary-50 text-primary"
          >
            <VbenIcon :icon="module.icon" class="size-5" />
          </div>
          <div class="mt-4 text-sm font-medium">
            {{ getModuleTitle(module.key, module.title) }}
          </div>
          <div class="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
            <span
              class="size-2 rounded-full"
              :class="module.status === 'live' ? 'bg-success' : 'bg-warning'"
            ></span>
            {{
              module.status === 'live' ? t('common.live') : t('common.planned')
            }}
          </div>
        </RouterLink>
      </div>
    </section>

    <section class="card-box p-5">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-base font-semibold">{{ t('dashboard.modules') }}</h3>
          <span class="text-xs text-muted-foreground">
            {{ visibleModulesCount }}
          </span>
        </div>

      <div class="grid gap-4 xl:grid-cols-3">
        <section
          v-for="section in navigationSections"
          :key="section.key"
          class="rounded-2xl border border-border/70 bg-background/75 p-4"
        >
          <div class="mb-3 flex items-center justify-between">
            <h4 class="text-sm font-semibold">
              {{ getSectionTitle(section.key, section.title) }}
            </h4>
            <span class="text-xs text-muted-foreground">
              {{ section.items.length }}
            </span>
          </div>

          <div class="space-y-2">
            <RouterLink
              v-for="module in section.items"
              :key="module.key"
              :to="module.path"
              class="flex items-center justify-between rounded-xl px-3 py-2 transition-colors hover:bg-accent/70"
            >
              <div class="flex min-w-0 items-center gap-3">
                <VbenIcon :icon="module.icon" class="size-4 text-muted-foreground" />
                <span class="truncate text-sm">
                  {{ getModuleTitle(module.key, module.title) }}
                </span>
              </div>
              <span
                class="rounded-full px-2 py-0.5 text-[11px]"
                :class="
                  module.status === 'live'
                    ? 'bg-success-100 text-success-700'
                    : 'bg-warning-100 text-warning-700'
                "
              >
                {{ module.status === 'live' ? t('common.live') : t('common.planned') }}
              </span>
            </RouterLink>
          </div>
        </section>
      </div>
    </section>
  </Page>
</template>
