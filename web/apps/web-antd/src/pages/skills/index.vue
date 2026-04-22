<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message, Modal } from 'ant-design-vue';
import dayjs from 'dayjs';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

import type {
  InstallSkillInput,
  InstalledSkillDto,
  SkillCatalogItemDto,
} from './api';
import {
  getInstalledSkills,
  getSkillCatalogItem,
  getSkillSources,
  installSkill,
  searchSkillCatalog,
  uninstallSkill,
} from './api';

const catalogFilters = reactive({
  owner: '',
  page: 1,
  pageSize: 12,
  query: '',
  repo: '',
});

const installedSearch = ref('');
const detailOpen = ref(false);
const selectedSkill = ref<null | {
  owner: string;
  repo: string;
  skillId: string;
}>(null);

const sourcesQuery = useQuery({
  queryKey: ['skills', 'sources'],
  queryFn: getSkillSources,
});

const catalogQuery = useQuery({
  queryKey: ['skills', 'catalog'],
  queryFn: () => searchSkillCatalog({
    owner: catalogFilters.owner || undefined,
    page: catalogFilters.page,
    pageSize: catalogFilters.pageSize,
    query: catalogFilters.query || undefined,
    repo: catalogFilters.repo || undefined,
  }),
});

const installedQuery = useQuery({
  queryKey: ['skills', 'installed'],
  queryFn: () => getInstalledSkills(installedSearch.value || undefined),
});

const detailQuery = useQuery({
  queryKey: ['skills', 'detail', selectedSkill],
  enabled: computed(() => !!selectedSkill.value),
  queryFn: () => {
    if (!selectedSkill.value) {
      throw new Error('Missing selected skill.');
    }

    return getSkillCatalogItem(
      selectedSkill.value.owner,
      selectedSkill.value.repo,
      selectedSkill.value.skillId,
    );
  },
});

watch(
  () => ({ ...catalogFilters }),
  () => {
    void catalogQuery.refetch();
  },
  { deep: true },
);

watch(installedSearch, () => {
  void installedQuery.refetch();
});

const installedByIdentity = computed(() => {
  const map = new Map<string, InstalledSkillDto>();
  for (const item of installedQuery.data.value ?? []) {
    const identity = buildIdentity(item.owner, item.repo, item.skillId);
    if (identity) {
      map.set(identity, item);
    }
  }
  return map;
});

const selectedInstalledItem = computed(() => {
  if (!selectedSkill.value) {
    return null;
  }

  return installedByIdentity.value.get(
    buildIdentity(
      selectedSkill.value.owner,
      selectedSkill.value.repo,
      selectedSkill.value.skillId,
    ) ?? '',
  ) ?? null;
});

const detailView = computed(() => {
  const detail = detailQuery.data.value;
  if (detail) {
    return detail;
  }

  const installed = selectedInstalledItem.value;
  if (!installed) {
    return null;
  }

  return {
    description: installed.description,
    displayName: installed.displayName,
    githubUrl: installed.githubUrl,
    isInstalled: true,
    name: installed.name,
    owner: installed.owner,
    repo: installed.repo,
    skillId: installed.skillId,
    source: installed.source,
    sourceKey: installed.sourceKey,
  } satisfies Partial<SkillCatalogItemDto>;
});

const overviewItems = computed<AnalysisOverviewItem[]>(() => [
  {
    title: t('skills.catalogEntries'),
    icon: 'lucide:sparkles',
    value: catalogQuery.data.value?.total ?? 0,
    totalTitle: t('skills.currentPage'),
    totalValue: catalogQuery.data.value?.items?.length ?? 0,
  },
  {
    title: t('skills.installedSkills'),
    icon: 'lucide:folder-down',
    value: installedQuery.data.value?.length ?? 0,
    totalTitle: t('common.all'),
    totalValue: installedQuery.data.value?.length ?? 0,
  },
  {
    title: t('skills.catalogSources'),
    icon: 'lucide:database-zap',
    value: sourcesQuery.data.value?.length ?? 0,
    totalTitle: t('skills.sourceName'),
    totalValue: sourcesQuery.data.value?.length ?? 0,
  },
]);

const installMutation = useMutation({
  mutationFn: (payload: InstallSkillInput) => installSkill(payload),
  onSuccess: async () => {
    message.success(t('skills.installSuccess'));
    await refreshAll();
  },
});

const uninstallMutation = useMutation({
  mutationFn: uninstallSkill,
  onSuccess: async () => {
    message.success(t('skills.uninstallSuccess'));
    await refreshAll();
  },
});

async function refreshAll() {
  await Promise.all([
    catalogQuery.refetch(),
    installedQuery.refetch(),
    detailOpen.value ? detailQuery.refetch() : Promise.resolve(),
  ]);
}

