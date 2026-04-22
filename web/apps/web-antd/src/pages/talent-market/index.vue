<script setup lang="ts">
import type {
  TalentMarketHirePreviewDto,
  TalentMarketMcpRequirementDto,
  TalentMarketRoleSummaryDto,
  TalentMarketSkillRequirementDto,
} from './api';

import { Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';
import { localizeJobTitle } from '@/utils/job-title';

import {
  getTalentMarketSources,
  hireTalentMarketRole,
  previewTalentMarketHire,
  searchTalentMarket,
} from './api';

const DEFAULT_SOURCE = 'sunny-dotnet-agents';

const filters = reactive({
  keyword: '',
  page: 1,
  pageSize: 12,
});

const previewOpen = ref(false);
const previewContext = ref<null | TalentMarketRoleSummaryDto>(null);
const previewData = ref<null | TalentMarketHirePreviewDto>(null);
const overwriteExisting = ref(false);

const sourcesQuery = useQuery({
  queryKey: ['talent-market', 'sources'],
  queryFn: getTalentMarketSources,
});

const catalogQuery = useQuery({
  queryKey: ['talent-market', 'search'],
  queryFn: () => searchTalentMarket({
    keyword: filters.keyword || undefined,
    page: filters.page,
    pageSize: filters.pageSize,
    sourceKey: DEFAULT_SOURCE,
  }),
});

watch(
  () => ({ ...filters }),
  () => {
    void catalogQuery.refetch();
  },
  { deep: true },
);

const previewMutation = useMutation({
  mutationFn: (item: TalentMarketRoleSummaryDto) => previewTalentMarketHire({
    sourceKey: item.sourceKey || DEFAULT_SOURCE,
    templateId: item.templateId || '',
  }),
  onSuccess: (data) => {
    previewData.value = data;
    overwriteExisting.value = false;
  },
});

const hireMutation = useMutation({
  mutationFn: async () => {
    const template = previewData.value?.template;
    if (!template?.templateId) {
      throw new Error(t('talentMarket.hireFailed'));
    }

    return hireTalentMarketRole({
      sourceKey: previewData.value?.sourceKey || template.sourceKey || DEFAULT_SOURCE,
      templateId: template.templateId,
      overwriteExisting: overwriteExisting.value,
    });
  },
  onSuccess: async () => {
    message.success(t('talentMarket.hireSuccess'));
    resetPreview();
    await catalogQuery.refetch();
  },
});

const remoteSourceLabel = computed(() =>
  sourcesQuery.data.value?.[0]?.displayName || 'Sunny-DotNet/agents',
);

const items = computed(() => catalogQuery.data.value?.items ?? []);
const totalCount = computed(() => catalogQuery.data.value?.totalCount ?? 0);
const selectedTemplate = computed(() => previewData.value?.template ?? previewContext.value);
const previewRole = computed(() => previewData.value?.preview?.role);
const previewMcps = computed(() => previewData.value?.preview?.mcps ?? []);
const previewSkills = computed(() => previewData.value?.preview?.skills ?? []);
const canHire = computed(() => {
  if (!previewData.value) {
    return false;
  }

  if (previewData.value.overwriteBlockedReason) {
    return false;
  }

  if (previewData.value.requiresOverwriteConfirmation && !overwriteExisting.value) {
    return false;
  }

  return true;
});

const confirmButtonText = computed(() =>
  overwriteExisting.value
    ? t('talentMarket.confirmOverwrite')
    : t('talentMarket.confirmHire'),
);

function handleSearch() {
  filters.page = 1;
  void catalogQuery.refetch();
}

function handlePageChange(page: number) {
  filters.page = page;
}

async function openPreview(item: TalentMarketRoleSummaryDto) {
  previewContext.value = item;
  previewData.value = null;
  overwriteExisting.value = false;
  previewOpen.value = true;

  try {
    await previewMutation.mutateAsync(item);
  } catch (error) {
    resetPreview();
    message.error(getErrorMessage(error, t('talentMarket.previewFailed')));
  }
}

async function confirmHire() {
  try {
    await hireMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('talentMarket.hireFailed')));
  }
}

function resetPreview() {
  previewOpen.value = false;
  previewContext.value = null;
  previewData.value = null;
  overwriteExisting.value = false;
}

