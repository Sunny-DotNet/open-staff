<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { ProjectApi } from '#/api/openstaff/project';
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  Col,
  Divider,
  Form,
  Input,
  message,
  Popconfirm,
  Row,
  Select,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'ant-design-vue';

import { getAgentRolesApi } from '#/api/openstaff/agent';
import {
  getProjectAgentsApi,
  getProjectApi,
  setProjectAgentsApi,
  updateProjectApi,
} from '#/api/openstaff/project';
import {
  getProviderAccountsApi,
  getProviderModelsApi,
} from '#/api/openstaff/settings';

const route = useRoute();
const router = useRouter();
const projectId = route.params.id as string;

const loading = ref(true);
const saving = ref(false);
const project = ref<ProjectApi.Project | null>(null);

// Tab 1: 基本信息
const basicForm = ref({
  name: '',
  description: '',
  language: 'zh-CN',
});

const languageOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en' },
  { label: '日本語', value: 'ja' },
];

// Tab 2: 员工配置
const allRoles = ref<AgentApi.AgentRole[]>([]);
const selectedRoleIds = ref<string[]>([]);
const loadingRoles = ref(false);

// Tab 3: 模型与参数
const providers = ref<SettingsApi.ProviderAccount[]>([]);
const providerModels = ref<{ id: string; displayName: string | null }[]>([]);
const modelForm = ref({
  defaultProviderId: null as null | string,
  defaultModelName: '',
});
const extraConfigList = ref<{ key: string; value: string }[]>([]);
const loadingModels = ref(false);

// 角色颜色
const roleColorMap: Record<string, string> = {
  communicator: '#1890ff',
  decision_maker: '#722ed1',
  architect: '#13c2c2',
  producer: '#52c41a',
  debugger: '#fa8c16',
  orchestrator: '#faad14',
  image_creator: '#eb2f96',
  video_creator: '#f5222d',
};

async function fetchProject() {
  loading.value = true;
  try {
    const data = await getProjectApi(projectId);
    const p = ((data as any)?.data ?? data) as ProjectApi.Project;
    project.value = p;
    basicForm.value = {
      name: p.name,
      description: p.description || '',
      language: p.language || 'zh-CN',
    };
    modelForm.value = {
      defaultProviderId: p.defaultProviderId || null,
      defaultModelName: p.defaultModelName || '',
    };
    // 解析 extraConfig
    try {
      const parsed = p.extraConfig ? JSON.parse(p.extraConfig) : {};
      extraConfigList.value = Object.entries(parsed).map(([key, value]) => ({
        key,
        value: String(value),
      }));
    } catch {
      extraConfigList.value = [];
    }
  } catch {
    message.error('加载项目失败');
  } finally {
    loading.value = false;
  }
}

async function fetchRoles() {
  loadingRoles.value = true;
  try {
    const [roles, projectAgents] = await Promise.all([
      getAgentRolesApi(),
      getProjectAgentsApi(projectId),
    ]);
    allRoles.value = (roles ?? []).filter((r) => r.isBuiltin !== undefined);
    selectedRoleIds.value = (projectAgents ?? []).map(
      (pa: ProjectApi.ProjectAgent) => pa.agentRoleId,
    );
  } catch {
    message.error('加载员工列表失败');
  } finally {
    loadingRoles.value = false;
  }
}

async function fetchProviders() {
  try {
    providers.value = await getProviderAccountsApi();
  } catch {
    providers.value = [];
  }
}

async function onProviderChange(providerId: string | null) {
  modelForm.value.defaultModelName = '';
  providerModels.value = [];
  if (!providerId) return;
  loadingModels.value = true;
  try {
    providerModels.value = await getProviderModelsApi(providerId);
  } catch {
    providerModels.value = [];
  } finally {
    loadingModels.value = false;
  }
}

