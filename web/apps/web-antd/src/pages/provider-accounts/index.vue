<script setup lang="ts">
import type { AnalysisOverviewItem } from '@vben/common-ui';
import type { ProviderAccountDto } from '@openstaff/api';

import { AnalysisOverview, Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import {
  deleteApiProviderAccountsById,
  getApiProviderAccounts,
  getApiProviderAccountsProviders,
  unwrapCollection,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { computed, ref } from 'vue';

import { t } from '@/i18n';

import ProviderAccountEdit from './edit.vue';
import ProviderAccountModels from './models.vue';

const searchText = ref('');
const statusFilter = ref<'all' | 'disabled' | 'enabled'>('all');
const providerFilter = ref('all');
const editorOpen = ref(false);
const configOpen = ref(false);
const modelsOpen = ref(false);
const editorMode = ref<'create' | 'edit'>('create');
const activeAccount = ref<ProviderAccountDto | null>(null);

const providersQuery = useQuery({
  queryKey: ['provider-accounts', 'providers'],
  queryFn: async () => unwrapClientEnvelope(await getApiProviderAccountsProviders()),
});

const providerAccountsQuery = useQuery({
  queryKey: ['provider-accounts', 'accounts'],
  queryFn: async () =>
    unwrapCollection(unwrapClientEnvelope(await getApiProviderAccounts())),
});

const providers = computed(() => providersQuery.data.value ?? []);
const providerAccounts = computed(() => providerAccountsQuery.data.value ?? []);
const enabledCount = computed(
  () => providerAccounts.value.filter((account) => account.isEnabled).length,
);
const filteredAccounts = computed(() =>
  providerAccounts.value.filter((account) => {
    const keyword = searchText.value.trim().toLowerCase();
    const matchesKeyword =
      keyword.length === 0 ||
      account.name?.toLowerCase().includes(keyword) ||
      account.protocolType?.toLowerCase().includes(keyword);
    const matchesStatus =
      statusFilter.value === 'all' ||
      (statusFilter.value === 'enabled' && account.isEnabled) ||
      (statusFilter.value === 'disabled' && !account.isEnabled);
    const matchesProvider =
      providerFilter.value === 'all' ||
      account.protocolType === providerFilter.value;

    return matchesKeyword && matchesStatus && matchesProvider;
  }),
);
const providerCards = computed(() =>
  providers.value.map((provider) => ({
    provider,
    accountCount: providerAccounts.value.filter(
      (account) => account.protocolType === provider.key,
    ).length,
  })),
);
const providerOptions = computed(() => [
  { label: t('provider.allProviders'), value: 'all' },
  ...providers.value.map((provider) => ({
    label: provider.displayName ?? provider.key ?? '--',
    value: provider.key ?? 'unknown',
  })),
]);
const accountColumns = computed(() => [
  {
    title: t('provider.name'),
    dataIndex: 'name',
    key: 'name',
  },
  {
    title: t('provider.provider'),
    dataIndex: 'protocolType',
    key: 'protocolType',
  },
  {
    title: t('provider.status'),
    dataIndex: 'isEnabled',
    key: 'isEnabled',
    width: 120,
  },
  {
    title: t('provider.createdAt'),
    dataIndex: 'createdAt',
    key: 'createdAt',
    width: 180,
  },
  {
    title: t('provider.updatedAt'),
    dataIndex: 'updatedAt',
    key: 'updatedAt',
    width: 180,
  },
  {
    title: t('provider.actions'),
    key: 'actions',
    width: 320,
  },
]);
const overviewItems = computed<AnalysisOverviewItem[]>(() => [
  {
    title: t('provider.providers'),
    icon: 'lucide:key-round',
    value: providers.value.length,
    totalTitle: t('provider.directory'),
    totalValue: providerCards.value.filter((item) => item.accountCount > 0).length,
  },
  {
    title: t('provider.accounts'),
    icon: 'lucide:server-cog',
    value: providerAccounts.value.length,
    totalTitle: t('common.all'),
    totalValue: filteredAccounts.value.length,
  },
  {
    title: t('provider.enabled'),
    icon: 'lucide:badge-check',
    value: enabledCount.value,
    totalTitle: t('provider.disabledState'),
    totalValue: Math.max(providerAccounts.value.length - enabledCount.value, 0),
  },
  {
    title: t('provider.provider'),
    icon: 'lucide:file-code-2',
    value: providerCards.value.filter((item) => item.accountCount > 0).length,
    totalTitle: t('common.live'),
    totalValue: providers.value.length,
  },
]);

const deleteAccountMutation = useMutation({
  mutationFn: async (id: string) =>
    deleteApiProviderAccountsById({
      path: { id },
    }),
  onSuccess: async () => {
    message.success(t('provider.deleteSuccess'));
    await refreshAll();
  },
});

async function refreshAll() {
  await Promise.all([providersQuery.refetch(), providerAccountsQuery.refetch()]);
}

function handleSaved() {
  void refreshAll();
}

function openCreateEditor() {
  editorMode.value = 'create';
  activeAccount.value = null;
  editorOpen.value = true;
}

function openEditEditor(account: ProviderAccountDto) {
  editorMode.value = 'edit';
  activeAccount.value = account;
  editorOpen.value = true;
}

function openConfigEditor(account: ProviderAccountDto) {
  activeAccount.value = account;
  configOpen.value = true;
}

function openModelsDrawer(account: ProviderAccountDto) {
  activeAccount.value = account;
  modelsOpen.value = true;
}

async function removeAccount(account: ProviderAccountDto) {
  if (!account.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  try {
    await deleteAccountMutation.mutateAsync(account.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.actionFailed')));
  }
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

function resolveProviderName(key: string | undefined) {
  return (
    providers.value.find((provider) => provider.key === key)?.displayName ??
    key ??
    '--'
  );
}

function formatDate(value: ProviderAccountDto['updatedAt']) {
  if (!value) {
    return '--';
  }

  return dayjs(value).format('YYYY-MM-DD HH:mm');
}
</script>

<template>
  <Page :title="t('provider.title')" content-class="space-y-5">
    <template #extra>
      <a-space>
        <a-button
          :loading="providersQuery.isFetching.value || providerAccountsQuery.isFetching.value"
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button type="primary" @click="openCreateEditor">
          {{ t('provider.create') }}
        </a-button>
      </a-space>
    </template>

    <AnalysisOverview :items="overviewItems" />

    <section class="card-box p-4">
      <div class="flex flex-col gap-3 md:flex-row">
        <a-input
          v-model:value="searchText"
          allow-clear
          :placeholder="t('provider.searchPlaceholder')"
          style="width: 260px"
        />
        <a-select
          v-model:value="providerFilter"
          :options="providerOptions"
          style="width: 180px"
        />
        <a-select
          v-model:value="statusFilter"
          :options="[
            { label: t('provider.allStatus'), value: 'all' },
            { label: t('provider.enabledState'), value: 'enabled' },
            { label: t('provider.disabledState'), value: 'disabled' },
          ]"
          style="width: 160px"
        />
      </div>
    </section>

    <a-alert
      v-if="providerAccountsQuery.isError.value"
      type="error"
      show-icon
      :message="t('common.requestFailed', { status: 'accounts' })"
    />

    <section class="card-box overflow-hidden">
      <div class="border-b border-border/70 px-5 py-4">
        <h3 class="text-base font-semibold">{{ t('provider.accounts') }}</h3>
      </div>

      <a-table
        :columns="accountColumns"
        :data-source="filteredAccounts"
        :loading="providerAccountsQuery.isLoading.value"
        :pagination="{ pageSize: 8, showSizeChanger: false }"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <div v-if="column.key === 'name'" class="py-1">
            <div class="font-medium text-foreground">
              {{ record.name || t('provider.unnamed') }}
            </div>
            <div class="text-xs text-muted-foreground">
              {{ record.id }}
            </div>
          </div>

          <template v-else-if="column.key === 'protocolType'">
            <div class="py-1">
              <div class="font-medium text-foreground">
                {{ resolveProviderName(record.protocolType) }}
              </div>
              <div class="text-xs text-muted-foreground">
                {{ record.protocolType || '--' }}
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'isEnabled'">
            <a-tag :color="record.isEnabled ? 'success' : 'default'">
              {{
                record.isEnabled
                  ? t('provider.enabledState')
                  : t('provider.disabledState')
              }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>

          <template v-else-if="column.key === 'updatedAt'">
            {{ formatDate(record.updatedAt) }}
          </template>

          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openConfigEditor(record)">
                {{ t('provider.configure') }}
              </a-button>
              <a-button size="small" type="link" @click="openModelsDrawer(record)">
                {{ t('provider.models') }}
              </a-button>
              <a-button size="small" type="link" @click="openEditEditor(record)">
                {{ t('provider.edit') }}
              </a-button>
              <a-popconfirm
                :ok-text="t('provider.delete')"
                :title="t('provider.deleteConfirm')"
                @confirm="removeAccount(record)"
              >
                <a-button
                  danger
                  size="small"
                  type="link"
                  :loading="
                    deleteAccountMutation.isPending.value &&
                    deleteAccountMutation.variables.value === record.id
                  "
                >
                  {{ t('provider.delete') }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </section>

    <ProviderAccountEdit
      v-model:editor-open="editorOpen"
      v-model:config-open="configOpen"
      :account="activeAccount"
      :mode="editorMode"
      :providers="providers"
      @saved="handleSaved"
    />

    <ProviderAccountModels
      v-model:open="modelsOpen"
      :account="activeAccount"
    />
  </Page>
</template>
