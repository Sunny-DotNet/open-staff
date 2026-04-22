<script setup lang="ts">
import type { ProviderAccountDto, ProviderModelDto } from '@openstaff/api';

import {
  getApiProviderAccountsByIdModels,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { computed, ref, watch } from 'vue';

import { t } from '@/i18n';

const props = defineProps<{
  account: null | ProviderAccountDto;
  open: boolean;
}>();

const emit = defineEmits<{
  (event: 'update:open', value: boolean): void;
}>();

const loading = ref(false);
const error = ref<string | null>(null);
const models = ref<ProviderModelDto[]>([]);

const title = computed(() => {
  const accountName = props.account?.name || t('provider.unnamed');
  return `${t('provider.modelsTitle')} · ${accountName}`;
});

const columns = computed(() => [
  {
    title: t('provider.modelId'),
    dataIndex: 'id',
    key: 'id',
  },
  {
    title: t('provider.modelVendor'),
    dataIndex: 'vendor',
    key: 'vendor',
    width: 180,
  },
  {
    title: t('provider.modelProtocols'),
    dataIndex: 'protocols',
    key: 'protocols',
    width: 220,
  },
]);

watch(
  () => [props.open, props.account?.id] as const,
  ([open]) => {
    if (!open) {
      resetState();
      return;
    }

    if (props.account?.id) {
      void loadModels();
    }
  },
  { immediate: true },
);

async function refreshModels() {
  await loadModels();
}

async function loadModels() {
  if (!props.account?.id) {
    return;
  }

  loading.value = true;
  error.value = null;

  try {
    models.value = unwrapClientEnvelope(
      await getApiProviderAccountsByIdModels({
        path: { id: props.account.id },
      }),
    );
  } catch (loadError) {
    error.value = getErrorMessage(loadError, t('provider.loadModelsFailed'));
  } finally {
    loading.value = false;
  }
}

function closeDrawer() {
  emit('update:open', false);
}

function modelRowKey(row: ProviderModelDto) {
  return `${row.id ?? 'unknown'}-${row.vendor ?? 'unknown'}`;
}

function resetState() {
  loading.value = false;
  error.value = null;
  models.value = [];
}

function getErrorMessage(errorValue: unknown, fallback: string) {
  if (errorValue instanceof Error && errorValue.message) {
    return errorValue.message;
  }

  return fallback;
}
</script>

<template>
  <a-drawer
    :open="open"
    :title="title"
    :width="720"
    destroy-on-close
    @close="closeDrawer"
  >
    <template #extra>
      <a-space>
        <a-button @click="closeDrawer">
          {{ t('provider.cancel') }}
        </a-button>
        <a-button :disabled="!account?.id" :loading="loading" @click="refreshModels">
          {{ t('common.refresh') }}
        </a-button>
      </a-space>
    </template>

    <div class="space-y-4">
      <section
        v-if="account"
        class="rounded-2xl border border-border/70 bg-background/75 p-4"
      >
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div class="text-sm font-semibold">
              {{ account.name || t('provider.unnamed') }}
            </div>
            <div class="mt-1 text-xs text-muted-foreground">
              {{ account.protocolType || '--' }}
            </div>
          </div>
          <a-tag :color="account.isEnabled ? 'success' : 'default'">
            {{
              account.isEnabled
                ? t('provider.enabledState')
                : t('provider.disabledState')
            }}
          </a-tag>
        </div>
      </section>

      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-4">
          <h4 class="text-sm font-semibold">{{ t('provider.modelsSectionTitle') }}</h4>
          <p class="mt-1 text-xs leading-6 text-muted-foreground">
            {{ t('provider.modelsSectionDescription') }}
          </p>
        </div>

        <a-alert
          v-if="error"
          type="error"
          show-icon
          :message="error"
        />

        <a-empty
          v-else-if="!loading && models.length === 0"
          :description="t('provider.noModels')"
        />

        <a-table
          v-else
          :columns="columns"
          :data-source="models"
          :loading="loading"
          :pagination="{ pageSize: 8, showSizeChanger: false }"
          :row-key="modelRowKey"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'id'">
              <div class="py-1">
                <div class="font-medium text-foreground">
                  {{ record.id || '--' }}
                </div>
                <div class="text-xs text-muted-foreground">
                  {{ record.vendor || '--' }}
                </div>
              </div>
            </template>

            <template v-else-if="column.key === 'protocols'">
              <span class="text-sm text-muted-foreground">
                {{ record.protocols || '--' }}
              </span>
            </template>
          </template>
        </a-table>
      </section>
    </div>
  </a-drawer>
</template>
