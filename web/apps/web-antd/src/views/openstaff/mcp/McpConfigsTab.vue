<script lang="ts" setup>
import type { McpApi } from '#/api/openstaff/mcp';

import { computed, onMounted, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Card,
  Collapse,
  CollapsePanel,
  Empty,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Popconfirm,
  Select,
  SelectOption,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import {
  createMcpConfigApi,
  deleteMcpConfigApi,
  getMcpConfigsApi,
  getMcpServersApi,
  testMcpConnectionApi,
  updateMcpConfigApi,
} from '#/api/openstaff/mcp';

const emit = defineEmits<{
  switchTab: [tab: string];
}>();

// ===== 状态 =====
const configs = ref<McpApi.McpServerConfig[]>([]);
const servers = ref<McpApi.McpServer[]>([]);
const loading = ref(false);
const saving = ref(false);
const showModal = ref(false);
const editingConfig = ref<McpApi.McpServerConfig | null>(null);
const filterServerId = ref<string | undefined>(undefined);

// 测试连接
const testing = ref<string | null>(null);
const testResult = ref<McpApi.TestConnectionResult | null>(null);
const testConfigId = ref<string | null>(null);

// ===== 表单 =====
interface FormState {
  mcpServerId: string;
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

const formState = ref<FormState>(createEmptyForm());

function createEmptyForm(): FormState {
  return {
    mcpServerId: '',
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
  };
}

// ===== 计算属性 =====
const serverMap = computed(() => {
  const map = new Map<string, McpApi.McpServer>();
  for (const s of servers.value) map.set(s.id, s);
  return map;
});

const filteredConfigs = computed(() => {
  if (!filterServerId.value) return configs.value;
  return configs.value.filter((c) => c.mcpServerId === filterServerId.value);
});

const columns = [
  {
    title: '配置名称',
    dataIndex: 'name',
    key: 'name',
    width: 180,
  },
  {
    title: '所属 MCP',
    dataIndex: 'serverName',
    key: 'serverName',
    width: 160,
  },
  {
    title: '传输方式',
    dataIndex: 'transportType',
    key: 'transportType',
    width: 100,
  },
  {
    title: '描述',
    dataIndex: 'description',
    key: 'description',
    ellipsis: true,
  },
  {
    title: '状态',
    dataIndex: 'isEnabled',
    key: 'isEnabled',
    width: 80,
  },
  {
    title: '操作',
    key: 'actions',
    width: 240,
  },
];

const TRANSPORT_COLORS: Record<string, string> = {
  stdio: 'cyan',
  http: 'orange',
  sse: 'orange',
  'streamable-http': 'purple',
};

// ===== 数据加载 =====
async function fetchAll() {
  loading.value = true;
  try {
    const [configList, serverList] = await Promise.all([
      getMcpConfigsApi(),
      getMcpServersApi(),
    ]);
    configs.value = configList;
    servers.value = serverList;
  } catch {
    configs.value = [];
    servers.value = [];
  } finally {
    loading.value = false;
  }
}

// ===== 表单操作 =====
function openAddModal(serverId?: string) {
  editingConfig.value = null;
  const form = createEmptyForm();
  if (serverId) {
    form.mcpServerId = serverId;
    const server = serverMap.value.get(serverId);
    if (server) {
      form.transportType = server.transportType;
      prefillFromDefaultConfig(form, server);
    }
  }
  formState.value = form;
  showModal.value = true;
}

function prefillFromDefaultConfig(form: FormState, server: McpApi.McpServer) {
  if (!server.defaultConfig) return;
  try {
    const defaults = JSON.parse(server.defaultConfig);
    if (defaults.command) form.command = defaults.command;
    if (defaults.args) {
      form.args = Array.isArray(defaults.args)
        ? defaults.args.join('\n')
        : String(defaults.args);
    }
    if (defaults.url) form.url = defaults.url;
    if (defaults.env) {
      form.envVars = Object.entries(defaults.env)
        .map(([k, v]) => `${k}=${v}`)
        .join('\n');
    }
    if (defaults.headers) {
      form.headers = Object.entries(defaults.headers)
        .map(([k, v]) => `${k}=${v}`)
        .join('\n');
    }
  } catch {
    // ignore
  }
}

function openEditModal(config: McpApi.McpServerConfig) {
  editingConfig.value = config;
  const form = createEmptyForm();
  form.mcpServerId = config.mcpServerId;
  form.name = config.name;
  form.description = config.description ?? '';
  form.transportType = config.transportType;
  form.isEnabled = config.isEnabled;
  form.authConfig = config.authConfig ?? '';

  try {
    const conn = JSON.parse(config.connectionConfig);
    if (config.transportType === 'stdio') {
      form.command = conn.command ?? '';
      form.args = Array.isArray(conn.args)
        ? conn.args.join('\n')
        : (conn.args ?? '');
    } else {
      form.url = conn.url ?? '';
      form.headers = conn.headers
        ? Object.entries(conn.headers)
            .map(([k, v]) => `${k}=${v}`)
            .join('\n')
        : '';
    }
  } catch {
    // ignore
  }

  if (config.environmentVariables) {
    try {
      const env = JSON.parse(config.environmentVariables);
      form.envVars = Object.entries(env)
        .map(([k, v]) => `${k}=${v}`)
        .join('\n');
    } catch {
      form.envVars = config.environmentVariables;
    }
  }

  formState.value = form;
  showModal.value = true;
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

function buildConnectionConfig(): string {
  if (formState.value.transportType === 'stdio') {
    const config: Record<string, unknown> = {
      command: formState.value.command,
    };
    if (formState.value.args.trim()) {
      config.args = formState.value.args
        .split('\n')
        .map((s) => s.trim())
        .filter(Boolean);
    }
    return JSON.stringify(config);
  } else {
    const config: Record<string, unknown> = { url: formState.value.url };
    if (formState.value.headers.trim()) {
      config.headers = parseKeyValuePairs(formState.value.headers);
    }
    return JSON.stringify(config);
  }
}

function buildEnvVars(): string | undefined {
  if (!formState.value.envVars.trim()) return undefined;
  return JSON.stringify(parseKeyValuePairs(formState.value.envVars));
}

async function handleSave() {
  const f = formState.value;
  if (!f.name.trim()) {
    message.warning('请输入配置名称');
    return;
  }
  if (!f.mcpServerId) {
    message.warning('请选择 MCP 服务器');
    return;
  }
  if (f.transportType === 'stdio' && !f.command.trim()) {
    message.warning('请输入命令');
    return;
  }
  if (f.transportType !== 'stdio' && !f.url.trim()) {
    message.warning('请输入 URL');
    return;
  }

  saving.value = true;
  try {
    const connectionConfig = buildConnectionConfig();
    const environmentVariables = buildEnvVars();
    const authConfig = f.authConfig.trim() || undefined;

    if (editingConfig.value) {
      await updateMcpConfigApi(editingConfig.value.id, {
        name: f.name,
        description: f.description || undefined,
        transportType: f.transportType,
        connectionConfig,
        environmentVariables,
        authConfig,
        isEnabled: f.isEnabled,
      });
      message.success('配置已更新');
    } else {
      await createMcpConfigApi({
        mcpServerId: f.mcpServerId,
        name: f.name,
        description: f.description || undefined,
        transportType: f.transportType,
        connectionConfig,
        environmentVariables,
        authConfig,
      });
      message.success('配置已创建');
    }

    showModal.value = false;
    await fetchAll();
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
    await fetchAll();
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('删除失败: ' + msg);
  }
}

async function handleToggleEnabled(config: McpApi.McpServerConfig) {
  try {
    await updateMcpConfigApi(config.id, { isEnabled: !config.isEnabled });
    await fetchAll();
  } catch {
    message.error('操作失败');
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

function handleServerChange(serverId: string) {
  const server = serverMap.value.get(serverId);
  if (server) {
    formState.value.transportType = server.transportType;
    prefillFromDefaultConfig(formState.value, server);
  }
}

function goToMarket() {
  emit('switchTab', 'market');
}

function setServerFilter(serverId: string) {
  filterServerId.value = serverId;
}

// ===== 初始化 =====
onMounted(async () => {
  await fetchAll();
});

defineExpose({ setServerFilter, refresh: fetchAll });
</script>

<template>
  <div>
    <div style="display: flex; justify-content: flex-end; margin-bottom: 16px">
      <Space>
        <Select
          v-model:value="filterServerId"
          placeholder="筛选 MCP 服务器"
          allow-clear
          style="width: 220px"
          :options="
            servers.map((s) => ({
              label: s.name,
              value: s.id,
            }))
          "
        />
        <Button type="primary" @click="openAddModal()">
          <template #icon><IconifyIcon icon="lucide:plus" :width="14" /></template>
          新建配置
        </Button>
        <Tooltip title="前往 MCP 市场安装更多服务器">
          <Button @click="goToMarket">
            <template #icon><IconifyIcon icon="lucide:store" :width="14" /></template>
            市场
          </Button>
        </Tooltip>
      </Space>
    </div>

    <Spin :spinning="loading">
      <Empty
        v-if="filteredConfigs.length === 0 && !loading"
        description="暂无配置"
      >
        <template #extra>
          <Space>
            <Button type="primary" @click="openAddModal()">新建配置</Button>
            <Button @click="goToMarket">前往市场安装</Button>
          </Space>
        </template>
      </Empty>

      <Table
        v-else
        :data-source="filteredConfigs"
        :columns="columns"
        :pagination="{ pageSize: 20, showSizeChanger: true }"
        row-key="id"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <Space>
              <IconifyIcon icon="lucide:settings-2" :width="14" />
              <span style="font-weight: 500">{{ record.name }}</span>
            </Space>
          </template>

          <template v-if="column.key === 'serverName'">
            <Tag color="blue">{{ record.mcpServerName || record.serverName || '-' }}</Tag>
          </template>

          <template v-if="column.key === 'transportType'">
            <Tag :color="TRANSPORT_COLORS[record.transportType] || 'default'">
              {{ record.transportType }}
            </Tag>
          </template>

          <template v-if="column.key === 'description'">
            <span v-if="record.description">{{ record.description }}</span>
            <Typography.Text v-else type="secondary">无描述</Typography.Text>
          </template>

          <template v-if="column.key === 'isEnabled'">
            <Switch
              :checked="record.isEnabled"
              size="small"
              @change="handleToggleEnabled(record)"
            />
          </template>

          <template v-if="column.key === 'actions'">
            <Space>
              <Button
                size="small"
                :loading="testing === record.id"
                @click="handleTest(record.id)"
              >
                <template #icon><IconifyIcon icon="lucide:zap" :width="12" /></template>
                测试
              </Button>
              <Button size="small" type="link" @click="openEditModal(record)">
                编辑
              </Button>
              <Popconfirm
                title="确认删除此配置？"
                @confirm="handleDelete(record.id)"
              >
                <Button size="small" type="link" danger>删除</Button>
              </Popconfirm>
            </Space>
          </template>
        </template>
      </Table>

      <!-- 测试结果 -->
      <Card
        v-if="testResult && testConfigId"
        size="small"
        style="margin-top: 16px"
      >
        <Alert
          :type="testResult.success ? 'success' : 'error'"
          :message="testResult.success ? '连接成功' : '连接失败'"
          :description="testResult.message"
          show-icon
        />
        <div
          v-if="testResult.success && testResult.tools.length > 0"
          style="margin-top: 12px"
        >
          <Collapse>
            <CollapsePanel
              key="tools"
              :header="`可用工具 (${testResult.tools.length})`"
            >
              <div
                v-for="tool in testResult.tools"
                :key="tool.name"
                class="tool-item"
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
      </Card>
    </Spin>

    <!-- 新建/编辑配置 Modal -->
    <Modal
      :open="showModal"
      :title="editingConfig ? '编辑配置' : '新建配置'"
      :confirm-loading="saving"
      ok-text="保存"
      width="640px"
      @ok="handleSave"
      @cancel="showModal = false"
      destroy-on-close
    >
      <Form layout="vertical" style="margin-top: 16px">
        <FormItem label="所属 MCP 服务器" required>
          <Select
            v-model:value="formState.mcpServerId"
            placeholder="选择已安装的 MCP 服务器"
            :disabled="!!editingConfig"
            show-search
            option-filter-prop="label"
            :options="
              servers.map((s) => ({
                label: s.name,
                value: s.id,
              }))
            "
            @change="handleServerChange"
          />
        </FormItem>

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
      </Form>
    </Modal>
  </div>
</template>

<style scoped>
.tool-item {
  margin-bottom: 8px;
  padding: 8px;
  background: var(--ant-color-fill-quaternary, #fafafa);
  border-radius: 4px;
}
</style>
