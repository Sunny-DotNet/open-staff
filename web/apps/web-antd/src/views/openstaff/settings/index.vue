<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Row,
  Space,
  Switch,
  Typography,
} from 'ant-design-vue';

import { getSettingsApi, updateSettingsApi } from '#/api/openstaff/settings';

const router = useRouter();
const loading = ref(false);
const saving = ref(false);
const settings = ref({
  defaultModel: '',
  language: 'zh-CN',
  maxTokens: 4096,
  enableAutoSave: true,
});

async function fetchSettings() {
  loading.value = true;
  try {
    const data = await getSettingsApi();
    if (data) {
      Object.assign(settings.value, data);
    }
  } catch {
    // 使用默认值
  } finally {
    loading.value = false;
  }
}

async function handleSave() {
  saving.value = true;
  try {
    await updateSettingsApi(settings.value as any);
    message.success('设置已保存');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

onMounted(fetchSettings);
</script>

<template>
  <Page title="系统设置">
    <Row :gutter="[16, 16]">
      <Col :span="24" :lg="12">
        <Card title="通用设置">
          <Form layout="vertical">
            <FormItem label="默认模型">
              <Input
                v-model:value="settings.defaultModel"
                placeholder="gpt-4o"
              />
            </FormItem>
            <FormItem label="语言">
              <Input v-model:value="settings.language" placeholder="zh-CN" />
            </FormItem>
            <FormItem label="最大 Token 数">
              <InputNumber
                v-model:value="settings.maxTokens"
                :min="256"
                :max="128000"
                style="width: 100%"
              />
            </FormItem>
            <FormItem label="自动保存">
              <Switch v-model:checked="settings.enableAutoSave" />
            </FormItem>
            <FormItem>
              <Button type="primary" :loading="saving" @click="handleSave">
                保存设置
              </Button>
            </FormItem>
          </Form>
        </Card>
      </Col>

      <Col :span="24" :lg="12">
        <Card title="模型供应商">
          <div style="text-align: center; padding: 24px 0">
            <Typography.Text type="secondary" style="display: block; margin-bottom: 16px">
              供应商管理已迁移至独立页面，支持完整的 CRUD 操作。
            </Typography.Text>
            <Button type="primary" @click="router.push('/providers')">
              ☁️ 前往供应商管理
            </Button>
          </div>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
