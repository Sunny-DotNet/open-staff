<script setup lang="ts">
import type { AgentRoleDto } from '@openstaff/api';

import { Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import {
  deleteApiAgentRolesById,
  getApiAgentRoles,
  getApiProviderAccounts,
  postApiAgentRolesVendorByProviderTypeReset,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';

import { t } from '@/i18n';
import { localizeJobTitle } from '@/utils/job-title';

import AgentRoleEdit from './edit.vue';
import AgentRoleWorkspace from './workspace.vue';

const SOURCE_CUSTOM = 0;
const SOURCE_BUILTIN = 1;
const SOURCE_VENDOR = 3;
const router = useRouter();

const searchText = ref('');
const sourceFilter = ref<'all' | 'builtin' | 'custom' | 'vendor' | 'virtual'>('all');
const editOpen = ref(false);
const workspaceOpen = ref(false);
const activeRole = ref<AgentRoleDto | null>(null);

const rolesQuery = useQuery({
  queryKey: ['agent-roles', 'list'],
  queryFn: async () => unwrapClientEnvelope(await getApiAgentRoles()),
});

const providersQuery = useQuery({
  queryKey: ['provider-accounts', 'role-models'],
  queryFn: async () =>
    unwrapClientEnvelope(await getApiProviderAccounts()).items ?? [],
});

const roles = computed(() => rolesQuery.data.value ?? []);
const providers = computed(() => providersQuery.data.value ?? []);
const filteredRoles = computed(() =>
  roles.value.filter((role) => {
    const keyword = searchText.value.trim().toLowerCase();
    const matchesKeyword =
      !keyword ||
      role.name?.toLowerCase().includes(keyword) ||
      role.jobTitle?.toLowerCase().includes(keyword) ||
      localizeJobTitle(role.jobTitle)?.toLowerCase().includes(keyword) ||
      role.modelName?.toLowerCase().includes(keyword) ||
      role.providerType?.toLowerCase().includes(keyword) ||
      role.modelProviderName?.toLowerCase().includes(keyword);

    const matchesSource =
      sourceFilter.value === 'all' ||
      (sourceFilter.value === 'builtin' && role.source === SOURCE_BUILTIN) ||
      (sourceFilter.value === 'custom' && role.source === SOURCE_CUSTOM) ||
      (sourceFilter.value === 'vendor' && role.source === SOURCE_VENDOR && !role.isVirtual) ||
      (sourceFilter.value === 'virtual' && !!role.isVirtual);

    return matchesKeyword && matchesSource;
  }),
);

const accountColumns = computed(() => [
  { title: t('role.name'), dataIndex: 'name', key: 'name' },
  { title: t('role.jobTitle'), dataIndex: 'jobTitle', key: 'jobTitle', width: 180 },
  { title: t('role.source'), dataIndex: 'source', key: 'source', width: 140 },
  { title: t('role.model'), dataIndex: 'modelName', key: 'modelName', width: 260 },
  { title: t('role.actions'), key: 'actions', width: 240 },
]);

const deleteRoleMutation = useMutation({
  mutationFn: async (id: string) =>
    deleteApiAgentRolesById({
      path: { id },
    }),
  onSuccess: async () => {
    message.success(t('role.deleteSuccess'));
    await refreshAll();
  },
});

const resetVendorMutation = useMutation({
  mutationFn: async (providerType: string) =>
    postApiAgentRolesVendorByProviderTypeReset({
      path: { providerType },
    }),
  onSuccess: async () => {
    message.success(t('role.resetSuccess'));
    await refreshAll();
  },
});

async function refreshAll() {
  await Promise.all([rolesQuery.refetch(), providersQuery.refetch()]);
}

function openCreate() {
  activeRole.value = null;
  editOpen.value = true;
}

function openWorkspace(role: AgentRoleDto) {
  activeRole.value = role;
  workspaceOpen.value = true;
}

async function removeRole(role: AgentRoleDto) {
  if (!role.id) {
    message.error(t('role.validationRole'));
    return;
  }

  try {
    await deleteRoleMutation.mutateAsync(role.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

async function resetVendor(role: AgentRoleDto) {
  if (!role.providerType) {
    message.error(t('role.validationProviderType'));
    return;
  }

  try {
    await resetVendorMutation.mutateAsync(role.providerType);
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

function handleSaved() {
  void refreshAll();
}

function isVendorRole(role: AgentRoleDto) {
  return role.source === SOURCE_VENDOR;
}

function getSourceLabel(role: AgentRoleDto) {
  if (role.source === SOURCE_BUILTIN) {
    return t('role.sourceBuiltin');
  }

  if (role.source === SOURCE_VENDOR) {
    return role.isVirtual ? t('role.sourceVirtualVendor') : t('role.sourceVendor');
  }

  return t('role.sourceCustom');
}

function getSourceColor(role: AgentRoleDto) {
  if (role.source === SOURCE_BUILTIN) {
    return 'blue';
  }

  if (role.source === SOURCE_VENDOR) {
    return role.isVirtual ? 'default' : 'purple';
  }

  return 'green';
}

function getRoleRowKey(role: AgentRoleDto) {
  return role.id && role.id !== '00000000-0000-0000-0000-000000000000'
    ? role.id
    : `virtual-${role.providerType ?? role.jobTitle ?? role.name ?? 'role'}`;
}

function getRoleName(role: AgentRoleDto) {
  return role.name || t('role.unnamedRole');
}

function getRoleAvatarFallback(role: AgentRoleDto) {
  return [...getRoleName(role).trim()].slice(0, 2).join('') || 'A';
}

function getModelProviderLabel(role: AgentRoleDto) {
  return role.modelProviderName || role.providerType || '--';
}

function getModelDisplay(role: AgentRoleDto) {
  if (!role.modelName) {
    return '--';
  }

  const providerLabel = getModelProviderLabel(role);
  return providerLabel && providerLabel !== '--'
    ? `${role.modelName} (${providerLabel})`
    : role.modelName;
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <Page :title="t('role.title')" content-class="agent-roles-page space-y-5">
    <template #extra>
      <a-space>
        <a-button
          :loading="rolesQuery.isFetching.value || providersQuery.isFetching.value"
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button @click="router.push('/talent-market')">
          {{ t('role.talentMarket') }}
        </a-button>
        <a-button type="primary" @click="openCreate">
          {{ t('role.create') }}
        </a-button>
      </a-space>
    </template>

    <section class="card-box p-4">
      <div class="flex flex-col gap-3 md:flex-row">
        <a-input
          v-model:value="searchText"
          allow-clear
          :placeholder="t('role.searchPlaceholder')"
          style="width: 280px"
        />
        <a-select
          v-model:value="sourceFilter"
          :options="[
            { label: t('role.allSources'), value: 'all' },
            { label: t('role.sourceBuiltin'), value: 'builtin' },
            { label: t('role.sourceCustom'), value: 'custom' },
            { label: t('role.sourceVendor'), value: 'vendor' },
            { label: t('role.sourceVirtualVendor'), value: 'virtual' },
          ]"
          style="width: 180px"
        />
      </div>
    </section>

    <a-alert
      v-if="rolesQuery.isError.value"
      type="error"
      show-icon
      :message="t('common.requestFailed', { status: 'agent-roles' })"
    />

    <section class="card-box overflow-hidden">
      <div class="border-b border-border/70 px-5 py-4">
        <h3 class="text-base font-semibold">{{ t('role.roleList') }}</h3>
      </div>

      <a-table
        :columns="accountColumns"
        :data-source="filteredRoles"
        :loading="rolesQuery.isLoading.value"
        :pagination="{ pageSize: 8, showSizeChanger: false }"
        :row-key="getRoleRowKey"
      >
        <template #bodyCell="{ column, record }">
          <div v-if="column.key === 'name'" class="py-1">
            <div class="flex items-center gap-3">
              <a-avatar :src="record.avatar || undefined" :size="40">
                {{ getRoleAvatarFallback(record) }}
              </a-avatar>
              <div class="min-w-0">
                <div class="truncate font-medium text-foreground">
                  {{ getRoleName(record) }}
                </div>
              </div>
            </div>
          </div>

          <template v-else-if="column.key === 'jobTitle'">
            {{ localizeJobTitle(record.jobTitle) || '--' }}
          </template>

          <template v-else-if="column.key === 'source'">
            <a-tag :color="getSourceColor(record)">
              {{ getSourceLabel(record) }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'modelName'">
            {{ getModelDisplay(record) }}
          </template>

          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openWorkspace(record)">
                {{ t('role.workspace') }}
              </a-button>
              <a-popconfirm
                v-if="isVendorRole(record) && !record.isVirtual"
                :ok-text="t('role.reset')"
                :title="t('role.resetConfirm')"
                @confirm="resetVendor(record)"
              >
                <a-button size="small" type="link">
                  {{ t('role.reset') }}
                </a-button>
              </a-popconfirm>
              <a-popconfirm
                v-if="!record.isBuiltin && !isVendorRole(record)"
                :ok-text="t('role.delete')"
                :title="t('role.deleteConfirm')"
                @confirm="removeRole(record)"
              >
                <a-button
                  danger
                  size="small"
                  type="link"
                  :loading="
                    deleteRoleMutation.isPending.value &&
                    deleteRoleMutation.variables.value === record.id
                  "
                >
                  {{ t('role.delete') }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </section>

    <AgentRoleEdit
      v-model:open="editOpen"
      mode="create"
      :providers="providers"
      :role="activeRole"
      @saved="handleSaved"
    />

    <AgentRoleWorkspace
      v-model:open="workspaceOpen"
      :providers="providers"
      :role="activeRole"
      @saved="handleSaved"
    />
  </Page>
</template>

<style>
.agent-roles-page .ant-tag:not(.ant-tag-blue):not(.ant-tag-purple):not(.ant-tag-green):not(.ant-tag-gold) {
  color: hsl(var(--foreground)) !important;
  background: hsl(var(--muted) / 0.34) !important;
  border-color: hsl(var(--border)) !important;
}

.agent-roles-page .ant-table-thead > tr > th {
  color: hsl(var(--foreground)) !important;
  border-bottom-color: hsl(var(--border)) !important;
}

.agent-roles-page .ant-table-tbody > tr > td,
.agent-roles-page .ant-table-wrapper .ant-pagination .ant-pagination-item a,
.agent-roles-page .ant-select-selection-item,
.agent-roles-page .ant-select-selection-placeholder {
  color: hsl(var(--foreground)) !important;
}

.agent-roles-page .ant-input,
.agent-roles-page .ant-select-selector {
  border-color: hsl(var(--border)) !important;
}
</style>
