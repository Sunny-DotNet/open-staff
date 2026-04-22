<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query';
import { computed, watch } from 'vue';

import { appLocale } from '@/app-preferences';
import { t } from '@/i18n';

import {
  formatSoulDisplayValue,
  formatSoulDisplayValues,
  isSoulFormValueEqual,
  loadSoulOptions,
  normalizeSoulFormValue,
  type SoulFormValue,
  withSelectedSoulOptions,
} from './soul-options';

const soul = defineModel<SoulFormValue>({ required: true });

const soulOptionsQuery = useQuery({
  queryKey: ['agent-soul-options', computed(() => appLocale.value)],
  queryFn: () => loadSoulOptions(appLocale.value),
  staleTime: 5 * 60 * 1000,
});

const traitOptions = computed(() =>
  withSelectedSoulOptions(
    soulOptionsQuery.data.value?.traits,
    soul.value.traits,
  ).map((item) => ({
    label: item.label ?? item.key ?? '',
    value: item.key ?? item.label ?? '',
  })),
);

const styleOptions = computed(() =>
  withSelectedSoulOptions(
    soulOptionsQuery.data.value?.styles,
    soul.value.style ? [soul.value.style] : [],
  ).map((item) => ({
    label: item.label ?? item.key ?? '',
    value: item.key ?? item.label ?? '',
  })),
);

const attitudeOptions = computed(() =>
  withSelectedSoulOptions(
    soulOptionsQuery.data.value?.attitudes,
    soul.value.attitudes,
  ).map((item) => ({
    label: item.label ?? item.key ?? '',
    value: item.key ?? item.label ?? '',
  })),
);

const previewText = computed(() => {
  const parts: string[] = [];
  const catalog = soulOptionsQuery.data.value;
  if (soul.value.traits.length) {
    parts.push(
      `${t('role.soulTraits')}：${formatSoulDisplayValues(
        soul.value.traits,
        catalog?.traits,
      ).join('、')}`,
    );
  }
  if (soul.value.style) {
    parts.push(
      `${t('role.soulStyle')}：${formatSoulDisplayValue(
        soul.value.style,
        catalog?.styles,
      )}`,
    );
  }
  if (soul.value.attitudes.length) {
    parts.push(
      `${t('role.soulAttitudes')}：${formatSoulDisplayValues(
        soul.value.attitudes,
        catalog?.attitudes,
      ).join('、')}`,
    );
  }
  if (soul.value.custom.trim()) {
    parts.push(`${t('role.soulCustom')}：${soul.value.custom.trim()}`);
  }
  return parts.join('；');
});

const optionsErrorText = computed(() =>
  soulOptionsQuery.error.value instanceof Error &&
  soulOptionsQuery.error.value.message
    ? soulOptionsQuery.error.value.message
    : t('role.soulOptionsLoadFailed'),
);

watch(
  [() => soulOptionsQuery.data.value, soul],
  ([catalog, current]) => {
    if (!catalog) {
      return;
    }

    const normalized = normalizeSoulFormValue(current, catalog);
    if (!isSoulFormValueEqual(current, normalized)) {
      soul.value = normalized;
    }
  },
  { immediate: true, deep: true },
);
</script>

<template>
  <a-alert
    v-if="soulOptionsQuery.isError.value"
    class="mb-4"
    show-icon
    type="warning"
    :message="optionsErrorText"
  />

  <div class="grid gap-4 md:grid-cols-2">
    <a-form-item :label="t('role.soulTraits')">
      <a-select
        v-model:value="soul.traits"
        :loading="soulOptionsQuery.isPending.value"
        mode="multiple"
        :options="traitOptions"
        :placeholder="t('role.soulTraitsPlaceholder')"
        option-filter-prop="label"
        show-search
      />
    </a-form-item>
    <a-form-item :label="t('role.soulAttitudes')">
      <a-select
        v-model:value="soul.attitudes"
        :loading="soulOptionsQuery.isPending.value"
        mode="multiple"
        :options="attitudeOptions"
        :placeholder="t('role.soulAttitudesPlaceholder')"
        option-filter-prop="label"
        show-search
      />
    </a-form-item>
  </div>

  <a-form-item :label="t('role.soulStyle')">
    <a-select
      v-model:value="soul.style"
      allow-clear
      :loading="soulOptionsQuery.isPending.value"
      :options="styleOptions"
      :placeholder="t('role.soulStylePlaceholder')"
      option-filter-prop="label"
      show-search
    />
  </a-form-item>

  <a-form-item :label="t('role.soulCustom')">
    <a-textarea
      v-model:value="soul.custom"
      :rows="4"
      :placeholder="t('role.soulCustomPlaceholder')"
    />
  </a-form-item>

  <a-alert
    v-if="previewText"
    class="mt-2"
    show-icon
    type="info"
    :message="t('role.soulPreview')"
    :description="previewText"
  />
</template>
