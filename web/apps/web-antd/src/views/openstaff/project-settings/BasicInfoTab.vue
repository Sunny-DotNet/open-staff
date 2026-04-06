<script lang="ts" setup>
import { ref, watch } from 'vue';

import {
  Button,
  Card,
  Form,
  Input,
  message,
  Select,
} from 'ant-design-vue';

import { updateProjectApi } from '#/api/openstaff/project';

const props = defineProps<{
  projectId: string;
  saving: boolean;
  initialData: { name: string; description: string; language: string };
}>();

const emit = defineEmits<{
  (e: 'update:saving', value: boolean): void;
}>();

const form = ref({ ...props.initialData });

const languageOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en' },
  { label: '日本語', value: 'ja' },
];

watch(
  () => props.initialData,
  (val) => {
    form.value = { ...val };
  },
);

async function save() {
  emit('update:saving', true);
  try {
    await updateProjectApi(props.projectId, {
      name: form.value.name.trim(),
      description: form.value.description.trim() || undefined,
      language: form.value.language,
    });
    message.success('基本信息已保存');
  } catch {
    message.error('保存失败');
  } finally {
    emit('update:saving', false);
  }
}
</script>

<template>
  <Card :bordered="false" style="max-width: 600px">
    <Form layout="vertical">
      <Form.Item label="项目名称" required>
        <Input
          v-model:value="form.name"
          placeholder="输入项目名称"
        />
      </Form.Item>
      <Form.Item label="描述">
        <Input.TextArea
          v-model:value="form.description"
          :rows="4"
          placeholder="描述项目目标和背景"
        />
      </Form.Item>
      <Form.Item label="交互语言">
        <Select
          v-model:value="form.language"
          :options="languageOptions"
          style="width: 200px"
        />
      </Form.Item>
      <Form.Item>
        <Button
          :loading="saving"
          type="primary"
          @click="save"
        >
          保存基本信息
        </Button>
      </Form.Item>
    </Form>
  </Card>
</template>
