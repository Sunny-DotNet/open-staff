<script setup lang="ts">
import type { ProviderAccountDto, AgentRoleDto, AgentSoulDto } from '@openstaff/api';

import { useMutation, useQuery } from '@tanstack/vue-query';
import {
  getApiProviderAccountsByIdModels,
  postApiAgentRoles,
  putApiAgentRolesById,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

import SoulConfigSection from './SoulConfigSection.vue';
import {
  buildSoulPayloadFromForm,
  createEmptySoulForm,
  soulDtoToFormValue,
} from './soul-options';

const props = defineProps<{
  mode: 'create' | 'edit';
  open: boolean;
  providers: ProviderAccountDto[];
  role: AgentRoleDto | null;
}>();

const emit = defineEmits<{
  (event: 'saved'): void;
  (event: 'update:open', value: boolean): void;
}>();

const fileInputRef = ref<HTMLInputElement | null>(null);
const modelSectionTab = ref('settings');
const soulForm = ref(createEmptySoulForm());
const submitting = ref(false);
const form = reactive({
  avatar: '',
  config: '',
  description: '',
  jobTitle: '',
  modelName: '',
  modelProviderId: '',
  name: '',
});

const enabledProviders = computed(() =>
  props.providers.filter((provider) => provider.isEnabled),
);

const providerModelsQuery = useQuery({
  queryKey: ['provider-account-models', () => form.modelProviderId],
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProviderAccountsByIdModels({
        path: { id: form.modelProviderId },
      }),
    ),
  enabled: computed(() => !!form.modelProviderId && props.open),
});

const createRoleMutation = useMutation({
  mutationFn: async () =>
    unwrapClientEnvelope(
      await postApiAgentRoles({
        body: buildCreatePayload(),
      }),
    ),
  onSuccess: () => {
    message.success(t('role.createSuccess'));
    emit('saved');
    closeDrawer();
  },
});

const updateRoleMutation = useMutation({
  mutationFn: async () => {
    if (!props.role?.id) {
      throw new Error(t('role.validationRole'));
    }

    return unwrapClientEnvelope(
      await putApiAgentRolesById({
        body: buildUpdatePayload(),
        path: { id: props.role.id },
      }),
    );
  },
  onSuccess: () => {
    message.success(t('role.updateSuccess'));
    emit('saved');
    closeDrawer();
  },
});

watch(
  () => [props.open, props.mode, props.role?.id] as const,
  ([open]) => {
    if (!open) {
      return;
    }

    if (props.mode === 'create') {
      modelSectionTab.value = 'settings';
      resetForm();
      return;
    }

    modelSectionTab.value = 'settings';
    loadRoleIntoForm(props.role);
  },
  { immediate: true },
);

function loadRoleIntoForm(role: AgentRoleDto | null) {
  if (!role) {
    resetForm();
    return;
  }

  form.avatar = role.avatar ?? '';
  form.config = role.config ?? '';
  form.description = role.description ?? '';
  form.jobTitle = role.jobTitle ?? '';
  form.modelName = role.modelName ?? '';
  form.modelProviderId = role.modelProviderId ?? '';
  form.name = role.name ?? '';
  soulForm.value = soulDtoToFormValue(role.soul);
}

function resetForm() {
  form.avatar = '';
  form.config = '';
  form.description = '';
  form.jobTitle = '';
  form.modelName = '';
  form.modelProviderId = '';
  form.name = '';
  soulForm.value = createEmptySoulForm();
}

function closeDrawer() {
  emit('update:open', false);
  submitting.value = false;
}

function openAvatarPicker() {
  fileInputRef.value?.click();
}

function clearAvatar() {
  form.avatar = '';
}

function onAvatarFileChange(event: Event) {
  const input = event.target as HTMLInputElement | null;
  const file = input?.files?.[0];
  if (!file) {
    return;
  }

  const reader = new FileReader();
  reader.onerror = () => {
    if (input) {
      input.value = '';
    }
    message.error(t('role.avatarReadFailed'));
  };
  reader.onload = () => {
    const result = reader.result;
    if (typeof result !== 'string') {
      message.error(t('role.avatarReadFailed'));
      return;
    }

    const image = new Image();
    image.onerror = () => {
      if (input) {
        input.value = '';
      }
      message.error(t('role.avatarReadFailed'));
    };
    image.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = 128;
      canvas.height = 128;
      const context = canvas.getContext('2d');
      if (!context) {
        message.error(t('role.avatarReadFailed'));
        return;
      }

      context.drawImage(image, 0, 0, 128, 128);
      form.avatar = canvas.toDataURL('image/png');
      if (input) {
        input.value = '';
      }
    };
    image.src = result;
  };
  reader.readAsDataURL(file);
}

