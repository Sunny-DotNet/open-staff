<script setup lang="ts">
import type {
  CreateProjectInput,
  ProjectDto,
  ProviderAccountDto,
  UpdateProjectInput,
} from '@openstaff/api';

import {
  postApiProjects,
  putApiProjectsById,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref, watch } from 'vue';

import { t } from '@/i18n';

const props = defineProps<{
  editorOpen: boolean;
  mode: 'create' | 'edit';
  project: null | ProjectDto;
  providers: ProviderAccountDto[];
}>();

const emit = defineEmits<{
  (event: 'saved'): void;
  (event: 'update:editorOpen', value: boolean): void;
}>();

const LANGUAGE_OPTIONS = [
  { label: 'zh-CN', value: 'zh-CN' },
  { label: 'en-US', value: 'en-US' },
];

const submitting = ref(false);
const form = reactive({
  defaultModelName: '',
  defaultProviderId: undefined as string | undefined,
  description: '',
  language: 'zh-CN',
  name: '',
});

const drawerTitle = computed(() =>
  props.mode === 'create' ? t('project.createTitle') : t('project.editTitle'),
);

const providerOptions = computed(() =>
  props.providers
    .filter((provider) => provider.id)
    .map((provider) => ({
      label: provider.name ?? provider.protocolType ?? provider.id ?? '--',
      value: provider.id ?? '',
    })),
);

const createProjectMutation = useMutation({
  mutationFn: async (payload: CreateProjectInput) =>
    unwrapClientEnvelope(await postApiProjects({ body: payload })),
  onSuccess: () => {
    message.success(t('project.createSuccess'));
    emit('saved');
    closeDrawer();
  },
});

const updateProjectMutation = useMutation({
  mutationFn: async ({
    id,
    payload,
  }: {
    id: string;
    payload: UpdateProjectInput;
  }) =>
    unwrapClientEnvelope(
      await putApiProjectsById({
        body: payload,
        path: { id },
      }),
    ),
  onSuccess: () => {
    message.success(t('project.updateSuccess'));
    emit('saved');
    closeDrawer();
  },
});

watch(
  () => [props.editorOpen, props.mode, props.project?.id] as const,
  ([open]) => {
    if (!open) {
      return;
    }

    form.name = props.project?.name ?? '';
    form.description = props.project?.description ?? '';
    form.language = props.project?.language ?? 'zh-CN';
    form.defaultProviderId = props.project?.defaultProviderId ?? undefined;
    form.defaultModelName = props.project?.defaultModelName ?? '';

    if (props.mode === 'create') {
      form.description = '';
      form.defaultModelName = '';
      form.defaultProviderId = undefined;
      form.language = 'zh-CN';
      form.name = '';
    }
  },
  { immediate: true },
);

function closeDrawer() {
  emit('update:editorOpen', false);
  submitting.value = false;
}

async function submit() {
  if (!form.name.trim()) {
    message.error(t('project.validationName'));
    return;
  }

  submitting.value = true;

  try {
    if (props.mode === 'create') {
      await createProjectMutation.mutateAsync({
        defaultModelName: form.defaultModelName.trim() || undefined,
        defaultProviderId: form.defaultProviderId || undefined,
        description: form.description.trim() || undefined,
        language: form.language || undefined,
        name: form.name.trim(),
      });
      return;
    }

    if (!props.project?.id) {
      message.error(t('project.validationProject'));
      return;
    }

    await updateProjectMutation.mutateAsync({
      id: props.project.id,
      payload: {
        defaultModelName: form.defaultModelName.trim() || null,
        defaultProviderId: form.defaultProviderId || null,
        description: form.description.trim() || null,
        language: form.language || null,
        name: form.name.trim(),
      },
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  } finally {
    submitting.value = false;
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
    :open="editorOpen"
    :title="drawerTitle"
    :width="520"
    destroy-on-close
    @close="closeDrawer"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('project.name')" required>
        <a-input
          v-model:value="form.name"
          :placeholder="t('project.namePlaceholder')"
        />
      </a-form-item>

      <a-form-item :label="t('project.description')">
        <a-textarea
          v-model:value="form.description"
          :auto-size="{ minRows: 3, maxRows: 6 }"
          :placeholder="t('project.descriptionPlaceholder')"
        />
      </a-form-item>

      <div class="grid gap-4 md:grid-cols-2">
        <a-form-item :label="t('project.language')">
          <a-select
            v-model:value="form.language"
            :options="LANGUAGE_OPTIONS"
          />
        </a-form-item>

        <a-form-item :label="t('project.provider')">
          <a-select
            v-model:value="form.defaultProviderId"
            allow-clear
            :options="providerOptions"
            :placeholder="t('project.providerPlaceholder')"
          />
        </a-form-item>
      </div>

      <a-form-item :label="t('project.model')">
        <a-input
          v-model:value="form.defaultModelName"
          :placeholder="t('project.modelPlaceholder')"
        />
      </a-form-item>
    </a-form>

    <template #footer>
      <div class="flex justify-end gap-2">
        <a-button @click="closeDrawer">
          {{ t('common.cancel') }}
        </a-button>
        <a-button
          type="primary"
          :loading="submitting"
          @click="submit"
        >
          {{ t('common.save') }}
        </a-button>
      </div>
    </template>
  </a-drawer>
</template>
