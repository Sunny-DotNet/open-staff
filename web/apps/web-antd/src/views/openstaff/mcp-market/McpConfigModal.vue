<script lang="ts" setup>
import type { McpApi } from '#/api/openstaff/mcp';

import { computed, reactive, ref, watch } from 'vue';

import {
  Alert,
  Button,
  Collapse,
  CollapsePanel,
  Divider,
  Empty,
  Form,
  FormItem,
  Input,
  List,
  ListItem,
  ListItemMeta,
  message,
  Modal,
  Popconfirm,
  Select,
  SelectOption,
  Space,
  Spin,
  Switch,
  Tag,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import {
  createMcpConfigApi,
  deleteMcpConfigApi,
  getMcpConfigsApi,
  testMcpConnectionApi,
  updateMcpConfigApi,
} from '#/api/openstaff/mcp';

// ===== Props & Emits =====
const props = defineProps<{
  open: boolean;
  server: McpApi.McpServer | null;
}>();

const emit = defineEmits<{
  saved: [];
  'update:open': [value: boolean];
}>();

// ===== 状态 =====
const configs = ref<McpApi.McpServerConfig[]>([]);
const loading = ref(false);
const saving = ref(false);
const showForm = ref(false);
const editingConfig = ref<McpApi.McpServerConfig | null>(null);

// 测试连接
const testing = ref<string | null>(null);
const testResult = ref<McpApi.TestConnectionResult | null>(null);
const testConfigId = ref<string | null>(null);

// ===== 表单 =====
interface FormState {
  name: string;
  description: string;
  transportType: 'stdio' | 'http';
  command: string;
  args: string;
  envVars: string;
  url: string;
  headers: string;
  authConfig: string;
  isEnabled: boolean;
}

const formState = reactive<FormState>({
  name: '',
  description: '',
  transportType: 'stdio',
  command: '',
  args: '',
  envVars: '',
  url: '',
  headers: '',
  authConfig: '',
  isEnabled: true,
});

// ===== 计算属性 =====
const modalTitle = computed(() => {
  if (!props.server) return 'MCP 配置';
  return `${props.server.name} - 配置管理`;
});

// ===== 监听 =====
watch(
  () => props.open,
  async (val) => {
    if (val && props.server) {
      await fetchConfigs();
      resetForm();
      showForm.value = false;
      testResult.value = null;
      testConfigId.value = null;
    }
  },
);

// ===== 数据加载 =====
async function fetchConfigs() {
  if (!props.server) return;
  loading.value = true;
  try {
    configs.value = await getMcpConfigsApi(props.server.id);
  } catch {
    configs.value = [];
  } finally {
    loading.value = false;
  }
}

// ===== 表单操作 =====
function resetForm() {
  editingConfig.value = null;
  formState.name = '';
  formState.description = '';
  formState.transportType = props.server?.transportType ?? 'stdio';
  formState.command = '';
  formState.args = '';
  formState.envVars = '';
  formState.url = '';
  formState.headers = '';
  formState.authConfig = '';
  formState.isEnabled = true;

  // 尝试从 defaultConfig 预填充
  if (props.server?.defaultConfig) {
    try {
      const defaults = JSON.parse(props.server.defaultConfig);
      if (defaults.command) formState.command = defaults.command;
      if (defaults.args) {
        formState.args = Array.isArray(defaults.args)
          ? defaults.args.join('\n')
          : String(defaults.args);
      }
      if (defaults.url) formState.url = defaults.url;
      if (defaults.env) {
        formState.envVars = Object.entries(defaults.env)
          .map(([k, v]) => `${k}=${v}`)
          .join('\n');
      }
      if (defaults.headers) {
        formState.headers = Object.entries(defaults.headers)
          .map(([k, v]) => `${k}=${v}`)
          .join('\n');
      }
    } catch {
      // ignore parse errors
    }
  }
}

function openAddForm() {
  resetForm();
  showForm.value = true;
}

function openEditForm(config: McpApi.McpServerConfig) {
  editingConfig.value = config;
  formState.name = config.name;
  formState.description = config.description ?? '';
  formState.transportType = config.transportType;
  formState.isEnabled = config.isEnabled;
  formState.authConfig = config.authConfig ?? '';

  // 解析 connectionConfig
  try {
    const conn = JSON.parse(config.connectionConfig);
    if (config.transportType === 'stdio') {
      formState.command = conn.command ?? '';
      formState.args = Array.isArray(conn.args)
        ? conn.args.join('\n')
        : (conn.args ?? '');
      formState.url = '';
      formState.headers = '';
    } else {
      formState.url = conn.url ?? '';
      formState.headers = conn.headers
        ? Object.entries(conn.headers)
            .map(([k, v]) => `${k}=${v}`)
            .join('\n')
        : '';
      formState.command = '';
      formState.args = '';
    }
  } catch {
    formState.command = '';
    formState.args = '';
    formState.url = '';
    formState.headers = '';
  }

  // 解析 environmentVariables
  if (config.environmentVariables) {
    try {
      const env = JSON.parse(config.environmentVariables);
      formState.envVars = Object.entries(env)
        .map(([k, v]) => `${k}=${v}`)
        .join('\n');
    } catch {
      formState.envVars = config.environmentVariables;
    }
  } else {
    formState.envVars = '';
  }

  showForm.value = true;
}

function buildConnectionConfig(): string {
  if (formState.transportType === 'stdio') {
    const config: Record<string, unknown> = { command: formState.command };
    if (formState.args.trim()) {
      config.args = formState.args
        .split('\n')
        .map((s) => s.trim())
        .filter(Boolean);
    }
    return JSON.stringify(config);
  } else {
    const config: Record<string, unknown> = { url: formState.url };
    if (formState.headers.trim()) {
      config.headers = parseKeyValuePairs(formState.headers);
    }
    return JSON.stringify(config);
  }
}

function parseKeyValuePairs(text: string): Record<string, string> {
  const result: Record<string, string> = {};
  for (const line of text.split('\n')) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    const idx = trimmed.indexOf('=');
    if (idx > 0) {
      result[trimmed.slice(0, idx).trim()] = trimmed.slice(idx + 1).trim();
    }
  }
  return result;
}

