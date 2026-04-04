<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Row,
  Select,
  Switch,
  Table,
} from 'ant-design-vue';

import { $t } from '#/locales';

interface ModelProvider {
  id: string;
  name: string;
  type: string;
  apiKey: string;
  baseUrl: string;
  enabled: boolean;
}

const modelProviders = ref<ModelProvider[]>([
  {
    id: '1',
    name: 'OpenAI',
    type: 'openai',
    apiKey: 'sk-***',
    baseUrl: 'https://api.openai.com/v1',
    enabled: true,
  },
  {
    id: '2',
    name: 'Azure OpenAI',
    type: 'azure',
    apiKey: 'az-***',
    baseUrl: 'https://xxx.openai.azure.com',
    enabled: false,
  },
  {
    id: '3',
    name: '本地模型',
    type: 'ollama',
    apiKey: '',
    baseUrl: 'http://localhost:11434',
    enabled: true,
  },
]);

const providerColumns = [
  { title: $t('openstaff.settings.providerName'), dataIndex: 'name', key: 'name' },
  { title: $t('openstaff.settings.providerType'), dataIndex: 'type', key: 'type' },
  { title: $t('openstaff.settings.baseUrl'), dataIndex: 'baseUrl', key: 'baseUrl' },
  { title: $t('openstaff.settings.enabled'), dataIndex: 'enabled', key: 'enabled' },
  { title: '操作', key: 'action', width: 160 },
];

const defaultModel = ref('gpt-4');
const maxTokens = ref(4096);
const enableAutoSave = ref(true);
const language = ref('zh-CN');

const languageOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en-US' },
];

const modalVisible = ref(false);
const editingProvider = ref<ModelProvider | null>(null);
const formState = ref({
  name: '',
  type: 'openai',
  apiKey: '',
  baseUrl: '',
  enabled: true,
});

function handleAdd() {
  editingProvider.value = null;
  formState.value = { name: '', type: 'openai', apiKey: '', baseUrl: '', enabled: true };
  modalVisible.value = true;
}

function handleEdit(record: ModelProvider) {
  editingProvider.value = record;
  formState.value = { ...record };
  modalVisible.value = true;
}

function handleDelete(record: ModelProvider) {
  modelProviders.value = modelProviders.value.filter((p) => p.id !== record.id);
}

function handleModalOk() {
  if (editingProvider.value) {
    const idx = modelProviders.value.findIndex(
      (p) => p.id === editingProvider.value!.id,
    );
    if (idx >= 0) {
      modelProviders.value[idx] = { ...modelProviders.value[idx]!, ...formState.value };
    }
  } else {
    modelProviders.value.push({
      id: String(Date.now()),
      ...formState.value,
    });
  }
  modalVisible.value = false;
}

function handleToggle(record: ModelProvider) {
  record.enabled = !record.enabled;
}
</script>

<template>
  <Page :title="$t('openstaff.settings.title')">
    <Row :gutter="16">
      <Col :span="24" class="mb-4">
        <Card :title="$t('openstaff.settings.modelProviders')">
          <template #extra>
            <Button type="primary" @click="handleAdd">
              {{ $t('openstaff.settings.add') }}
            </Button>
          </template>
          <Table
            :columns="providerColumns"
            :data-source="modelProviders"
            :pagination="false"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'enabled'">
                <Switch
                  :checked="record.enabled"
                  @change="() => handleToggle(record)"
                />
              </template>
              <template v-if="column.key === 'action'">
                <Button size="small" type="link" @click="handleEdit(record)">
                  {{ $t('openstaff.settings.edit') }}
                </Button>
                <Button
                  danger
                  size="small"
                  type="link"
                  @click="handleDelete(record)"
                >
                  {{ $t('openstaff.settings.delete') }}
                </Button>
              </template>
            </template>
          </Table>
        </Card>
      </Col>

      <Col :span="12">
        <Card :title="$t('openstaff.settings.globalSettings')">
          <Form layout="vertical">
            <FormItem :label="$t('openstaff.settings.defaultModel')">
              <Select v-model:value="defaultModel">
                <Select.Option value="gpt-4">GPT-4</Select.Option>
                <Select.Option value="gpt-3.5-turbo">
                  GPT-3.5 Turbo
                </Select.Option>
                <Select.Option value="claude-3">Claude 3</Select.Option>
              </Select>
            </FormItem>
            <FormItem :label="$t('openstaff.settings.maxTokens')">
              <InputNumber
                v-model:value="maxTokens"
                :min="256"
                :max="128000"
                class="w-full"
              />
            </FormItem>
            <FormItem :label="$t('openstaff.settings.autoSave')">
              <Switch v-model:checked="enableAutoSave" />
            </FormItem>
            <FormItem>
              <Button type="primary">
                {{ $t('openstaff.settings.save') }}
              </Button>
            </FormItem>
          </Form>
        </Card>
      </Col>

      <Col :span="12">
        <Card :title="$t('openstaff.settings.language')">
          <Form layout="vertical">
            <FormItem :label="$t('openstaff.settings.language')">
              <Select v-model:value="language" :options="languageOptions" />
            </FormItem>
            <FormItem>
              <Button type="primary">
                {{ $t('openstaff.settings.save') }}
              </Button>
            </FormItem>
          </Form>
        </Card>
      </Col>
    </Row>

    <!-- 模型提供商编辑弹窗 -->
    <Modal
      v-model:open="modalVisible"
      :title="
        editingProvider
          ? $t('openstaff.settings.edit')
          : $t('openstaff.settings.add')
      "
      @ok="handleModalOk"
    >
      <Form layout="vertical">
        <FormItem :label="$t('openstaff.settings.providerName')">
          <Input v-model:value="formState.name" />
        </FormItem>
        <FormItem :label="$t('openstaff.settings.providerType')">
          <Select v-model:value="formState.type">
            <Select.Option value="openai">OpenAI</Select.Option>
            <Select.Option value="azure">Azure OpenAI</Select.Option>
            <Select.Option value="anthropic">Anthropic</Select.Option>
            <Select.Option value="ollama">Ollama</Select.Option>
          </Select>
        </FormItem>
        <FormItem :label="$t('openstaff.settings.apiKey')">
          <Input v-model:value="formState.apiKey" type="password" />
        </FormItem>
        <FormItem :label="$t('openstaff.settings.baseUrl')">
          <Input v-model:value="formState.baseUrl" />
        </FormItem>
        <FormItem :label="$t('openstaff.settings.enabled')">
          <Switch v-model:checked="formState.enabled" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
