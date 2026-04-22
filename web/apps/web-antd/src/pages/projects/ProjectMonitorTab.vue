<script setup lang="ts">
import type { EventDto, ProjectStatsDto } from '@openstaff/api';

import {
  getApiMonitorProjectsByProjectIdEvents,
  getApiMonitorProjectsByProjectIdStats,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useQuery } from '@tanstack/vue-query';
import dayjs from 'dayjs';
import { computed, ref, watch } from 'vue';

import { t } from '@/i18n';

const props = defineProps<{
  projectId: string;
}>();

const currentPage = ref(1);
const pageSize = ref(20);
const selectedEventType = ref<string>();
const selectedScene = ref<string>();

const projectStatsQuery = useQuery({
  queryKey: ['projects', 'monitor-stats', computed(() => props.projectId)],
  enabled: computed(() => !!props.projectId),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiMonitorProjectsByProjectIdStats({
        path: { projectId: props.projectId },
      }),
    ),
});

const eventsQuery = useQuery({
  queryKey: [
    'projects',
    'monitor-events',
    computed(() => props.projectId),
    currentPage,
    pageSize,
    selectedEventType,
    selectedScene,
  ],
  enabled: computed(() => !!props.projectId),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiMonitorProjectsByProjectIdEvents({
        path: { projectId: props.projectId },
        query: {
          eventType: selectedEventType.value,
          page: currentPage.value,
          pageSize: pageSize.value,
          scene: selectedScene.value,
        },
      }),
    ),
});

const stats = computed<ProjectStatsDto | null>(() => projectStatsQuery.data.value ?? null);
const events = computed<EventDto[]>(() => eventsQuery.data.value?.items ?? []);
const totalEvents = computed(() => Number(eventsQuery.data.value?.total ?? 0));

const eventTypeOptions = computed(() =>
  Object.keys(stats.value?.eventsByType ?? {})
    .sort()
    .map((value) => ({
      label: value,
      value,
    })),
);

const sceneOptions = computed(() =>
  (stats.value?.sceneBreakdown ?? []).map((item) => ({
    label: item.scene ?? '--',
    value: item.scene ?? '',
  })),
);

const eventColumns = computed(() => [
  { dataIndex: 'createdAt', key: 'createdAt', title: t('project.time'), width: 180 },
  { dataIndex: 'scene', key: 'scene', title: t('project.scene'), width: 140 },
  { dataIndex: 'eventType', key: 'eventType', title: t('project.eventType'), width: 160 },
  { dataIndex: 'agentName', key: 'agentName', title: t('project.agent'), width: 180 },
  { dataIndex: 'status', key: 'status', title: t('project.status'), width: 120 },
  { dataIndex: 'detail', key: 'detail', title: t('project.detail') },
]);

watch(
  () => [selectedEventType.value, selectedScene.value] as const,
  () => {
    currentPage.value = 1;
  },
);

async function refreshMonitor() {
  await Promise.all([
    projectStatsQuery.refetch(),
    eventsQuery.refetch(),
  ]);
}

function toNumber(value: null | number | string | undefined) {
  if (typeof value === 'number') {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? 0 : parsed;
  }

  return 0;
}

function formatDate(value: null | string | undefined) {
  if (!value) {
    return '--';
  }

  return dayjs(value).format('YYYY-MM-DD HH:mm:ss');
}

function formatDuration(value: null | number | string | undefined) {
  const duration = toNumber(value);
  if (!duration) {
    return '--';
  }

  if (duration < 1000) {
    return `${duration} ms`;
  }

  return `${(duration / 1000).toFixed(1)} s`;
}
</script>

<template>
  <div class="space-y-5">
    <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="text-xs text-muted-foreground">{{ t('project.assignedAgents') }}</div>
        <div class="mt-2 text-2xl font-semibold">{{ stats?.agents?.length ?? 0 }}</div>
      </section>
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="text-xs text-muted-foreground">{{ t('project.eventTypes') }}</div>
        <div class="mt-2 text-2xl font-semibold">
          {{ Object.keys(stats?.eventsByType ?? {}).length }}
        </div>
      </section>
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="text-xs text-muted-foreground">{{ t('project.totalEventsLabel') }}</div>
        <div class="mt-2 text-2xl font-semibold">
          {{
            Object.values(stats?.eventsByType ?? {}).reduce(
              (sum: number, count) => sum + toNumber(count),
              0,
            )
          }}
        </div>
      </section>
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="text-xs text-muted-foreground">{{ t('project.checkpoints') }}</div>
        <div class="mt-2 text-2xl font-semibold">{{ toNumber(stats?.checkpoints) }}</div>
      </section>
    </div>

    <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <h3 class="text-base font-semibold">{{ t('project.sceneBreakdownTitle') }}</h3>
        <a-button
          :loading="projectStatsQuery.isFetching.value || eventsQuery.isFetching.value"
          @click="refreshMonitor"
        >
          {{ t('common.refresh') }}
        </a-button>
      </div>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div
          v-for="scene in stats?.sceneBreakdown ?? []"
          :key="scene.scene"
          class="rounded-2xl border border-border/70 bg-background p-4"
        >
          <div class="text-sm font-medium text-foreground">{{ scene.scene || '--' }}</div>
          <div class="mt-2 space-y-1 text-xs text-muted-foreground">
            <div>{{ t('project.sessionsCount') }}: {{ toNumber(scene.sessionCount) }}</div>
            <div>{{ t('project.tasks') }}: {{ toNumber(scene.taskCount) }}</div>
            <div>{{ t('project.eventTypes') }}: {{ toNumber(scene.eventCount) }}</div>
            <div>{{ t('project.averageDuration') }}: {{ formatDuration(scene.averageDurationMs) }}</div>
          </div>
        </div>
      </div>
    </section>

    <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center">
        <h3 class="text-base font-semibold">{{ t('project.eventsTableTitle') }}</h3>
        <div class="flex flex-1 flex-wrap gap-3 lg:justify-end">
          <a-select
            v-model:value="selectedScene"
            allow-clear
            style="width: 180px"
            :options="sceneOptions"
            :placeholder="t('project.scenePlaceholder')"
          />
          <a-select
            v-model:value="selectedEventType"
            allow-clear
            style="width: 220px"
            :options="eventTypeOptions"
            :placeholder="t('project.eventTypePlaceholder')"
          />
        </div>
      </div>

      <a-table
        :columns="eventColumns"
        :data-source="events"
        :loading="eventsQuery.isLoading.value"
        :pagination="{
          current: currentPage,
          pageSize,
          total: totalEvents,
          showSizeChanger: true,
          onChange: (page: number, size: number) => {
            currentPage = page;
            pageSize = size;
          },
        }"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'detail'">
            <div class="max-w-[520px] whitespace-pre-wrap break-words text-xs leading-6 text-muted-foreground">
              {{ record.detail || record.content || record.data || '--' }}
            </div>
          </template>
        </template>
      </a-table>
    </section>
  </div>
</template>