function getAvatarFallback(item: { name?: null | string }) {
  return [...(item.name || '').trim()].slice(0, 2).join('') || 'AI';
}

function getJobDisplay(item: TalentMarketRoleSummaryDto | null | undefined) {
  return localizeJobTitle(item?.job || item?.jobTitle, item?.jobTitle) || '--';
}

function getRequirementLabel(item: TalentMarketMcpRequirementDto | TalentMarketSkillRequirementDto) {
  if ('name' in item && item.name) {
    return item.name;
  }

  if ('displayName' in item && item.displayName) {
    return item.displayName;
  }

  if (item.key) {
    return item.key;
  }

  if ('skillId' in item && item.skillId) {
    return item.skillId;
  }

  return '--';
}

function getRequirementDescription(item: TalentMarketMcpRequirementDto | TalentMarketSkillRequirementDto) {
  if ('matchedServerName' in item) {
    return item.matchedServerName || item.message || '--';
  }

  if ('installKey' in item) {
    return item.installKey || item.message || '--';
  }

  return item.message || '--';
}

function getRequirementColor(status?: string) {
  return status === 'resolved' ? 'green' : 'default';
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <Page :title="t('talentMarket.title')" content-class="space-y-5">
    <template #extra>
      <a-space>
        <a-button
          :loading="catalogQuery.isFetching.value || sourcesQuery.isFetching.value"
          @click="catalogQuery.refetch()"
        >
          {{ t('common.refresh') }}
        </a-button>
      </a-space>
    </template>

    <section class="card-box p-4">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <a-input-search
          v-model:value="filters.keyword"
          allow-clear
          enter-button
          :placeholder="t('talentMarket.searchPlaceholder')"
          style="width: 320px"
          @search="handleSearch"
        />
        <div class="text-sm text-muted-foreground">
          {{ t('talentMarket.remoteSource') }}: {{ remoteSourceLabel }}
        </div>
      </div>
    </section>

    <a-alert
      v-if="catalogQuery.isError.value"
      type="error"
      show-icon
      :message="t('talentMarket.loadFailed')"
    />

    <section class="space-y-4">
      <a-row :gutter="[16, 16]">
        <a-col
          v-for="item in items"
          :key="item.templateId"
          :lg="8"
          :md="12"
          :xs="24"
        >
          <a-card class="h-full">
            <template #extra>
              <a-tag :color="item.isHired ? 'blue' : 'default'">
                {{ item.isHired ? t('talentMarket.hired') : t('talentMarket.remote') }}
              </a-tag>
            </template>

            <div class="flex items-center gap-3">
              <a-avatar :size="48" :src="item.avatar || undefined">
                {{ getAvatarFallback(item) }}
              </a-avatar>
              <div class="min-w-0">
                <div class="truncate text-base font-semibold">
                  {{ item.name || '--' }}
                </div>
                <div class="text-sm text-muted-foreground">
                  {{ getJobDisplay(item) }}
                </div>
              </div>
            </div>

            <div class="mt-3 min-h-16 text-sm text-muted-foreground">
              {{ item.description || t('talentMarket.emptyDescription') }}
            </div>

            <div class="mt-3 flex flex-wrap gap-2">
              <a-tag>{{ t('talentMarket.modelLabel', { value: item.modelName || '--' }) }}</a-tag>
              <a-tag>{{ t('talentMarket.mcpCountLabel', { value: item.mcpCount ?? 0 }) }}</a-tag>
              <a-tag>{{ t('talentMarket.skillCountLabel', { value: item.skillCount ?? 0 }) }}</a-tag>
            </div>

            <div class="mt-4 flex items-center justify-between gap-3">
              <div class="min-w-0 text-xs text-muted-foreground">
                <template v-if="item.matchedRoleName">
                  {{ t('talentMarket.matchedRole', { name: item.matchedRoleName }) }}
                </template>
              </div>
              <a-button
                type="primary"
                :loading="previewMutation.isPending.value && previewContext?.templateId === item.templateId"
                @click="openPreview(item)"
              >
                {{ item.isHired ? t('talentMarket.rehire') : t('talentMarket.hire') }}
              </a-button>
            </div>
          </a-card>
        </a-col>
      </a-row>

      <div class="flex justify-end">
        <a-pagination
          :current="filters.page"
          :page-size="filters.pageSize"
          :total="totalCount"
          :show-size-changer="false"
          @change="handlePageChange"
        />
      </div>
    </section>

    <a-drawer
      v-model:open="previewOpen"
      :destroy-on-close="true"
      :title="t('talentMarket.previewTitle')"
      width="680"
      @close="resetPreview"
    >
      <a-spin :spinning="previewMutation.isPending.value && !previewData">
        <template v-if="selectedTemplate">
          <div class="space-y-4">
            <a-alert
              v-if="previewData?.overwriteBlockedReason"
              type="error"
              show-icon
              :message="previewData.overwriteBlockedReason"
            />
            <a-alert
              v-else-if="previewData?.matchedRoleName && previewData.requiresOverwriteConfirmation"
              type="warning"
              show-icon
              :message="t('talentMarket.overwriteWarning', { name: previewData.matchedRoleName })"
            />

            <a-checkbox
              v-if="previewData?.requiresOverwriteConfirmation"
              v-model:checked="overwriteExisting"
            >
              {{ t('talentMarket.overwriteExisting') }}
            </a-checkbox>

            <section class="rounded-lg border border-border/70 p-4">
              <div class="flex items-center gap-3">
                <a-avatar :size="56" :src="previewRole?.avatar || undefined">
                  {{ getAvatarFallback(previewRole || selectedTemplate) }}
                </a-avatar>
                <div class="min-w-0">
                  <div class="truncate text-lg font-semibold">
                    {{ previewRole?.name || selectedTemplate.name || '--' }}
                  </div>
                  <div class="text-sm text-muted-foreground">
                    {{ localizeJobTitle(previewRole?.jobTitle) || getJobDisplay(selectedTemplate) }}
                  </div>
                </div>
              </div>

              <div class="mt-3 grid gap-2 text-sm md:grid-cols-2">
                <div>
                  <span class="text-muted-foreground">{{ t('talentMarket.model') }}:</span>
                  {{ previewRole?.modelName || selectedTemplate.modelName || '--' }}
                </div>
                <div>
                  <span class="text-muted-foreground">{{ t('talentMarket.status') }}:</span>
                  {{ selectedTemplate.isHired ? t('talentMarket.hired') : t('talentMarket.remote') }}
                </div>
              </div>

              <div class="mt-3 text-sm text-muted-foreground">
                {{ previewRole?.description || selectedTemplate.description || t('talentMarket.emptyDescription') }}
              </div>
            </section>

            <section class="rounded-lg border border-border/70 p-4">
              <div class="mb-3 text-sm font-medium">
                {{ t('talentMarket.mcpRequirements') }}
              </div>
              <a-list :data-source="previewMcps" size="small">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
                      <div class="min-w-0">
                        <div class="truncate font-medium">
                          {{ getRequirementLabel(item) }}
                        </div>
                        <div class="truncate text-xs text-muted-foreground">
                          {{ getRequirementDescription(item) }}
                        </div>
                      </div>
                      <a-tag :color="getRequirementColor(item.status)">
                        {{ item.status || '--' }}
                      </a-tag>
                    </div>
                  </a-list-item>
                </template>
              </a-list>
            </section>

            <section class="rounded-lg border border-border/70 p-4">
              <div class="mb-3 text-sm font-medium">
                {{ t('talentMarket.skillRequirements') }}
              </div>
              <a-list :data-source="previewSkills" size="small">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
                      <div class="min-w-0">
                        <div class="truncate font-medium">
                          {{ getRequirementLabel(item) }}
                        </div>
                        <div class="truncate text-xs text-muted-foreground">
                          {{ getRequirementDescription(item) }}
                        </div>
                      </div>
                      <a-tag :color="getRequirementColor(item.status)">
                        {{ item.status || '--' }}
                      </a-tag>
                    </div>
                  </a-list-item>
                </template>
              </a-list>
            </section>
          </div>
        </template>
      </a-spin>

      <template #footer>
        <div class="flex justify-end gap-2">
          <a-button @click="resetPreview">
            {{ t('common.cancel') }}
          </a-button>
          <a-button
            type="primary"
            :disabled="!canHire"
            :loading="hireMutation.isPending.value"
            @click="confirmHire"
          >
            {{ confirmButtonText }}
          </a-button>
        </div>
      </template>
    </a-drawer>
  </Page>
</template>