// 保存基本信息
async function saveBasicInfo() {
  saving.value = true;
  try {
    await updateProjectApi(projectId, {
      name: basicForm.value.name.trim(),
      description: basicForm.value.description.trim() || undefined,
      language: basicForm.value.language,
    });
    message.success('基本信息已保存');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

// 保存员工配置
async function saveAgents() {
  saving.value = true;
  try {
    await setProjectAgentsApi(projectId, selectedRoleIds.value);
    message.success('员工配置已保存');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

// 保存模型与参数
async function saveModelAndParams() {
  saving.value = true;
  try {
    const extraObj: Record<string, string> = {};
    for (const item of extraConfigList.value) {
      if (item.key.trim()) {
        extraObj[item.key.trim()] = item.value;
      }
    }
    await updateProjectApi(projectId, {
      defaultProviderId: modelForm.value.defaultProviderId,
      defaultModelName: modelForm.value.defaultModelName || undefined,
      extraConfig: JSON.stringify(extraObj),
    });
    message.success('模型与参数已保存');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

function addExtraParam() {
  extraConfigList.value.push({ key: '', value: '' });
}

function removeExtraParam(index: number) {
  extraConfigList.value.splice(index, 1);
}

function toggleRole(roleId: string) {
  const idx = selectedRoleIds.value.indexOf(roleId);
  if (idx >= 0) {
    selectedRoleIds.value.splice(idx, 1);
  } else {
    selectedRoleIds.value.push(roleId);
  }
}

const enabledProviders = computed(() =>
  providers.value.filter((p) => p.isEnabled),
);

onMounted(async () => {
  await fetchProject();
  await Promise.all([fetchRoles(), fetchProviders()]);
  // 若已有默认供应商，加载其模型列表
  if (modelForm.value.defaultProviderId) {
    onProviderChange(modelForm.value.defaultProviderId);
  }
});
</script>

<template>
  <Page
    :title="`项目配置 — ${project?.name || '...'}`"
    content-class="p-4"
  >
    <template #extra>
      <Space>
        <Button @click="router.push('/projects/list')">返回列表</Button>
        <Button
          v-if="project?.status === 'active'"
          type="primary"
          @click="router.push(`/projects/${projectId}/chat`)"
        >
          💬 进入群聊
        </Button>
      </Space>
    </template>

    <Spin :spinning="loading">
      <Tabs v-if="project" type="card">
        <!-- Tab 1: 基本信息 -->
        <Tabs.TabPane key="basic" tab="📋 基本信息">
          <Card :bordered="false" style="max-width: 600px">
            <Form layout="vertical">
              <Form.Item label="项目名称" required>
                <Input
                  v-model:value="basicForm.name"
                  placeholder="输入项目名称"
                />
              </Form.Item>
              <Form.Item label="描述">
                <Input.TextArea
                  v-model:value="basicForm.description"
                  :rows="4"
                  placeholder="描述项目目标和背景"
                />
              </Form.Item>
              <Form.Item label="交互语言">
                <Select
                  v-model:value="basicForm.language"
                  :options="languageOptions"
                  style="width: 200px"
                />
              </Form.Item>
              <Form.Item>
                <Button
                  :loading="saving"
                  type="primary"
                  @click="saveBasicInfo"
                >
                  保存基本信息
                </Button>
              </Form.Item>
            </Form>
          </Card>
        </Tabs.TabPane>

        <!-- Tab 2: 员工配置 -->
        <Tabs.TabPane key="agents" tab="👥 员工配置">
          <Card :bordered="false">
            <Typography.Paragraph type="secondary">
              从全局员工库中选择参与本项目的智能体。
            </Typography.Paragraph>

            <Spin :spinning="loadingRoles">
              <Row :gutter="[16, 16]">
                <Col
                  v-for="role in allRoles"
                  :key="role.id"
                  :lg="8"
                  :md="12"
                  :xs="24"
                >
                  <Card
                    :bordered="true"
                    :class="{
                      'border-blue-400': selectedRoleIds.includes(role.id),
                    }"
                    hoverable
                    size="small"
                    @click="toggleRole(role.id)"
                    style="cursor: pointer"
                  >
                    <div class="flex items-start gap-3">
                      <Checkbox
                        :checked="selectedRoleIds.includes(role.id)"
                        @click.stop
                        @change="toggleRole(role.id)"
                      />
                      <div class="flex-1">
                        <div class="flex items-center gap-2 mb-1">
                          <Typography.Text strong>
                            {{ role.name }}
                          </Typography.Text>
                          <Tag
                            :color="roleColorMap[role.roleType] || '#666'"
                            size="small"
                          >
                            {{ role.roleType }}
                          </Tag>
                        </div>
                        <Typography.Text type="secondary" style="font-size: 12px">
                          {{ role.description || '暂无描述' }}
                        </Typography.Text>
                      </div>
                    </div>
                  </Card>
                </Col>
              </Row>
            </Spin>

            <Divider />
            <div class="flex items-center justify-between">
              <Typography.Text type="secondary">
                已选择 {{ selectedRoleIds.length }} 个员工
              </Typography.Text>
              <Button :loading="saving" type="primary" @click="saveAgents">
                保存员工配置
              </Button>
            </div>
          </Card>
        </Tabs.TabPane>

        <!-- Tab 3: 模型与参数 -->
        <Tabs.TabPane key="model" tab="🤖 模型与参数">
          <Card :bordered="false" style="max-width: 700px">
            <Typography.Title :level="5">备用模型</Typography.Title>
            <Typography.Paragraph type="secondary">
              项目备用模型用于非 Agent 的辅助思考问题。
            </Typography.Paragraph>

            <Form layout="vertical">
              <Form.Item label="供应商">
                <Select
                  v-model:value="modelForm.defaultProviderId"
                  :options="
                    enabledProviders.map((p) => ({
                      label: `${p.name} (${p.providerType})`,
                      value: p.id,
                    }))
                  "
                  allow-clear
                  placeholder="选择供应商账户"
                  style="width: 100%"
                  @change="onProviderChange"
                />
              </Form.Item>
              <Form.Item label="模型名称">
                <Select
                  v-if="providerModels.length > 0"
                  v-model:value="modelForm.defaultModelName"
                  :loading="loadingModels"
                  :options="
                    providerModels.map((m) => ({
                      label: m.displayName || m.id,
                      value: m.id,
                    }))
                  "
                  allow-clear
                  placeholder="选择模型"
                  show-search
                  style="width: 100%"
                />
                <Input
                  v-else
                  v-model:value="modelForm.defaultModelName"
                  placeholder="手动输入模型名称（如 gpt-4o）"
                />
              </Form.Item>
            </Form>

            <Divider />

            <Typography.Title :level="5">扩展参数</Typography.Title>
            <Typography.Paragraph type="secondary">
              键值对形式的扩展参数，可用于存储环境变量等。
            </Typography.Paragraph>

            <Table
              :columns="[
                { title: '键', dataIndex: 'key', key: 'key' },
                { title: '值', dataIndex: 'value', key: 'value' },
                { title: '操作', key: 'action', width: 80 },
              ]"
              :data-source="extraConfigList"
              :pagination="false"
              row-key="key"
              size="small"
            >
              <template #bodyCell="{ column, record, index }">
                <template v-if="column.key === 'key'">
                  <Input
                    v-model:value="record.key"
                    placeholder="参数名"
                    size="small"
                  />
                </template>
                <template v-else-if="column.key === 'value'">
                  <Input
                    v-model:value="record.value"
                    placeholder="参数值"
                    size="small"
                  />
                </template>
                <template v-else-if="column.key === 'action'">
                  <Popconfirm
                    title="确认删除？"
                    @confirm="removeExtraParam(index)"
                  >
                    <Button danger size="small" type="text">删除</Button>
                  </Popconfirm>
                </template>
              </template>
            </Table>

            <Button
              block
              type="dashed"
              style="margin-top: 8px"
              @click="addExtraParam"
            >
              ＋ 添加参数
            </Button>

            <Divider />
            <Button
              :loading="saving"
              type="primary"
              @click="saveModelAndParams"
            >
              保存模型与参数
            </Button>
          </Card>
        </Tabs.TabPane>
      </Tabs>
    </Spin>
  </Page>
</template>