async function submit() {
  const roleName = form.name.trim();

  if (!roleName) {
    message.error(t('role.validationName'));
    return;
  }

  if (!isValidJson(form.config)) {
    message.error(t('role.validationConfigJson'));
    return;
  }

  submitting.value = true;

  try {
    if (props.mode === 'create') {
      await createRoleMutation.mutateAsync();
      return;
    }

    await updateRoleMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('role.actionFailed')));
  } finally {
    submitting.value = false;
  }
}

function buildCreatePayload() {
  return {
    avatar: form.avatar || undefined,
    config: form.config.trim() || undefined,
    description: form.description.trim() || undefined,
    jobTitle: form.jobTitle.trim() || undefined,
    modelName: form.modelName || undefined,
    modelProviderId: form.modelProviderId || undefined,
    name: form.name.trim(),
    soul: buildSoulPayload(),
  };
}

function buildUpdatePayload() {
  return {
    avatar: form.avatar,
    config: form.config.trim(),
    description: form.description.trim(),
    jobTitle: form.jobTitle.trim(),
    modelName: form.modelName,
    modelProviderId: form.modelProviderId,
    name: form.name.trim(),
    soul: buildSoulPayload(),
  };
}

function buildSoulPayload(): AgentSoulDto | undefined {
  return buildSoulPayloadFromForm(soulForm.value);
}