function buildEnvVars(): string | undefined {
  if (!formState.envVars.trim()) return undefined;
  return JSON.stringify(parseKeyValuePairs(formState.envVars));
}

async function handleSave() {
  if (!formState.name.trim()) {
    message.warning('请输入配置名称');
    return;
  }
  if (formState.transportType === 'stdio' && !formState.command.trim()) {
    message.warning('请输入命令');
    return;
  }
  if (formState.transportType === 'http' && !formState.url.trim()) {
    message.warning('请输入 URL');
    return;
  }

  saving.value = true;
  try {
    const connectionConfig = buildConnectionConfig();
    const environmentVariables = buildEnvVars();
    const authConfig = formState.authConfig.trim() || undefined;

    if (editingConfig.value) {
      await updateMcpConfigApi(editingConfig.value.id, {
        name: formState.name,
        description: formState.description || undefined,
        transportType: formState.transportType,
        connectionConfig,
        environmentVariables,
        authConfig,
        isEnabled: formState.isEnabled,
      });
      message.success('配置已更新');
    } else {
      await createMcpConfigApi({
        mcpServerId: props.server!.id,
        name: formState.name,
        description: formState.description || undefined,
        transportType: formState.transportType,
        connectionConfig,
        environmentVariables,
        authConfig,
      });
      message.success('配置已创建');
    }

    showForm.value = false;
    await fetchConfigs();
    emit('saved');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('保存失败: ' + msg);
  } finally {
    saving.value = false;
  }
}

async function handleDelete(configId: string) {
  try {
    await deleteMcpConfigApi(configId);
    message.success('配置已删除');
    await fetchConfigs();
    emit('saved');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('删除失败: ' + msg);
  }
}

async function handleTest(configId: string) {
  testing.value = configId;
  testResult.value = null;
  testConfigId.value = configId;
  try {
    testResult.value = await testMcpConnectionApi(configId);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    testResult.value = { success: false, message: msg, tools: [] };
  } finally {
    testing.value = null;
  }
}

async function handleToggleEnabled(config: McpApi.McpServerConfig) {
  try {
    await updateMcpConfigApi(config.id, { isEnabled: !config.isEnabled });
    await fetchConfigs();
    emit('saved');
  } catch {
    message.error('操作失败');
  }
}

function handleClose() {
  emit('update:open', false);
}
</script>

