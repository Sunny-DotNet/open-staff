<script lang="ts" setup>
import type { SettingsApi } from '#/api/openstaff/settings';

import { onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Card,
  Col,
  Divider,
  Form,
  FormItem,
  Input,
  InputPassword,
  message,
  Modal,
  Radio,
  RadioButton,
  RadioGroup,
  Row,
  Space,
  Switch,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  cancelDeviceAuthApi,
  initiateDeviceAuthApi,
  pollDeviceAuthApi,
  updateModelProviderApi,
} from '#/api/openstaff/settings';
import { useSettingsStore } from '#/store/openstaff';

const settingsStore = useSettingsStore();

// 编辑状态
const editingId = ref<null | string>(null);
const editForm = ref<{
  apiKey: string;
  apiKeyEnvVar: string;
  apiKeyMode: 'env' | 'input';
  baseUrl: string;
}>({
  apiKeyMode: 'env',
  apiKeyEnvVar: '',
  apiKey: '',
  baseUrl: '',
});
const saving = ref(false);

// 设备码授权状态
const deviceAuth = ref<{
  expiresIn: number;
  interval: number;
  polling: boolean;
  providerId: string | null;
  status: string;
  userCode: string;
  verificationUri: string;
}>({
  providerId: null,
  userCode: '',
  verificationUri: '',
  expiresIn: 0,
  interval: 5,
  polling: false,
  status: '',
});
let pollTimer: ReturnType<typeof setTimeout> | null = null;

const providerIcons: Record<string, string> = {
  openai: '🤖',
  google: '🔷',
  anthropic: '🟠',
  github_copilot: '🐙',
};

const defaultEnvVars: Record<string, string> = {
  openai: 'OPENAI_API_KEY',
  google: 'GOOGLE_API_KEY',
  anthropic: 'ANTHROPIC_API_KEY',
};

onMounted(async () => {
  await settingsStore.fetchModelProviders();
});

onUnmounted(() => {
  stopPolling();
});

function startEdit(provider: SettingsApi.ModelProvider) {
  editingId.value = provider.id;
  editForm.value = {
    apiKeyMode: provider.apiKeyMode === 'device' ? 'env' : provider.apiKeyMode || 'env',
    apiKeyEnvVar:
      provider.apiKeyEnvVar ||
      defaultEnvVars[provider.providerType] ||
      '',
    apiKey: '',
    baseUrl: provider.baseUrl || '',
  };
}

function cancelEdit() {
  editingId.value = null;
}