function isValidJson(value: string) {
  if (!value.trim()) {
    return true;
  }

  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
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
    :title="mode === 'create' ? t('role.createTitle') : t('role.editTitle')"
    :width="620"
    root-class-name="agent-role-editor-drawer"
    destroy-on-close
    @close="closeDrawer"
  >
    <template #extra>
      <a-space>
        <a-button @click="closeDrawer">
          {{ t('role.cancel') }}
        </a-button>
        <a-button
          type="primary"
          :loading="
            submitting ||
            createRoleMutation.isPending.value ||
            updateRoleMutation.isPending.value
          "
          @click="submit"
        >
          {{ mode === 'create' ? t('role.create') : t('role.save') }}
        </a-button>
      </a-space>
    </template>

    <a-form layout="vertical">
      <section class="mb-5 rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-3 text-sm font-semibold">{{ t('role.basicSection') }}</div>
        <div class="mb-4 flex items-center gap-4">
          <div
            class="flex size-18 items-center justify-center overflow-hidden rounded-2xl border border-dashed border-border bg-muted"
          >
            <img
              v-if="form.avatar"
              :src="form.avatar"
              alt=""
              class="size-full object-cover"
            />
            <span v-else class="text-xl text-muted-foreground">🧠</span>
          </div>
          <div class="space-y-2">
            <a-space>
              <a-button size="small" @click="openAvatarPicker">
                {{ t('role.uploadAvatar') }}
              </a-button>
              <a-button v-if="form.avatar" danger size="small" @click="clearAvatar">
                {{ t('role.removeAvatar') }}
              </a-button>
            </a-space>
            <div class="text-xs text-muted-foreground">
              {{ t('role.avatarHint') }}
            </div>
            <input
              ref="fileInputRef"
              accept="image/png,image/jpeg,image/webp"
              class="hidden"
              type="file"
              @change="onAvatarFileChange"
            />
          </div>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <a-form-item :label="t('role.name')" required>
            <a-input v-model:value="form.name" :placeholder="t('role.namePlaceholder')" />
          </a-form-item>
          <a-form-item :label="t('role.jobTitle')">
            <a-input v-model:value="form.jobTitle" :placeholder="t('role.jobTitlePlaceholder')" />
          </a-form-item>
          <a-form-item :label="t('role.modelProvider')">
            <a-select
              v-model:value="form.modelProviderId"
              allow-clear
              :options="
                enabledProviders.map((provider) => ({
                  label: provider.name ?? provider.id ?? '--',
                  value: provider.id ?? '',
                }))
              "
              :placeholder="t('role.modelProviderPlaceholder')"
            />
          </a-form-item>
        </div>

        <a-form-item :label="t('role.description')">
          <a-textarea
            v-model:value="form.description"
            :rows="3"
            :placeholder="t('role.descriptionPlaceholder')"
          />
        </a-form-item>
      </section>

      <section class="mb-5 rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-3 text-sm font-semibold">{{ t('role.modelSection') }}</div>
        <a-tabs v-model:activeKey="modelSectionTab">
          <a-tab-pane key="settings" :tab="t('role.modelSettingsTab')">
            <div class="grid gap-4 md:grid-cols-2">
              <a-form-item :label="t('role.model')">
                <a-select
                  v-model:value="form.modelName"
                  allow-clear
                  show-search
                  :disabled="!form.modelProviderId"
                  :loading="providerModelsQuery.isFetching.value"
                  :options="
                    (providerModelsQuery.data.value ?? []).map((model) => ({
                      label: model.id ?? '--',
                      value: model.id ?? '',
                    }))
                  "
                  :placeholder="t('role.modelPlaceholder')"
                />
              </a-form-item>
            </div>
          </a-tab-pane>
          <a-tab-pane key="raw-config" :tab="t('role.rawConfigTab')">
            <a-form-item :label="t('role.rawConfig')">
              <a-textarea
                v-model:value="form.config"
                :rows="6"
                :placeholder="t('role.configPlaceholder')"
              />
            </a-form-item>
          </a-tab-pane>
        </a-tabs>
      </section>

      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-3 text-sm font-semibold">{{ t('role.soulSection') }}</div>
        <SoulConfigSection v-model="soulForm" />
      </section>
    </a-form>
  </a-drawer>
</template>

<style>
.agent-role-editor-drawer .ant-drawer-content,
.agent-role-editor-drawer .ant-drawer-header,
.agent-role-editor-drawer .ant-drawer-body {
  background: hsl(var(--background)) !important;
  color: hsl(var(--foreground)) !important;
}

.agent-role-editor-drawer .ant-drawer-header {
  border-bottom-color: hsl(var(--border)) !important;
}

.agent-role-editor-drawer .ant-drawer-title,
.agent-role-editor-drawer .ant-drawer-close,
.agent-role-editor-drawer .ant-drawer-close-x,
.agent-role-editor-drawer .ant-form-item-label > label,
.agent-role-editor-drawer .ant-tabs-tab-btn,
.agent-role-editor-drawer .ant-select-selection-item,
.agent-role-editor-drawer .ant-select-item-option-content {
  color: hsl(var(--foreground)) !important;
}

.agent-role-editor-drawer .ant-tabs-tab-active .ant-tabs-tab-btn,
.agent-role-editor-drawer .ant-tabs-tab:hover .ant-tabs-tab-btn {
  color: hsl(var(--primary)) !important;
}

.agent-role-editor-drawer .ant-tabs-nav::before {
  border-bottom-color: hsl(var(--border)) !important;
}

.agent-role-editor-drawer .ant-select-selection-placeholder,
.agent-role-editor-drawer .ant-form-item-extra,
.agent-role-editor-drawer .ant-form-item-explain,
.agent-role-editor-drawer .text-muted-foreground {
  color: hsl(var(--muted-foreground)) !important;
}

.agent-role-editor-drawer .ant-input,
.agent-role-editor-drawer textarea.ant-input,
.agent-role-editor-drawer .ant-input-affix-wrapper,
.agent-role-editor-drawer .ant-select-selector {
  background: hsl(var(--card) / 0.78) !important;
  border-color: hsl(var(--border)) !important;
  color: hsl(var(--foreground)) !important;
}

.agent-role-editor-drawer .ant-input::placeholder,
.agent-role-editor-drawer textarea.ant-input::placeholder {
  color: hsl(var(--muted-foreground)) !important;
}

.agent-role-editor-drawer .ant-input-clear-icon,
.agent-role-editor-drawer .ant-select-arrow {
  color: hsl(var(--muted-foreground)) !important;
}
</style>