async function handleInstall(item: Partial<SkillCatalogItemDto>) {
  const owner = item.owner?.trim();
  const repo = item.repo?.trim();
  const skillId = item.skillId?.trim();

  if (!owner || !repo || !skillId) {
    message.error(t('skills.installFailed'));
    return;
  }

  try {
    await installMutation.mutateAsync({
      owner,
      overwriteExisting: true,
      repo,
      skillId,
      sourceKey: item.sourceKey || 'skills.sh',
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('skills.installFailed')));
  }
}

function confirmUninstall(item: InstalledSkillDto) {
  Modal.confirm({
    title: t('skills.uninstallConfirm'),
    content: `${item.displayName || item.skillId || '--'}`,
    async onOk() {
      if (!item.owner || !item.repo || !item.skillId) {
        message.error(t('skills.uninstallFailed'));
        return;
      }

      try {
        await uninstallMutation.mutateAsync({
          owner: item.owner,
          repo: item.repo,
          skillId: item.skillId,
        });
      } catch (error) {
        message.error(getErrorMessage(error, t('skills.uninstallFailed')));
      }
    },
  });
}

function openSkillDetail(item: Partial<SkillCatalogItemDto> | InstalledSkillDto) {
  if (!item.owner || !item.repo || !item.skillId) {
    message.error(t('skills.loadDetailFailed'));
    return;
  }

  selectedSkill.value = {
    owner: item.owner,
    repo: item.repo,
    skillId: item.skillId,
  };
  detailOpen.value = true;
  void detailQuery.refetch();
}

