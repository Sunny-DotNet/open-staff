<script setup lang="ts">
import type {
  AgentRoleDto,
  VendorModelCatalogDto,
  VendorProviderConfigurationDto,
  VendorProviderConfigurationPropertyDto,
} from '@openstaff/api';

import {
  getApiAgentRolesVendorByProviderTypeConfiguration,
  getApiAgentRolesVendorByProviderTypeModelCatalog,
  postApiAgentRoles,
  postApiAgentRolesVendorByProviderTypeReset,
  putApiAgentRolesById,
  putApiAgentRolesVendorByProviderTypeConfiguration,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

type VendorConfigValue = boolean | number | string | null;

const props = defineProps<{
  open: boolean;
  role: AgentRoleDto | null;
}>();

const emit = defineEmits<{
  (event: 'saved'): void;
  (event: 'update:open', value: boolean): void;
}>();

const configurationLoading = ref(false);
const catalogLoading = ref(false);
const configurationError = ref<string | null>(null);
const catalogError = ref<string | null>(null);
const providerConfiguration = ref<null | VendorProviderConfigurationDto>(null);
const modelCatalog = ref<null | VendorModelCatalogDto>(null);
const selectedModel = ref('');
const configValues = reactive<Record<string, VendorConfigValue>>({});

const configurationProperties = computed(
  () => providerConfiguration.value?.properties ?? [],
);
const isVirtualRole = computed(() => !!props.role?.isVirtual);
const providerType = computed(() => props.role?.providerType ?? '');
const catalogModels = computed(() => modelCatalog.value?.models ?? []);
const missingConfigurationFields = computed(
  () => modelCatalog.value?.missingConfigurationFields ?? [],
);

const saveRoleMutation = useMutation({
  mutationFn: async () => {
    if (!props.role) {
      throw new Error(t('role.validationRole'));
    }

    if (isVirtualRole.value) {
      return unwrapClientEnvelope(
        await postApiAgentRoles({
          body: {
            avatar: props.role.avatar ?? undefined,
            modelName: selectedModel.value,
            name: props.role.name ?? providerType.value,
            providerType: providerType.value,
            roleType: props.role.roleType ?? providerType.value,
            source: props.role.source ?? 3,
          },
        }),
      );
    }

    if (!props.role.id) {
      throw new Error(t('role.validationRole'));
    }

    return unwrapClientEnvelope(
      await putApiAgentRolesById({
        body: {
          modelName: selectedModel.value || undefined,
        },
        path: { id: props.role.id },
      }),
    );
  },
  onSuccess: () => {
    message.success(t('role.vendorSaveSuccess'));
    emit('saved');
    closeDrawer();
  },
});

const saveConfigurationMutation = useMutation({
  mutationFn: async () => {
    if (!providerType.value) {
      throw new Error(t('role.validationProviderType'));
    }

    return unwrapClientEnvelope(
      await putApiAgentRolesVendorByProviderTypeConfiguration({
        body: {
          configuration: buildConfigurationPayload(),
        },
        path: { providerType: providerType.value },
      }),
    );
  },
  onSuccess: async (data) => {
    providerConfiguration.value = data;
    message.success(t('role.vendorConfigSaved'));
    await loadCatalog();
  },
});

const resetVendorMutation = useMutation({
  mutationFn: async () => {
    if (!providerType.value) {
      throw new Error(t('role.validationProviderType'));
    }

    return postApiAgentRolesVendorByProviderTypeReset({
      path: { providerType: providerType.value },
    });
  },
  onSuccess: () => {
    message.success(t('role.resetSuccess'));
    emit('saved');
    closeDrawer();
  },
});

watch(
  () => [props.open, props.role?.providerType] as const,
  async ([open]) => {
    if (!open) {
      resetState();
      return;
    }

    selectedModel.value = props.role?.modelName ?? '';
    await Promise.all([loadConfiguration(), loadCatalog()]);
  },
  { immediate: true },
);

async function refreshAll() {
  await Promise.all([loadConfiguration(), loadCatalog()]);
}

async function loadConfiguration() {
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

async function loadCatalog() {
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
  } catch (error) {
    catalogError.value = getErrorMessage(error, t('role.vendorCatalogLoadFailed'));
  } finally {
    catalogLoading.value = false;
  }
}

async function saveAll() {
  if (!selectedModel.value) {
    message.error(t('role.validationModel'));
    return;
  }

  if (!validateConfiguration()) {
    return;
  }

  try {
    await saveConfigurationMutation.mutateAsync();
    await saveRoleMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

async function resetVendor() {
  try {
    await resetVendorMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  }
}

function closeDrawer() {
  emit('update:open', false);
}

function resetState() {
  configurationLoading.value = false;
  catalogLoading.value = false;
  configurationError.value = null;
  catalogError.value = null;
  providerConfiguration.value = null;
  modelCatalog.value = null;
  selectedModel.value = '';
  clearConfigValues();
}

function clearConfigValues() {
  for (const key of Object.keys(configValues)) {
    delete configValues[key];
  }
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

    payload[property.name] = normalizeConfigValue(
      property,
      configValues[property.name],
    );
  }

  return payload;
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

function isSensitiveField(name?: string) {
  const normalized = (name ?? '').toLowerCase();
  return (
    normalized.includes('api_key') ||
    normalized.includes('apikey') ||
    normalized.includes('password') ||
    normalized.includes('secret') ||
    normalized.includes('token')
  );
}

function isBooleanField(property: VendorProviderConfigurationPropertyDto) {
  return property.fieldType === 'boolean';
}

function isNumberField(property: VendorProviderConfigurationPropertyDto) {
  return property.fieldType === 'double' || property.fieldType === 'int64';
}

function isIntegerField(property: VendorProviderConfigurationPropertyDto) {
  return property.fieldType === 'int64';
}

function formatConfigLabel(name: string) {
  return name
    .split('_')
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <a-drawer
    :open="open"
    :title="t('role.vendorTitle')"
    :width="720"
    destroy-on-close
    @close="closeDrawer"
  >
    <template #extra>
      <a-space>
        <a-button @click="closeDrawer">
          {{ t('role.cancel') }}
        </a-button>
        <a-button
          :loading="configurationLoading || catalogLoading"
          @click="refreshAll"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button
          v-if="role && !role.isVirtual"
          danger
          :loading="resetVendorMutation.isPending.value"
          @click="resetVendor"
        >
          {{ t('role.reset') }}
        </a-button>
        <a-button
          type="primary"
          :loading="
            saveConfigurationMutation.isPending.value ||
            saveRoleMutation.isPending.value
          "
          @click="saveAll"
        >
          {{ isVirtualRole ? t('role.materialize') : t('role.save') }}
        </a-button>
      </a-space>
    </template>

    <div class="space-y-4">
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div class="text-sm font-semibold">
              {{ role?.name || t('role.unnamedRole') }}
            </div>
            <div class="mt-1 text-xs text-muted-foreground">
              {{ role?.providerType || '--' }}
            </div>
          </div>
          <a-tag :color="role?.isVirtual ? 'default' : 'purple'">
            {{ role?.isVirtual ? t('role.sourceVirtualVendor') : t('role.sourceVendor') }}
          </a-tag>
        </div>
      </section>

      <a-alert
        v-if="configurationError"
        type="error"
        show-icon
        :message="configurationError"
      />

      <a-alert
        v-if="catalogError"
        type="error"
        show-icon
        :message="catalogError"
      />

      <a-alert
        v-if="modelCatalog?.status === 'requires_provider_configuration'"
        type="warning"
        show-icon
        :message="modelCatalog.message || t('role.vendorCatalogNeedsConfig')"
      />

      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-4">
          <h4 class="text-sm font-semibold">{{ t('role.vendorModelSection') }}</h4>
          <p class="mt-1 text-xs leading-6 text-muted-foreground">
            {{ t('role.vendorModelSectionDescription') }}
          </p>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <a-form-item :label="t('role.modelCatalogStatus')">
            <a-input :value="modelCatalog?.status || '--'" disabled />
          </a-form-item>
          <a-form-item :label="t('role.model')">
            <a-select
              v-model:value="selectedModel"
              allow-clear
              show-search
              :loading="catalogLoading"
              :options="
                catalogModels.map((model) => ({
                  label: model.name || model.id,
                  value: model.id,
                }))
              "
              :placeholder="t('role.modelPlaceholder')"
            />
          </a-form-item>
        </div>

        <div v-if="missingConfigurationFields.length" class="mt-2 text-xs text-warning-700">
          {{ t('role.missingFields') }}:
          {{ missingConfigurationFields.join(', ') }}
        </div>
      </section>

      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-4">
          <h4 class="text-sm font-semibold">{{ t('role.vendorConfigSection') }}</h4>
          <p class="mt-1 text-xs leading-6 text-muted-foreground">
            {{ t('role.vendorConfigSectionDescription') }}
          </p>
        </div>

        <a-empty
          v-if="!configurationProperties.length && !configurationLoading"
          :description="t('role.noVendorConfig')"
        />

        <a-form v-else layout="vertical">
          <div class="grid gap-4 md:grid-cols-2">
            <a-form-item
              v-for="property in configurationProperties"
              :key="property.name"
              :label="formatConfigLabel(property.name ?? '')"
              :required="property.required"
            >
              <a-switch
                v-if="isBooleanField(property)"
                :checked="Boolean(configValues[property.name ?? ''])"
                @update:checked="configValues[property.name ?? ''] = $event"
              />

              <a-input-number
                v-else-if="isNumberField(property)"
                :precision="isIntegerField(property) ? 0 : undefined"
                :value="
                  typeof configValues[property.name ?? ''] === 'number'
                    ? Number(configValues[property.name ?? ''])
                    : null
                "
                class="w-full"
                @update:value="
                  configValues[property.name ?? ''] =
                    typeof $event === 'number' ? $event : null
                "
              />

              <a-input-password
                v-else-if="isSensitiveField(property.name)"
                :value="String(configValues[property.name ?? ''] ?? '')"
                @update:value="configValues[property.name ?? ''] = $event ?? ''"
              />

              <a-input
                v-else
                :value="String(configValues[property.name ?? ''] ?? '')"
                @update:value="configValues[property.name ?? ''] = $event ?? ''"
              />
            </a-form-item>
          </div>
        </a-form>
      </section>
    </div>
  </a-drawer>
</template>
