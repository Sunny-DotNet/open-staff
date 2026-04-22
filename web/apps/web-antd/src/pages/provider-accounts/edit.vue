<script setup lang="ts">
import type {
  ConfigurationProperty,
  CreateProviderAccountInput,
  DeviceAuthPollDto,
  DeviceCodeDto,
  JsonElement,
  ProviderAccountDto,
  ProviderInfo,
  UpdateProviderAccountInput,
} from '@openstaff/api';

import {
  deleteApiProviderAccountsByIdDeviceAuth,
  getApiProviderAccountsByIdConfiguration,
  postApiProviderAccounts,
  postApiProviderAccountsByIdDeviceAuth,
  postApiProviderAccountsByIdDeviceAuthPoll,
  putApiProviderAccountsById,
  putApiProviderAccountsByIdConfiguration,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

type ConfigFieldValue = boolean | number | string | null;

const CONFIG_TYPE_STRING = 0;
const CONFIG_TYPE_BOOLEAN = 1;
const CONFIG_TYPE_INT64 = 2;
const CONFIG_TYPE_DOUBLE = 3;
const GITHUB_COPILOT_PROTOCOL = 'github-copilot';

const props = defineProps<{
  account: null | ProviderAccountDto;
  configOpen: boolean;
  editorOpen: boolean;
  mode: 'create' | 'edit';
  providers: ProviderInfo[];
}>();

const emit = defineEmits<{
  (event: 'saved'): void;
  (event: 'update:configOpen', value: boolean): void;
  (event: 'update:editorOpen', value: boolean): void;
}>();

const editorSubmitting = ref(false);
const configLoading = ref(false);
const configProperties = ref<ConfigurationProperty[]>([]);
const configError = ref<string | null>(null);
const configLoadedKeys = ref<string[]>([]);
const configOriginal = ref<Record<string, unknown>>({});
const configDeviceCode = ref<DeviceCodeDto | null>(null);
const configDeviceStatus = ref<DeviceAuthPollDto | null>(null);
const editorForm = reactive<{
  isEnabled: boolean;
  name: string;
  protocolType: string;
}>({
  isEnabled: true,
  name: '',
  protocolType: '',
});
const configForm = reactive<Record<string, ConfigFieldValue>>({});
const configTouched = reactive<Record<string, boolean>>({});

const providerOptions = computed(() =>
  props.providers.map((provider) => ({
    label: provider.displayName ?? provider.key ?? '--',
    value: provider.key ?? 'unknown',
  })),
);
const configDrawerTitle = computed(() => {
  const accountName = props.account?.name || t('provider.unnamed');
  return `${t('provider.configureTitle')} · ${accountName}`;
});
const supportsDeviceAuth = computed(
  () => props.account?.protocolType === GITHUB_COPILOT_PROTOCOL,
);

const createAccountMutation = useMutation({
  mutationFn: async (payload: CreateProviderAccountInput) =>
    unwrapClientEnvelope(await postApiProviderAccounts({ body: payload })),
  onSuccess: () => {
    message.success(t('provider.createSuccess'));
    emit('saved');
    closeEditor();
  },
});

const updateAccountMutation = useMutation({
  mutationFn: async ({
    id,
    payload,
  }: {
    id: string;
    payload: UpdateProviderAccountInput;
  }) =>
    unwrapClientEnvelope(
      await putApiProviderAccountsById({
        body: payload,
        path: { id },
      }),
    ),
  onSuccess: () => {
    message.success(t('provider.updateSuccess'));
    emit('saved');
    closeEditor();
  },
});

const saveConfigurationMutation = useMutation({
  mutationFn: async ({
    id,
    payload,
  }: {
    id: string;
    payload: JsonElement;
  }) =>
    putApiProviderAccountsByIdConfiguration({
      body: payload,
      path: { id },
    }),
  onSuccess: async () => {
    message.success(t('provider.configSaveSuccess'));
    emit('saved');
    if (props.account) {
      await loadConfiguration(props.account);
    }
  },
});

const startDeviceAuthMutation = useMutation({
  mutationFn: async (id: string) =>
    unwrapClientEnvelope(
      await postApiProviderAccountsByIdDeviceAuth({
        path: { id },
      }),
    ),
  onSuccess: (data) => {
    configDeviceCode.value = data;
    configDeviceStatus.value = null;
    message.success(t('provider.deviceAuthStarted'));
  },
});

const pollDeviceAuthMutation = useMutation({
  mutationFn: async (id: string) =>
    unwrapClientEnvelope(
      await postApiProviderAccountsByIdDeviceAuthPoll({
        path: { id },
      }),
    ),
  onSuccess: (data) => {
    configDeviceStatus.value = data;
    message.success(t('provider.deviceAuthPolled'));
  },
});

const cancelDeviceAuthMutation = useMutation({
  mutationFn: async (id: string) =>
    deleteApiProviderAccountsByIdDeviceAuth({
      path: { id },
    }),
  onSuccess: () => {
    configDeviceCode.value = null;
    configDeviceStatus.value = null;
    message.success(t('provider.deviceAuthCancelled'));
  },
});

watch(
  () => [props.editorOpen, props.mode, props.account?.id] as const,
  ([open]) => {
    if (!open) {
      return;
    }

    if (props.mode === 'create') {
      editorForm.name = '';
      editorForm.protocolType = '';
      editorForm.isEnabled = true;
      return;
    }

    editorForm.name = props.account?.name ?? '';
    editorForm.protocolType = props.account?.protocolType ?? '';
    editorForm.isEnabled = !!props.account?.isEnabled;
  },
  { immediate: true },
);

watch(
  () => [props.configOpen, props.account?.id] as const,
  async ([open]) => {
    if (!open) {
      resetConfigState();
      return;
    }

    if (props.account) {
      await loadConfiguration(props.account);
    }
  },
  { immediate: true },
);

function closeEditor() {
  emit('update:editorOpen', false);
  editorSubmitting.value = false;
}

function closeConfigEditor() {
  emit('update:configOpen', false);
}

async function submitEditor() {
  if (!editorForm.name.trim()) {
    message.error(t('provider.validationName'));
    return;
  }

  if (props.mode === 'create' && !editorForm.protocolType) {
    message.error(t('provider.validationProvider'));
    return;
  }

  editorSubmitting.value = true;

  try {
    if (props.mode === 'create') {
      await createAccountMutation.mutateAsync({
        isEnabled: editorForm.isEnabled,
        name: editorForm.name.trim(),
        protocolType: editorForm.protocolType,
      });
      return;
    }

    if (!props.account?.id) {
      message.error(t('provider.validationAccount'));
      return;
    }

    await updateAccountMutation.mutateAsync({
      id: props.account.id,
      payload: {
        isEnabled: editorForm.isEnabled,
        name: editorForm.name.trim(),
      },
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.actionFailed')));
  } finally {
    editorSubmitting.value = false;
  }
}

async function reloadConfiguration() {
  if (!props.account) {
    return;
  }

  await loadConfiguration(props.account);
}

async function loadConfiguration(account: ProviderAccountDto) {
  if (!account.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  configLoading.value = true;
  configError.value = null;
  configProperties.value = [];
  configLoadedKeys.value = [];
  configOriginal.value = {};
  configDeviceCode.value = null;
  configDeviceStatus.value = null;
  clearDynamicState(configForm);
  clearDynamicState(configTouched);

  try {
    const result = unwrapClientEnvelope(
      await getApiProviderAccountsByIdConfiguration({
        path: { id: account.id },
      }),
    );
    const properties = (result.properties ?? []).filter(
      (property): property is ConfigurationProperty =>
        typeof property.name === 'string' && property.name.length > 0,
    );
    const configuration = toConfigurationObject(result.configuration);

    configProperties.value = properties;
    configLoadedKeys.value = Object.keys(configuration);
    configOriginal.value = configuration;

    for (const property of properties) {
      const name = property.name!;
      configForm[name] = getInitialConfigValue(property, configuration);
      configTouched[name] = false;
    }
  } catch (error) {
    configError.value = getErrorMessage(error, t('provider.configLoadFailed'));
  } finally {
    configLoading.value = false;
  }
}

async function submitConfiguration() {
  if (!props.account?.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  if (!validateConfigurationForm()) {
    return;
  }

  const payload = buildConfigurationPayload();
  if (Object.keys(payload).length === 0) {
    message.info(t('provider.configNoChanges'));
    return;
  }

  try {
    await saveConfigurationMutation.mutateAsync({
      id: props.account.id,
      payload,
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.configSaveFailed')));
  }
}

async function startDeviceAuth() {
  if (!props.account?.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  try {
    await startDeviceAuthMutation.mutateAsync(props.account.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.deviceAuthActionFailed')));
  }
}

async function pollDeviceAuth() {
  if (!props.account?.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  try {
    await pollDeviceAuthMutation.mutateAsync(props.account.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.deviceAuthActionFailed')));
  }
}

async function cancelDeviceAuth() {
  if (!props.account?.id) {
    message.error(t('provider.validationAccount'));
    return;
  }

  try {
    await cancelDeviceAuthMutation.mutateAsync(props.account.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('provider.deviceAuthActionFailed')));
  }
}

function resetConfigState() {
  configLoading.value = false;
  configProperties.value = [];
  configError.value = null;
  configLoadedKeys.value = [];
  configOriginal.value = {};
  configDeviceCode.value = null;
  configDeviceStatus.value = null;
  clearDynamicState(configForm);
  clearDynamicState(configTouched);
}

function clearDynamicState(record: Record<string, ConfigFieldValue | boolean>) {
  for (const key of Object.keys(record)) {
    delete record[key];
  }
}

function toConfigurationObject(value: unknown) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return {};
  }

  return { ...(value as Record<string, unknown>) };
}

function getInitialConfigValue(
  property: ConfigurationProperty,
  configuration: Record<string, unknown>,
): ConfigFieldValue {
  const name = property.name ?? '';

  if (Object.prototype.hasOwnProperty.call(configuration, name)) {
    return coerceConfigValue(property, configuration[name]);
  }

  if (property.default_value !== undefined) {
    return coerceConfigValue(property, property.default_value);
  }

  if (property.type === CONFIG_TYPE_BOOLEAN) {
    return false;
  }

  if (
    property.type === CONFIG_TYPE_INT64 ||
    property.type === CONFIG_TYPE_DOUBLE
  ) {
    return null;
  }

  return '';
}

function coerceConfigValue(
  property: ConfigurationProperty,
  value: unknown,
): ConfigFieldValue {
  if (property.type === CONFIG_TYPE_BOOLEAN) {
    return Boolean(value);
  }

  if (
    property.type === CONFIG_TYPE_INT64 ||
    property.type === CONFIG_TYPE_DOUBLE
  ) {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsed = typeof value === 'number' ? value : Number(value);
    if (Number.isNaN(parsed)) {
      return null;
    }

    return property.type === CONFIG_TYPE_INT64 ? Math.trunc(parsed) : parsed;
  }

  return value === null || value === undefined ? '' : String(value);
}

function normalizeConfigValue(
  property: ConfigurationProperty,
  value: unknown,
): boolean | number | string | null {
  if (property.type === CONFIG_TYPE_BOOLEAN) {
    return Boolean(value);
  }

  if (
    property.type === CONFIG_TYPE_INT64 ||
    property.type === CONFIG_TYPE_DOUBLE
  ) {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsed = typeof value === 'number' ? value : Number(value);
    if (Number.isNaN(parsed)) {
      return null;
    }

    return property.type === CONFIG_TYPE_INT64 ? Math.trunc(parsed) : parsed;
  }

  if (value === null || value === undefined || value === '') {
    return null;
  }

  return String(value);
}

function validateConfigurationForm() {
  for (const property of configProperties.value) {
    if (!property.required || !property.name) {
      continue;
    }

    if (isSensitiveProperty(property) && !configTouched[property.name]) {
      continue;
    }

    const value = normalizeConfigValue(property, configForm[property.name]);
    if (value === null || value === '') {
      message.error(
        t('provider.validationConfigField', {
          field: formatConfigLabel(property.name),
        }),
      );
      return false;
    }
  }

  return true;
}

function buildConfigurationPayload() {
  const payload: Record<string, boolean | number | string | null> = {};

  for (const property of configProperties.value) {
    const name = property.name;
    if (!name) {
      continue;
    }

    const touched = !!configTouched[name];
    const currentValue = normalizeConfigValue(property, configForm[name]);
    const hasOriginal = Object.prototype.hasOwnProperty.call(
      configOriginal.value,
      name,
    );

    if (!hasOriginal) {
      if (isSensitiveProperty(property)) {
        if (touched) {
          payload[name] = currentValue;
        }

        continue;
      }

      if (touched) {
        payload[name] = currentValue;
      }

      continue;
    }

    const originalValue = normalizeConfigValue(property, configOriginal.value[name]);
    if (originalValue !== currentValue) {
      payload[name] = currentValue;
    }
  }

  return payload;
}

function hasLoadedConfigKey(name: string) {
  return configLoadedKeys.value.includes(name);
}

function isSensitiveProperty(property: ConfigurationProperty) {
  if (!property.name) {
    return false;
  }

  return property.type === CONFIG_TYPE_STRING && !hasLoadedConfigKey(property.name);
}

function isBooleanProperty(property: ConfigurationProperty) {
  return property.type === CONFIG_TYPE_BOOLEAN;
}

function isIntegerProperty(property: ConfigurationProperty) {
  return property.type === CONFIG_TYPE_INT64;
}

function isNumberProperty(property: ConfigurationProperty) {
  return (
    property.type === CONFIG_TYPE_INT64 || property.type === CONFIG_TYPE_DOUBLE
  );
}

function getStringConfigValue(name: string) {
  const value = configForm[name];
  return typeof value === 'string' ? value : value === null || value === undefined ? '' : String(value);
}

function setStringConfigValue(name: string, value: string) {
  configForm[name] = value;
  configTouched[name] = true;
}

function getBooleanConfigValue(name: string) {
  return Boolean(configForm[name]);
}

function setBooleanConfigValue(name: string, value: boolean) {
  configForm[name] = value;
  configTouched[name] = true;
}

function getNumberConfigValue(name: string) {
  const value = configForm[name];
  return typeof value === 'number' ? value : null;
}

function setNumberConfigValue(name: string, value: null | number) {
  configForm[name] = value;
  configTouched[name] = true;
}

function formatConfigLabel(name: string) {
  return name
    .split('_')
    .filter(Boolean)
    .map((part) => {
      const lower = part.toLowerCase();

      if (['api', 'id', 'uri', 'url'].includes(lower)) {
        return lower.toUpperCase();
      }

      return lower.charAt(0).toUpperCase() + lower.slice(1);
    })
    .join(' ');
}

function getConfigFieldHint(property: ConfigurationProperty) {
  const hints: string[] = [];

  if (property.required) {
    hints.push(t('provider.configRequiredHint'));
  }

  if (isSensitiveProperty(property)) {
    hints.push(t('provider.configSecretHint'));
  }

  if (
    property.default_value !== undefined &&
    property.default_value !== null &&
    property.default_value !== ''
  ) {
    hints.push(
      t('provider.configDefaultHint', {
        value: formatConfigDefaultValue(property.default_value),
      }),
    );
  }

  return hints.join(' · ');
}

function formatConfigDefaultValue(value: unknown) {
  if (typeof value === 'boolean') {
    return value ? 'true' : 'false';
  }

  if (typeof value === 'number' || typeof value === 'string') {
    return String(value);
  }

  return JSON.stringify(value);
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <a-modal
    :cancel-text="t('provider.cancel')"
    :confirm-loading="
      editorSubmitting ||
      createAccountMutation.isPending.value ||
      updateAccountMutation.isPending.value
    "
    :ok-text="mode === 'create' ? t('provider.create') : t('provider.save')"
    :open="editorOpen"
    :title="mode === 'create' ? t('provider.createTitle') : t('provider.editTitle')"
    wrap-class-name="provider-account-editor-modal"
    destroy-on-close
    @cancel="closeEditor"
    @ok="submitEditor"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('provider.name')">
        <a-input
          v-model:value="editorForm.name"
          :placeholder="t('provider.namePlaceholder')"
        />
      </a-form-item>

      <a-form-item :label="t('provider.provider')">
        <a-select
          v-model:value="editorForm.protocolType"
          :disabled="mode === 'edit'"
          :options="providerOptions"
          :placeholder="t('provider.providerPlaceholder')"
        />
      </a-form-item>

      <a-form-item :label="t('provider.status')">
        <a-switch
          v-model:checked="editorForm.isEnabled"
          :checked-children="t('provider.enabledState')"
          :un-checked-children="t('provider.disabledState')"
        />
      </a-form-item>
    </a-form>
  </a-modal>

  <a-drawer
    :open="configOpen"
    :title="configDrawerTitle"
    :width="680"
    root-class-name="provider-account-config-drawer"
    destroy-on-close
    @close="closeConfigEditor"
  >
    <template #extra>
      <a-space>
        <a-button @click="closeConfigEditor">
          {{ t('provider.cancel') }}
        </a-button>
        <a-button :disabled="configLoading" @click="reloadConfiguration">
          {{ t('common.refresh') }}
        </a-button>
        <a-button
          type="primary"
          :loading="saveConfigurationMutation.isPending.value"
          @click="submitConfiguration"
        >
          {{ t('provider.saveConfig') }}
        </a-button>
      </a-space>
    </template>

    <a-spin :spinning="configLoading">
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

        <a-alert
          v-if="configError"
          type="error"
          show-icon
          :message="configError"
        />

        <section
          v-if="supportsDeviceAuth"
          class="rounded-2xl border border-border/70 bg-background/75 p-4"
        >
          <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
            <div>
              <h4 class="text-sm font-semibold">{{ t('provider.deviceAuthTitle') }}</h4>
              <p class="mt-1 text-xs leading-6 text-muted-foreground">
                {{ t('provider.deviceAuthDescription') }}
              </p>
            </div>
            <a-space wrap>
              <a-button
                :loading="startDeviceAuthMutation.isPending.value"
                @click="startDeviceAuth"
              >
                {{ t('provider.startDeviceAuth') }}
              </a-button>
              <a-button
                v-if="configDeviceCode"
                :loading="pollDeviceAuthMutation.isPending.value"
                @click="pollDeviceAuth"
              >
                {{ t('provider.pollDeviceAuth') }}
              </a-button>
              <a-button
                v-if="configDeviceCode"
                danger
                :loading="cancelDeviceAuthMutation.isPending.value"
                @click="cancelDeviceAuth"
              >
                {{ t('provider.cancelDeviceAuth') }}
              </a-button>
            </a-space>
          </div>

          <div v-if="configDeviceCode" class="mt-4 grid gap-3 md:grid-cols-2">
            <div class="rounded-xl border border-border/60 bg-background p-3">
              <div class="text-xs text-muted-foreground">
                {{ t('provider.verificationUri') }}
              </div>
              <div class="mt-1 break-all text-sm font-medium">
                {{ configDeviceCode.verificationUri || '--' }}
              </div>
            </div>
            <div class="rounded-xl border border-border/60 bg-background p-3">
              <div class="text-xs text-muted-foreground">
                {{ t('provider.userCode') }}
              </div>
              <div class="mt-1 text-lg font-semibold tracking-[0.2em]">
                {{ configDeviceCode.userCode || '--' }}
              </div>
            </div>
          </div>

          <a-alert
            v-if="configDeviceStatus?.status || configDeviceStatus?.message"
            class="mt-4"
            type="info"
            show-icon
            :message="
              configDeviceStatus?.message ||
              `${t('provider.deviceAuthStatus')}: ${configDeviceStatus?.status ?? '--'}`
            "
          />
        </section>

        <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
          <div class="mb-4">
            <h4 class="text-sm font-semibold">{{ t('provider.configSectionTitle') }}</h4>
            <p class="mt-1 text-xs leading-6 text-muted-foreground">
              {{ t('provider.configSectionDescription') }}
            </p>
          </div>

          <a-empty
            v-if="!configError && configProperties.length === 0"
            :description="t('provider.noConfigProperties')"
          />

          <a-form v-else layout="vertical">
            <div class="grid gap-4 md:grid-cols-2">
              <a-form-item
                v-for="property in configProperties"
                :key="property.name"
                :label="formatConfigLabel(property.name ?? '')"
                :required="property.required"
              >
                <a-switch
                  v-if="isBooleanProperty(property)"
                  :checked="getBooleanConfigValue(property.name ?? '')"
                  :checked-children="t('provider.enabledState')"
                  :un-checked-children="t('provider.disabledState')"
                  @update:checked="
                    setBooleanConfigValue(property.name ?? '', $event)
                  "
                />

                <a-input-number
                  v-else-if="isNumberProperty(property)"
                  :precision="isIntegerProperty(property) ? 0 : undefined"
                  :value="getNumberConfigValue(property.name ?? '')"
                  class="w-full"
                  @update:value="
                    setNumberConfigValue(
                      property.name ?? '',
                      typeof $event === 'number' ? $event : null,
                    )
                  "
                />

                <a-input-password
                  v-else-if="isSensitiveProperty(property)"
                  :placeholder="t('provider.secretPlaceholder')"
                  :value="getStringConfigValue(property.name ?? '')"
                  @update:value="
                    setStringConfigValue(property.name ?? '', $event ?? '')
                  "
                />

                <a-input
                  v-else
                  :placeholder="formatConfigLabel(property.name ?? '')"
                  :value="getStringConfigValue(property.name ?? '')"
                  @update:value="
                    setStringConfigValue(property.name ?? '', $event ?? '')
                  "
                />

                <div
                  v-if="getConfigFieldHint(property)"
                  class="mt-1 text-xs text-muted-foreground"
                >
                  {{ getConfigFieldHint(property) }}
                </div>
              </a-form-item>
            </div>
          </a-form>
        </section>
      </div>
    </a-spin>
  </a-drawer>
</template>

<style>
.provider-account-editor-modal .ant-modal-content,
.provider-account-config-drawer .ant-drawer-content,
.provider-account-config-drawer .ant-drawer-header {
  background: hsl(var(--background)) !important;
  color: hsl(var(--foreground)) !important;
}

.provider-account-editor-modal .ant-modal-header,
.provider-account-config-drawer .ant-drawer-header {
  background: transparent !important;
  border-bottom: 1px solid hsl(var(--border) / 0.7) !important;
}

.provider-account-editor-modal .ant-modal-footer,
.provider-account-config-drawer .ant-drawer-footer {
  border-top: 1px solid hsl(var(--border) / 0.7) !important;
}

.provider-account-editor-modal .ant-modal-title,
.provider-account-editor-modal .ant-modal-close,
.provider-account-editor-modal .ant-modal-close-x,
.provider-account-editor-modal .ant-modal-body,
.provider-account-editor-modal .ant-form-item-label > label,
.provider-account-config-drawer .ant-drawer-title,
.provider-account-config-drawer .ant-drawer-close,
.provider-account-config-drawer .ant-drawer-body,
.provider-account-config-drawer .ant-form-item-label > label {
  color: hsl(var(--foreground)) !important;
}
</style>