function openRepository(url: null | string | undefined) {
  if (url) {
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}

function buildIdentity(owner?: null | string, repo?: null | string, skillId?: null | string) {
  if (!owner || !repo || !skillId) {
    return null;
  }

  return `${owner.toLowerCase()}::${repo.toLowerCase()}::${skillId.toLowerCase()}`;
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

function formatDate(value?: null | string) {
  return value ? dayjs(value).format('YYYY-MM-DD HH:mm') : '--';
}
</script>

<template>
  <Page auto-content-height>
    <div class="space-y-4 p-1">
      <AnalysisOverview :items="overviewItems" />

      <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div class="text-base font-semibold">
              {{ t('skills.catalogTitle') }}
            </div>
            <div class="mt-1 text-sm text-muted-foreground">
              {{ t('skills.catalogDescription') }}
            </div>
          </div>
          <a-space wrap>
            <a-input
              v-model:value="catalogFilters.query"
              allow-clear
              :placeholder="t('skills.searchPlaceholder')"
              style="width: 220px"
            />
            <a-input
              v-model:value="catalogFilters.owner"
              allow-clear
              :placeholder="t('skills.ownerPlaceholder')"
              style="width: 180px"
            />
            <a-input
              v-model:value="catalogFilters.repo"
              allow-clear
              :placeholder="t('skills.repoPlaceholder')"
              style="width: 180px"
            />
          </a-space>
        </div>

        <div class="mt-3 flex flex-wrap gap-2">
          <a-tag
            v-for="source in sourcesQuery.data.value ?? []"
            :key="source.sourceKey || source.source"
            color="blue"
          >
            {{ source.displayName || source.sourceKey || source.source || '--' }}
          </a-tag>
        </div>
      </div>

      <div class="grid gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
          <div class="text-sm font-semibold">
            {{ t('skills.catalogEntries') }}
          </div>

          <div
            v-if="catalogQuery.data.value?.items?.length"
            class="mt-4 grid gap-3 md:grid-cols-2"
          >
            <div
              v-for="item in catalogQuery.data.value?.items ?? []"
              :key="`${item.owner}-${item.repo}-${item.skillId}`"
              class="rounded-2xl border border-border/60 bg-background/90 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <div class="text-sm font-semibold">
                    {{ item.displayName || item.name || '--' }}
                  </div>
                  <div class="mt-1 text-xs text-muted-foreground">
                    {{ item.owner }}/{{ item.repo }} · {{ item.skillId }}
                  </div>
                </div>
                <a-tag :color="item.isInstalled ? 'success' : 'default'">
                  {{ item.isInstalled ? t('skills.installedTag') : t('skills.notInstalledTag') }}
                </a-tag>
              </div>

              <p class="mt-3 line-clamp-3 min-h-[60px] text-sm text-muted-foreground">
                {{ item.description || t('skills.noDescription') }}
              </p>

              <div class="mt-3 text-xs text-muted-foreground">
                {{ t('skills.installs') }}: {{ item.installs ?? 0 }}
              </div>

              <div class="mt-4 flex flex-wrap gap-2">
                <a-button size="small" @click="openSkillDetail(item)">
                  {{ t('skills.viewDetails') }}
                </a-button>
                <a-button
                  size="small"
                  type="primary"
                  :disabled="!!item.isInstalled"
                  @click="handleInstall(item)"
                >
                  {{ t('skills.install') }}
                </a-button>
              </div>
            </div>
          </div>

          <a-empty v-else class="py-10" :description="t('skills.catalogEmpty')" />

          <div class="mt-4 flex justify-end">
            <a-pagination
              :current="catalogQuery.data.value?.page ?? catalogFilters.page"
              :page-size="catalogQuery.data.value?.pageSize ?? catalogFilters.pageSize"
              :show-size-changer="true"
              :total="catalogQuery.data.value?.total ?? 0"
              @update:current="(page: number) => { catalogFilters.page = page; }"
              @update:page-size="(pageSize: number) => { catalogFilters.page = 1; catalogFilters.pageSize = pageSize; }"
            />
          </div>
        </div>

        <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
          <div class="flex items-center justify-between gap-3">
            <div>
              <div class="text-sm font-semibold">
                {{ t('skills.installedTitle') }}
              </div>
              <div class="mt-1 text-xs text-muted-foreground">
                {{ t('skills.installedDescription') }}
              </div>
            </div>
          </div>

          <a-input
            v-model:value="installedSearch"
            allow-clear
            class="mt-4"
            :placeholder="t('skills.installedSearchPlaceholder')"
          />

          <div
            v-if="installedQuery.data.value?.length"
            class="mt-4 space-y-3"
          >
            <div
              v-for="item in installedQuery.data.value ?? []"
              :key="item.installKey || `${item.owner}-${item.repo}-${item.skillId}`"
              class="rounded-2xl border border-border/60 bg-background/90 p-4"
            >
              <div class="text-sm font-semibold">
                {{ item.displayName || item.name || '--' }}
              </div>
              <div class="mt-1 text-xs text-muted-foreground">
                {{ item.owner }}/{{ item.repo }} · {{ item.skillId }}
              </div>
              <div class="mt-2 text-xs text-muted-foreground">
                {{ t('skills.directory') }}: {{ item.installRootPath || '--' }}
              </div>
              <div class="mt-1 text-xs text-muted-foreground">
                {{ t('skills.updatedAt') }}: {{ formatDate(item.updatedAt) }}
              </div>

              <div class="mt-4 flex flex-wrap gap-2">
                <a-button size="small" @click="openSkillDetail(item)">
                  {{ t('skills.viewDetails') }}
                </a-button>
                <a-button danger size="small" @click="confirmUninstall(item)">
                  {{ t('skills.uninstall') }}
                </a-button>
              </div>
            </div>
          </div>

          <a-empty v-else class="py-10" :description="t('skills.installedEmpty')" />
        </div>
      </div>
    </div>

    <a-drawer
      v-model:open="detailOpen"
      :title="detailView?.displayName || detailView?.skillId || t('skills.detailTitle')"
      width="520"
    >
      <template v-if="detailView">
        <div class="space-y-4">
          <div class="flex flex-wrap gap-2">
            <a-tag color="blue">
              {{ detailView.sourceKey || 'skills.sh' }}
            </a-tag>
            <a-tag :color="selectedInstalledItem ? 'success' : 'default'">
              {{ selectedInstalledItem ? t('skills.installedTag') : t('skills.notInstalledTag') }}
            </a-tag>
          </div>

          <p class="text-sm text-muted-foreground">
            {{ detailView.description || t('skills.noDescription') }}
          </p>

          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item :label="t('skills.ownerLabel')">
              {{ detailView.owner || '--' }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('skills.repoLabel')">
              {{ detailView.repo || '--' }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('skills.skillIdLabel')">
              {{ detailView.skillId || '--' }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('skills.installs')">
              {{ detailView.installs ?? '--' }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('skills.directory')">
              {{ selectedInstalledItem?.installRootPath || '--' }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('skills.updatedAt')">
              {{ formatDate(selectedInstalledItem?.updatedAt) }}
            </a-descriptions-item>
          </a-descriptions>

          <div class="flex flex-wrap gap-2">
            <a-button
              v-if="detailView.githubUrl"
              @click="openRepository(detailView.githubUrl)"
            >
              {{ t('skills.openRepository') }}
            </a-button>
            <a-button
              type="primary"
              :disabled="!!selectedInstalledItem"
              @click="handleInstall(detailView)"
            >
              {{ t('skills.install') }}
            </a-button>
            <a-button
              v-if="selectedInstalledItem"
              danger
              @click="confirmUninstall(selectedInstalledItem)"
            >
              {{ t('skills.uninstall') }}
            </a-button>
          </div>
        </div>
      </template>
    </a-drawer>
  </Page>
</template>