<template>
  <Modal
    :open="open"
    :title="modalTitle"
    :footer="null"
    width="720px"
    @cancel="handleClose"
    destroy-on-close
  >
    <Spin :spinning="loading">
      <!-- 已有配置列表 -->
      <div style="margin-bottom: 16px">
        <div
          style="
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 12px;
          "
        >
          <Typography.Text strong style="font-size: 14px">
            已有配置 ({{ configs.length }})
          </Typography.Text>
          <Button type="primary" size="small" @click="openAddForm">
            ＋ 新建配置
          </Button>
        </div>

        <Empty
          v-if="configs.length === 0 && !showForm"
          description="暂无配置，点击「新建配置」添加"
          style="padding: 20px 0"
        />

        <List
          v-else-if="configs.length > 0"
          :data-source="configs"
          size="small"
          bordered
        >
          <template #renderItem="{ item }">
            <ListItem>
              <ListItemMeta>
                <template #title>
                  <Space>
                    <span>{{ item.name }}</span>
                    <Tag>{{ item.transportType }}</Tag>
                    <Tag :color="item.isEnabled ? 'green' : 'default'">
                      {{ item.isEnabled ? '已启用' : '已禁用' }}
                    </Tag>
                  </Space>
                </template>
                <template #description>
                  <span v-if="item.description">{{ item.description }}</span>
                  <span v-else style="color: #bbb">无描述</span>
                </template>
              </ListItemMeta>
              <template #actions>
                <Space>
                  <Switch
                    :checked="item.isEnabled"
                    size="small"
                    @change="handleToggleEnabled(item)"
                  />
                  <Tooltip title="测试连接">
                    <Button
                      size="small"
                      :loading="testing === item.id"
                      @click="handleTest(item.id)"
                    >
                      测试
                    </Button>
                  </Tooltip>
                  <Button
                    size="small"
                    type="link"
                    @click="openEditForm(item)"
                  >
                    编辑
                  </Button>
                  <Popconfirm
                    title="确认删除此配置？"
                    @confirm="handleDelete(item.id)"
                  >
                    <Button size="small" type="link" danger>删除</Button>
                  </Popconfirm>
                </Space>
              </template>
            </ListItem>
          </template>
        </List>

        <!-- 测试结果 -->
        <div
          v-if="testResult && testConfigId"
          style="margin-top: 12px"
        >
          <Alert
            :type="testResult.success ? 'success' : 'error'"
            :message="testResult.success ? '连接成功' : '连接失败'"
            :description="testResult.message"
            show-icon
          />
          <div
            v-if="testResult.success && testResult.tools.length > 0"
            style="margin-top: 8px"
          >
            <Collapse>
              <CollapsePanel
                key="tools"
                :header="`可用工具 (${testResult.tools.length})`"
              >
                <div
                  v-for="tool in testResult.tools"
                  :key="tool.name"
                  style="
                    margin-bottom: 8px;
                    padding: 8px;
                    background: #fafafa;
                    border-radius: 4px;
                  "
                >
                  <Typography.Text strong>{{ tool.name }}</Typography.Text>
                  <br />
                  <Typography.Text type="secondary" style="font-size: 12px">
                    {{ tool.description || '无描述' }}
                  </Typography.Text>
                </div>
              </CollapsePanel>
            </Collapse>
          </div>
        </div>
      </div>

      <!-- 新建/编辑表单 -->
      <template v-if="showForm">
        <Divider />
        <Typography.Title :level="5" style="margin-bottom: 16px">
          {{ editingConfig ? '编辑配置' : '新建配置' }}
        </Typography.Title>

        <Form layout="vertical">
          <FormItem label="配置名称" required>
            <Input
              v-model:value="formState.name"
              placeholder="例如：本地开发配置"
            />
          </FormItem>

          <FormItem label="描述">
            <Input
              v-model:value="formState.description"
              placeholder="可选描述"
            />
          </FormItem>

          <FormItem label="传输类型">
            <Select v-model:value="formState.transportType">
              <SelectOption value="stdio">stdio</SelectOption>
              <SelectOption value="http">HTTP/SSE</SelectOption>
            </Select>
          </FormItem>

          <!-- stdio 配置 -->
          <template v-if="formState.transportType === 'stdio'">
            <FormItem label="命令" required>
              <Input
                v-model:value="formState.command"
                placeholder="例如：npx, uvx, docker"
              />
            </FormItem>

            <FormItem label="参数（每行一个）">
              <Input.TextArea
                v-model:value="formState.args"
                placeholder="例如：&#10;-y&#10;@modelcontextprotocol/server-filesystem&#10;/path/to/dir"
                :rows="4"
              />
            </FormItem>

            <FormItem label="环境变量（KEY=VALUE 格式，每行一个）">
              <Input.TextArea
                v-model:value="formState.envVars"
                placeholder="例如：&#10;API_KEY=sk-xxx&#10;DEBUG=true"
                :rows="3"
              />
            </FormItem>
          </template>

          <!-- HTTP 配置 -->
          <template v-else>
            <FormItem label="URL" required>
              <Input
                v-model:value="formState.url"
                placeholder="例如：http://localhost:3001/sse"
              />
            </FormItem>

            <FormItem label="请求头（KEY=VALUE 格式，每行一个）">
              <Input.TextArea
                v-model:value="formState.headers"
                placeholder="例如：&#10;Authorization=Bearer token&#10;X-Custom=value"
                :rows="3"
              />
            </FormItem>
          </template>

          <FormItem label="认证配置（JSON，可选）">
            <Input.TextArea
              v-model:value="formState.authConfig"
              placeholder='例如：{"type":"bearer","token":"xxx"}'
              :rows="2"
            />
          </FormItem>

          <FormItem v-if="editingConfig" label="启用">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>

          <FormItem>
            <Space>
              <Button type="primary" :loading="saving" @click="handleSave">
                {{ editingConfig ? '更新' : '创建' }}
              </Button>
              <Button @click="showForm = false">取消</Button>
            </Space>
          </FormItem>
        </Form>
      </template>
    </Spin>
  </Modal>
</template>