async function saveEdit(provider: SettingsApi.ModelProvider) {
  saving.value = true;
  try {
    const data: SettingsApi.UpdateModelProviderParams = {
      baseUrl: editForm.value.baseUrl,
      apiKeyMode: editForm.value.apiKeyMode,
      apiKeyEnvVar:
        editForm.value.apiKeyMode === 'env'
          ? editForm.value.apiKeyEnvVar
          : undefined,
      apiKey:
        editForm.value.apiKeyMode === 'input' && editForm.value.apiKey
          ? editForm.value.apiKey
          : undefined,
    };
    await updateModelProviderApi(provider.id, data);
    await settingsStore.fetchModelProviders();
    editingId.value = null;
    message.success('保存成功');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

async function toggleEnabled(provider: SettingsApi.ModelProvider) {
  try {
    await updateModelProviderApi(provider.id, {
      isEnabled: !provider.isEnabled,
    });
    await settingsStore.fetchModelProviders();
  } catch {
    message.error('操作失败');
  }
}

// ===== GitHub 设备码授权 =====

async function startDeviceAuth(provider: SettingsApi.ModelProvider) {
  try {
    deviceAuth.value = {
      providerId: provider.id,
      userCode: '',
      verificationUri: '',
      expiresIn: 0,
      interval: 5,
      polling: false,
      status: 'initiating',
    };

    const result = await initiateDeviceAuthApi(provider.id);

    deviceAuth.value.userCode = result.userCode;
    deviceAuth.value.verificationUri = result.verificationUri;
    deviceAuth.value.expiresIn = result.expiresIn;
    deviceAuth.value.interval = result.interval;
    deviceAuth.value.status = 'waiting';
    deviceAuth.value.polling = true;

    // 自动打开 GitHub 验证页面
    window.open(result.verificationUri, '_blank');

    // 开始轮询
    startPolling();
  } catch {
    message.error('无法发起设备码授权，请检查网络连接');
    deviceAuth.value.status = '';
    deviceAuth.value.providerId = null;
  }
}

function startPolling() {
  stopPolling();
  const interval = (deviceAuth.value.interval || 5) * 1000;
  pollTimer = setTimeout(pollDeviceAuth, interval);
}

function stopPolling() {
  if (pollTimer) {
    clearTimeout(pollTimer);
    pollTimer = null;
  }
  deviceAuth.value.polling = false;
}

async function pollDeviceAuth() {
  if (!deviceAuth.value.providerId) return;

  try {
    const result = await pollDeviceAuthApi(deviceAuth.value.providerId);

    switch (result.status) {
      case 'success': {
        stopPolling();
        deviceAuth.value.status = 'success';
        message.success('GitHub Copilot 授权成功！');
        await settingsStore.fetchModelProviders();
        // 2 秒后关闭弹窗
        setTimeout(() => {
          closeDeviceAuth();
        }, 2000);
        break;
      }
      case 'pending': {
        // 继续轮询（可能需要调整 interval）
        if (result.interval) {
          deviceAuth.value.interval = result.interval;
        }
        startPolling();
        break;
      }
      case 'expired': {
        stopPolling();
        deviceAuth.value.status = 'expired';
        message.warning('授权已过期，请重新发起');
        break;
      }
      case 'denied': {
        stopPolling();
        deviceAuth.value.status = 'denied';
        message.error('用户拒绝了授权');
        break;
      }
      default: {
        stopPolling();
        deviceAuth.value.status = 'error';
        message.error(result.message || '授权出错');
        break;
      }
    }
  } catch {
    stopPolling();
    deviceAuth.value.status = 'error';
    message.error('轮询授权状态失败');
  }
}

async function closeDeviceAuth() {
  if (
    deviceAuth.value.providerId &&
    deviceAuth.value.polling
  ) {
    await cancelDeviceAuthApi(deviceAuth.value.providerId).catch(() => {});
  }
  stopPolling();
  deviceAuth.value = {
    providerId: null,
    userCode: '',
    verificationUri: '',
    expiresIn: 0,
    interval: 5,
    polling: false,
    status: '',
  };
}

function copyUserCode() {
  navigator.clipboard.writeText(deviceAuth.value.userCode);
  message.success('已复制到剪贴板');
}
</script>

<template>
  <Page title="系统设置">
    <Row :gutter="[16, 16]">
      <Col :span="24">
        <Card title="模型提供商">
          <Row :gutter="[16, 16]">
            <Col
              v-for="provider in settingsStore.modelProviders"
              :key="provider.id"
              :lg="12"
              :span="24"
              :xl="12"
            >
              <Card
                :body-style="{ padding: '16px' }"
                :class="provider.isEnabled ? '' : 'opacity-60'"
                size="small"
              >
                <template #title>
                  <Space>
                    <span class="text-lg">
                      {{ providerIcons[provider.providerType] || '🔌' }}
                    </span>
                    <span>{{ provider.name }}</span>
                    <Tag v-if="provider.isBuiltin" color="blue">内置</Tag>
                    <Badge
                      :color="provider.isEnabled ? 'green' : 'default'"
                      :text="provider.isEnabled ? '已启用' : '已禁用'"
                    />
                  </Space>
                </template>
                <template #extra>
                  <Switch
                    :checked="provider.isEnabled"
                    checked-children="启用"
                    un-checked-children="禁用"
                    @change="() => toggleEnabled(provider)"
                  />
                </template>

                <!-- 查看模式 -->
                <template v-if="editingId !== provider.id">
                  <div class="mb-2">
                    <Typography.Text type="secondary">API 地址：</Typography.Text>
                    <Typography.Text>
                      {{ provider.baseUrl || '默认' }}
                    </Typography.Text>
                  </div>

                  <!-- GitHub Copilot 设备码授权 -->
                  <template v-if="provider.apiKeyMode === 'device'">
                    <div class="mb-2">
                      <Typography.Text type="secondary">
                        认证方式：
                      </Typography.Text>
                      <Tag color="purple">GitHub 设备码授权</Tag>
                      <Tag v-if="provider.hasApiKey" color="green">
                        已授权
                      </Tag>
                      <Tag v-else color="red">未授权</Tag>
                    </div>
                    <div class="mb-3">
                      <Typography.Text type="secondary">
                        默认模型：
                      </Typography.Text>
                      <Typography.Text>
                        {{ provider.defaultModel || '未设置' }}
                      </Typography.Text>
                    </div>
                    <Space>
                      <Button
                        size="small"
                        type="primary"
                        @click="startDeviceAuth(provider)"
                      >
                        🔑 {{ provider.hasApiKey ? '重新授权' : '设备码授权' }}
                      </Button>
                    </Space>
                  </template>

                  <!-- 标准 API Key 认证 -->
                  <template v-else>
                    <div class="mb-2">
                      <Typography.Text type="secondary">
                        API 密钥方式：
                      </Typography.Text>
                      <Tag
                        :color="
                          provider.apiKeyMode === 'env' ? 'green' : 'orange'
                        "
                      >
                        {{
                          provider.apiKeyMode === 'env'
                            ? '环境变量'
                            : '直接输入'
                        }}
                      </Tag>
                      <Typography.Text
                        v-if="provider.apiKeyMode === 'env'"
                        code
                      >
                        {{ provider.apiKeyEnvVar }}
                      </Typography.Text>
                      <Typography.Text
                        v-else-if="provider.hasApiKey"
                        type="secondary"
                      >
                        ••••••••
                      </Typography.Text>
                      <Typography.Text v-else type="danger">
                        未配置
                      </Typography.Text>
                    </div>
                    <div class="mb-3">
                      <Typography.Text type="secondary">
                        默认模型：
                      </Typography.Text>
                      <Typography.Text>
                        {{ provider.defaultModel || '未设置' }}
                      </Typography.Text>
                    </div>
                    <Button
                      size="small"
                      type="primary"
                      ghost
                      @click="startEdit(provider)"
                    >
                      配置
                    </Button>
                  </template>
                </template>

                <!-- 编辑模式（仅非 device 类型）-->
                <template v-else>
                  <Divider style="margin: 8px 0" />
                  <Form :label-col="{ span: 6 }" size="small">
                    <FormItem label="API 地址">
                      <Input
                        v-model:value="editForm.baseUrl"
                        :placeholder="provider.baseUrl || 'https://...'"
                      />
                    </FormItem>
                    <FormItem label="密钥方式">
                      <RadioGroup v-model:value="editForm.apiKeyMode">
                        <RadioButton value="env">环境变量</RadioButton>
                        <RadioButton value="input">直接输入</RadioButton>
                      </RadioGroup>
                    </FormItem>
                    <FormItem
                      v-if="editForm.apiKeyMode === 'env'"
                      label="变量名"
                    >
                      <Input
                        v-model:value="editForm.apiKeyEnvVar"
                        :placeholder="
                          defaultEnvVars[provider.providerType] || 'API_KEY'
                        "
                      />
                    </FormItem>
                    <FormItem v-else label="API 密钥">
                      <InputPassword
                        v-model:value="editForm.apiKey"
                        :placeholder="
                          provider.hasApiKey ? '留空则不修改' : '请输入 API Key'
                        "
                      />
                    </FormItem>
                    <FormItem :wrapper-col="{ offset: 6 }">
                      <Space>
                        <Button
                          :loading="saving"
                          size="small"
                          type="primary"
                          @click="saveEdit(provider)"
                        >
                          保存
                        </Button>
                        <Button size="small" @click="cancelEdit">
                          取消
                        </Button>
                      </Space>
                    </FormItem>
                  </Form>
                </template>
              </Card>
            </Col>
          </Row>
        </Card>
      </Col>
    </Row>

    <!-- GitHub 设备码授权弹窗 -->
    <Modal
      :closable="deviceAuth.status !== 'initiating'"
      :footer="null"
      :mask-closable="false"
      :open="!!deviceAuth.providerId"
      title="🐙 GitHub Copilot 设备码授权"
      @cancel="closeDeviceAuth"
    >
      <!-- 加载中 -->
      <template v-if="deviceAuth.status === 'initiating'">
        <div class="py-8 text-center">
          <Typography.Text>正在连接 GitHub...</Typography.Text>
        </div>
      </template>

      <!-- 等待用户授权 -->
      <template v-else-if="deviceAuth.status === 'waiting'">
        <div class="py-4 text-center">
          <Typography.Paragraph>
            请在浏览器中打开以下网址，输入授权码完成授权：
          </Typography.Paragraph>

          <div class="my-4">
            <Typography.Text type="secondary">验证网址：</Typography.Text>
            <br />
            <Typography.Link
              :href="deviceAuth.verificationUri"
              target="_blank"
            >
              {{ deviceAuth.verificationUri }}
            </Typography.Link>
          </div>

          <div class="my-6">
            <Typography.Text type="secondary">你的授权码：</Typography.Text>
            <div
              class="my-2 cursor-pointer select-all rounded-lg bg-gray-100 px-6 py-4 font-mono text-3xl font-bold tracking-widest"
              @click="copyUserCode"
            >
              {{ deviceAuth.userCode }}
            </div>
            <Button size="small" type="link" @click="copyUserCode">
              📋 点击复制
            </Button>
          </div>

          <div class="mt-4">
            <Badge color="blue" status="processing" text="等待授权中..." />
          </div>
        </div>
      </template>

      <!-- 授权成功 -->
      <template v-else-if="deviceAuth.status === 'success'">
        <div class="py-8 text-center">
          <div class="mb-4 text-5xl">✅</div>
          <Typography.Title :level="4">授权成功！</Typography.Title>
          <Typography.Text type="secondary">
            GitHub Copilot 已成功连接
          </Typography.Text>
        </div>
      </template>

      <!-- 失败/过期 -->
      <template v-else>
        <div class="py-8 text-center">
          <div class="mb-4 text-5xl">
            {{ deviceAuth.status === 'expired' ? '⏰' : '❌' }}
          </div>
          <Typography.Title :level="4">
            {{
              deviceAuth.status === 'expired' ? '授权已过期' : '授权失败'
            }}
          </Typography.Title>
          <Typography.Text type="secondary">
            请关闭弹窗后重新发起授权
          </Typography.Text>
        </div>
      </template>
    </Modal>
  </Page>
</template>
