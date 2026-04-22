<script setup lang="ts">
import type { CheckpointDto, FileNodeDto } from '@openstaff/api';
import type { TreeProps } from 'ant-design-vue';

import {
  getApiProjectsByProjectIdCheckpoints,
  getApiProjectsByProjectIdFiles,
  getApiProjectsByProjectIdFilesContent,
  getApiProjectsByProjectIdFilesDiff,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useQuery } from '@tanstack/vue-query';
import { computed, ref, watch } from 'vue';

import { t } from '@/i18n';

const props = defineProps<{
  projectId: string;
}>();

type FileTreeNode = NonNullable<TreeProps['treeData']>[number];

const selectedFilePath = ref('');
const selectedCommitSha = ref<string>();

const filesQuery = useQuery({
  queryKey: ['projects', 'files-tree', computed(() => props.projectId)],
  enabled: computed(() => !!props.projectId),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdFiles({
        path: { projectId: props.projectId },
      }),
    ),
});

const checkpointsQuery = useQuery({
  queryKey: ['projects', 'checkpoints', computed(() => props.projectId)],
  enabled: computed(() => !!props.projectId),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdCheckpoints({
        path: { projectId: props.projectId },
      }),
    ),
});

const fileContentQuery = useQuery({
  queryKey: ['projects', 'file-content', computed(() => props.projectId), selectedFilePath],
  enabled: computed(() => !!props.projectId && !!selectedFilePath.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdFilesContent({
        path: { projectId: props.projectId },
        query: { path: selectedFilePath.value },
      }),
    ),
});

const diffQuery = useQuery({
  queryKey: ['projects', 'file-diff', computed(() => props.projectId), selectedCommitSha],
  enabled: computed(() => !!props.projectId && !!selectedCommitSha.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdFilesDiff({
        path: { projectId: props.projectId },
        query: { commitSha: selectedCommitSha.value },
      }),
    ),
});

const treeData = computed<TreeProps['treeData']>(() =>
  (filesQuery.data.value ?? []).map((node) => toTreeNode(node)),
);
const checkpoints = computed<CheckpointDto[]>(() => checkpointsQuery.data.value ?? []);
const selectedCheckpoint = computed(() =>
  checkpoints.value.find((item) => item.commitSha === selectedCommitSha.value) ?? null,
);

watch(
  () => filesQuery.data.value,
  (nodes) => {
    if (!selectedFilePath.value) {
      selectedFilePath.value = findFirstFile(nodes ?? []) ?? '';
    }
  },
  { immediate: true },
);

watch(
  () => checkpointsQuery.data.value,
  (items) => {
    if (!selectedCommitSha.value) {
      selectedCommitSha.value = items?.[0]?.commitSha ?? undefined;
    }
  },
  { immediate: true },
);

function onSelectFile(selectedKeys: (number | string)[]) {
  const next = selectedKeys[0];
  if (typeof next === 'string' && next) {
    selectedFilePath.value = next;
  }
}

function toTreeNode(node: FileNodeDto): FileTreeNode {
  return {
    children: node.children?.map((child) => toTreeNode(child)) ?? undefined,
    isLeaf: !node.isDirectory,
    key: node.path ?? node.name ?? crypto.randomUUID(),
    title: node.name ?? node.path ?? '--',
  };
}

function findFirstFile(nodes: FileNodeDto[]): string | null {
  for (const node of nodes) {
    if (!node.isDirectory && node.path) {
      return node.path;
    }

    const childMatch = findFirstFile(node.children ?? []);
    if (childMatch) {
      return childMatch;
    }
  }

  return null;
}
</script>

<template>
  <div class="grid gap-5 xl:grid-cols-[0.32fr_0.68fr]">
    <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-base font-semibold">{{ t('project.fileExplorerTitle') }}</h3>
        <a-button :loading="filesQuery.isFetching.value" size="small" @click="filesQuery.refetch()">
          {{ t('common.refresh') }}
        </a-button>
      </div>

      <a-tree
        :selected-keys="selectedFilePath ? [selectedFilePath] : []"
        :tree-data="treeData"
        block-node
        default-expand-all
        @select="onSelectFile"
      />
    </section>

    <div class="space-y-5">
      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">{{ selectedFilePath || t('project.selectFile') }}</h3>
          </div>
          <a-button
            :disabled="!selectedFilePath"
            :loading="fileContentQuery.isFetching.value"
            size="small"
            @click="fileContentQuery.refetch()"
          >
            {{ t('common.refresh') }}
          </a-button>
        </div>

        <a-empty
          v-if="!selectedFilePath"
          :description="t('project.selectFile')"
        />
        <pre
          v-else
          class="max-h-[420px] overflow-auto rounded-2xl bg-background p-4 text-xs leading-6 text-foreground"
        ><code>{{ fileContentQuery.data.value?.content || '' }}</code></pre>
      </section>

      <section class="rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">{{ t('project.diffTitle') }}</h3>
            <p class="mt-1 text-xs text-muted-foreground">
              {{ selectedCheckpoint?.name || t('project.diffDescription') }}
            </p>
          </div>
          <div class="flex items-center gap-2">
            <a-select
              v-model:value="selectedCommitSha"
              allow-clear
              style="width: 260px"
              :options="
                checkpoints.map((checkpoint) => ({
                  label:
                    checkpoint.name ||
                    checkpoint.commitSha ||
                    checkpoint.id ||
                    '--',
                  value: checkpoint.commitSha || '',
                }))
              "
              :placeholder="t('project.checkpointPlaceholder')"
            />
            <a-button
              :disabled="!selectedCommitSha"
              :loading="diffQuery.isFetching.value"
              size="small"
              @click="diffQuery.refetch()"
            >
              {{ t('common.refresh') }}
            </a-button>
          </div>
        </div>

        <a-empty
          v-if="!selectedCommitSha"
          :description="t('project.noCheckpoints')"
        />
        <pre
          v-else
          class="max-h-[360px] overflow-auto rounded-2xl bg-background p-4 text-xs leading-6 text-foreground"
        ><code>{{ diffQuery.data.value?.diff || '' }}</code></pre>
      </section>
    </div>
  </div>
</template>
