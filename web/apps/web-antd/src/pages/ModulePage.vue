<script setup lang="ts">
import { Page } from '@vben/common-ui';
import { VbenIcon } from '@vben-core/shadcn-ui';
import { computed } from 'vue';

import { getModuleTitle, t } from '@/i18n';
import { getModuleByKey } from '@/navigation';

const props = defineProps<{
  moduleKey: string;
}>();

const module = computed(() => getModuleByKey(props.moduleKey));
</script>

<template>
  <Page
    v-if="module"
    :title="getModuleTitle(module.key, module.title)"
    content-class="space-y-6"
  >
    <template #extra>
      <span
        class="rounded-full px-3 py-1 text-xs font-medium"
        :class="
          module.status === 'live'
            ? 'bg-success-100 text-success-700'
            : 'bg-warning-100 text-warning-700'
        "
      >
        {{ module.status === 'live' ? t('common.live') : t('common.planned') }}
      </span>
    </template>

    <section class="card-box grid gap-6 p-5 xl:grid-cols-[1.15fr_0.85fr]">
      <div class="flex items-start gap-4">
        <span
          class="flex size-12 shrink-0 items-center justify-center rounded-2xl border border-primary/15 bg-primary-50 text-primary"
        >
          <VbenIcon :icon="module.icon" class="size-6" />
        </span>
        <div>
          <h2 class="text-2xl font-semibold tracking-tight text-foreground">
            {{ getModuleTitle(module.key, module.title) }}
          </h2>
          <div
            class="mt-3 inline-flex rounded-full bg-background px-3 py-1 text-xs text-muted-foreground"
          >
            {{ t('module.placeholder') }}
          </div>
        </div>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
          <div class="text-xs text-muted-foreground">{{ t('module.controller') }}</div>
          <div class="mt-2 text-sm font-semibold">{{ module.controller }}</div>
        </div>
        <div class="rounded-2xl border border-border/70 bg-background/75 p-4">
          <div class="text-xs text-muted-foreground">{{ t('module.endpoints') }}</div>
          <div class="mt-2 text-sm font-semibold">{{ module.endpoints.length }}</div>
        </div>
      </div>
    </section>

    <section class="card-box p-5">
      <div class="mb-4 flex items-center justify-between gap-4">
        <h3 class="text-base font-semibold">{{ t('module.endpoints') }}</h3>
        <span class="rounded-full bg-background px-3 py-1 text-xs text-muted-foreground">
          {{ module.endpoints.length }}
        </span>
      </div>

      <div class="flex flex-wrap gap-3">
        <span
          v-for="endpoint in module.endpoints"
          :key="endpoint"
          class="rounded-2xl border border-border/70 bg-background px-4 py-2 text-sm text-foreground"
        >
          {{ endpoint }}
        </span>
      </div>
    </section>
  </Page>
</template>
