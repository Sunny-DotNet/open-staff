<script setup lang="ts">
import type { SystemSettingsDto } from '@openstaff/api';

import { Page } from '@vben/common-ui';
import { useMutation, useQuery } from '@tanstack/vue-query';
import {
  getApiSettingsSystem,
  putApiSettingsSystem,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, reactive, watch } from 'vue';

import { t } from '@/i18n';

type SettingsForm = {
  autoApproveProjectGroupCapabilities: boolean;
  defaultMaxTokens: number;
  defaultTemperature: number;
  language: string;
  responseStyle: string;
  teamDescription: string;
  teamName: string;
  timezone: string;
  userName: string;
};

const languageOptions = [
  { label: '简体中文 (zh-CN)', value: 'zh-CN' },
  { label: 'English (en-US)', value: 'en-US' },
];

const timezoneOptions = [
  { label: 'Asia/Shanghai', value: 'Asia/Shanghai' },
  { label: 'UTC', value: 'UTC' },
  { label: 'Asia/Tokyo', value: 'Asia/Tokyo' },
  { label: 'Europe/Berlin', value: 'Europe/Berlin' },
  { label: 'America/New_York', value: 'America/New_York' },
];

const form = reactive(createDefaultSettings());

const settingsQuery = useQuery({
  queryKey: ['settings', 'system'],
  queryFn: async () => unwrapClientEnvelope(await getApiSettingsSystem()),
});

watch(
  () => settingsQuery.data.value,
  (value) => {
    Object.assign(form, normalizeSettings(value));
  },
  { immediate: true },
);

const saveMutation = useMutation({
  mutationFn: async () =>
    putApiSettingsSystem({
      body: toPayload(form),
    }),
  onSuccess: async () => {
    message.success(t('settings.saveSuccess'));
    await settingsQuery.refetch();
  },
});

const isDirty = computed(
  () => serializeSettings(form) !== serializeSettings(normalizeSettings(settingsQuery.data.value)),
);

const autoApproveSummary = computed(() =>
  form.autoApproveProjectGroupCapabilities
    ? t('settings.autoApproveEnabled')
    : t('settings.autoApproveDisabled'),
);

async function saveSettings() {
  if (!form.teamName.trim()) {
    message.error(t('settings.validationTeamName'));
    return;
  }

  if (!form.userName.trim()) {
    message.error(t('settings.validationUserName'));
    return;
  }

  try {
    await saveMutation.mutateAsync();
  } catch (error) {
    message.error(resolveErrorMessage(error, t('settings.saveFailed')));
  }
}

function createDefaultSettings(): SettingsForm {
  return {
    autoApproveProjectGroupCapabilities: false,
    defaultMaxTokens: 4096,
    defaultTemperature: 0.7,
    language: 'zh-CN',
    responseStyle: 'balanced',
    teamDescription: '',
    teamName: 'OpenStaff',
    timezone: 'Asia/Shanghai',
    userName: '主人',
  };
}

function normalizeSettings(value?: null | SystemSettingsDto): SettingsForm {
  const defaults = createDefaultSettings();
  return {
    autoApproveProjectGroupCapabilities:
      value?.autoApproveProjectGroupCapabilities ?? defaults.autoApproveProjectGroupCapabilities,
    defaultMaxTokens:
      typeof value?.defaultMaxTokens === 'number'
        ? value.defaultMaxTokens
        : defaults.defaultMaxTokens,
    defaultTemperature:
      typeof value?.defaultTemperature === 'number'
        ? value.defaultTemperature
        : defaults.defaultTemperature,
    language: value?.language || defaults.language,
    responseStyle: value?.responseStyle || defaults.responseStyle,
    teamDescription: value?.teamDescription ?? defaults.teamDescription,
    teamName: value?.teamName?.trim() || defaults.teamName,
    timezone: value?.timezone || defaults.timezone,
    userName: value?.userName?.trim() || defaults.userName,
  };
}

function toPayload(value: SettingsForm): SystemSettingsDto {
  return {
    autoApproveProjectGroupCapabilities: value.autoApproveProjectGroupCapabilities,
    defaultMaxTokens: value.defaultMaxTokens,
    defaultTemperature: value.defaultTemperature,
    language: value.language.trim() || 'zh-CN',
    responseStyle: value.responseStyle.trim() || 'balanced',
    teamDescription: value.teamDescription.trim(),
    teamName: value.teamName.trim() || 'OpenStaff',
    timezone: value.timezone.trim() || 'Asia/Shanghai',
    userName: value.userName.trim() || '主人',
  };
}

function serializeSettings(value: SettingsForm) {
  return JSON.stringify(toPayload(value));
}

function resolveErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <Page :title="t('settings.title')" content-class="settings-page space-y-5">
    <template #extra>
      <a-space>
        <a-button
          :loading="settingsQuery.isFetching.value && !saveMutation.isPending.value"
          @click="settingsQuery.refetch()"
        >
          {{ t('common.refresh') }}
        </a-button>
        <a-button
          type="primary"
          :disabled="!isDirty"
          :loading="saveMutation.isPending.value"
          @click="saveSettings"
        >
          {{ t('common.save') }}
        </a-button>
      </a-space>
    </template>

    <a-alert
      v-if="settingsQuery.isError.value"
      show-icon
      type="warning"
      :message="t('common.requestFailed', { status: 'settings' })"
    />

    <div class="grid gap-5 xl:grid-cols-[1.1fr_0.9fr]">
      <section class="card-box p-5">
        <div class="mb-5">
          <h3 class="text-base font-semibold text-foreground">
            {{ t('settings.globalSection') }}
          </h3>
          <p class="mt-2 text-sm text-muted-foreground">
            {{ t('settings.globalSectionDescription') }}
          </p>
        </div>

        <a-spin :spinning="settingsQuery.isLoading.value && !settingsQuery.data.value">
          <a-form layout="vertical">
            <div class="grid gap-4 md:grid-cols-2">
              <a-form-item :label="t('settings.teamName')" class="md:col-span-2">
                <a-input v-model:value="form.teamName" :placeholder="t('settings.teamNamePlaceholder')" />
              </a-form-item>

              <a-form-item :label="t('settings.teamDescription')" class="md:col-span-2">
                <a-textarea
                  v-model:value="form.teamDescription"
                  :auto-size="{ minRows: 3, maxRows: 5 }"
                  :placeholder="t('settings.teamDescriptionPlaceholder')"
                />
              </a-form-item>

              <a-form-item :label="t('settings.userName')">
                <a-input v-model:value="form.userName" :placeholder="t('settings.userNamePlaceholder')" />
              </a-form-item>

              <a-form-item :label="t('settings.language')">
                <a-select
                  v-model:value="form.language"
                  :options="languageOptions"
                  :placeholder="t('settings.languagePlaceholder')"
                />
              </a-form-item>

              <a-form-item :label="t('settings.timezone')" class="md:col-span-2">
                <a-select
                  v-model:value="form.timezone"
                  show-search
                  :options="timezoneOptions"
                  :placeholder="t('settings.timezonePlaceholder')"
                />
              </a-form-item>
            </div>
          </a-form>
        </a-spin>
      </section>

      <div class="space-y-5">
        <section class="card-box p-5">
          <div class="mb-5">
            <h3 class="text-base font-semibold text-foreground">
              {{ t('settings.approvalSection') }}
            </h3>
            <p class="mt-2 text-sm text-muted-foreground">
              {{ t('settings.approvalSectionDescription') }}
            </p>
          </div>

          <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
            <div class="flex items-start justify-between gap-4">
              <div class="min-w-0">
                <div class="text-sm font-medium text-foreground">
                  {{ t('settings.autoApproveProjectGroupCapabilities') }}
                </div>
                <p class="mt-2 text-sm leading-6 text-muted-foreground">
                  {{ t('settings.autoApproveProjectGroupCapabilitiesHint') }}
                </p>
              </div>
              <a-switch v-model:checked="form.autoApproveProjectGroupCapabilities" />
            </div>

            <div class="mt-4 rounded-xl bg-background px-3 py-2 text-xs text-muted-foreground">
              {{ autoApproveSummary }}
            </div>
          </div>
        </section>

        <section class="card-box p-5">
          <div class="mb-4">
            <h3 class="text-base font-semibold text-foreground">
              {{ t('settings.effectiveRulesTitle') }}
            </h3>
            <p class="mt-2 text-sm text-muted-foreground">
              {{ t('settings.effectiveRulesDescription') }}
            </p>
          </div>

          <div class="space-y-3">
            <div class="rounded-2xl border border-border/70 bg-background/75 px-4 py-3">
              <div class="text-xs text-muted-foreground">{{ t('settings.currentTeam') }}</div>
              <div class="mt-1 text-sm font-medium text-foreground">{{ form.teamName || '--' }}</div>
            </div>
            <div class="rounded-2xl border border-border/70 bg-background/75 px-4 py-3">
              <div class="text-xs text-muted-foreground">{{ t('settings.currentLocale') }}</div>
              <div class="mt-1 text-sm font-medium text-foreground">
                {{ form.language }} / {{ form.timezone }}
              </div>
            </div>
            <div class="rounded-2xl border border-border/70 bg-background/75 px-4 py-3">
              <div class="text-xs text-muted-foreground">{{ t('settings.currentAddressing') }}</div>
              <div class="mt-1 text-sm font-medium text-foreground">{{ form.userName || '--' }}</div>
            </div>
          </div>
        </section>
      </div>
    </div>
  </Page>
</template>

<style scoped>
.settings-page :deep(.ant-form-item-label > label) {
  color: hsl(var(--foreground));
}
</style>
