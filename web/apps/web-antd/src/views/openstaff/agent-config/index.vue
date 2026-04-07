<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Divider,
  Empty,
  message,
  Popconfirm,
  Row,
  Space,
  Spin,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  createAgentRoleApi,
  deleteAgentRoleApi,
  getAgentRolesApi,
} from '#/api/openstaff/agent';
import { getProviderAccountsApi } from '#/api/openstaff/settings';
import { getRoleIcon } from '#/constants/agent';

import AgentConfigDrawer from './AgentConfigDrawer.vue';
import ChatTestModal from './ChatTestModal.vue';

// ===== 类型 =====
interface EditFormState {
  avatar: string;
  description: string;
  maxTokens: number;
  modelProviderId: string;
  modelName: string;
  name: string;
  soul: AgentApi.AgentSoul;
  temperature: number;
  tools: string[];
}

// ===== 状态 =====
const roles = ref<AgentApi.AgentRole[]>([]);
const providers = ref<SettingsApi.ProviderAccount[]>([]);
const loadingRoles = ref(false);

// Drawer 状态
const drawerVisible = ref(false);
const drawerMode = ref<'create' | 'edit'>('edit');
const editingRoleId = ref<string>('');

// 对话测试 Modal 状态
const chatModalVisible = ref(false);
const chatRoleId = ref('');
const chatRoleName = ref('');

// ===== 计算属性 =====
const editingRole = computed(() =>
  roles.value.find((r) => r.id === editingRoleId.value),
);

// ===== 数据加载 =====
onMounted(async () => {
  loadingRoles.value = true;
  try {
    const [rolesData, providersData] = await Promise.all([
      getAgentRolesApi(),
      getProviderAccountsApi(),
    ]);
    roles.value = rolesData;
    providers.value = providersData;
  } finally {
    loadingRoles.value = false;
  }
});

// ===== 解析 config =====
function parseConfig(configStr: string | null): AgentApi.AgentRoleConfig {
  try {
    return configStr ? JSON.parse(configStr) : {};
  } catch {
    return {};
  }
}

// ===== Drawer 操作 =====
function openConfigDrawer(role: AgentApi.AgentRole) {
  drawerMode.value = 'edit';
  editingRoleId.value = role.id;
  drawerVisible.value = true;
}

function openCreateDrawer() {
  drawerMode.value = 'create';
  editingRoleId.value = '';
  drawerVisible.value = true;
}

// ===== 保存配置 =====
async function handleDrawerSave(form: EditFormState) {
  if (drawerMode.value === 'create') {
    await createRole(form);
    return;
  }

  const role = editingRole.value;
  if (!role) return;

  try {
    const existingConfig = parseConfig(role.config);
    const updatedConfig = {
      ...existingConfig,
      modelParameters: {
        temperature: form.temperature,
        maxTokens: form.maxTokens,
      },
      tools: form.tools,
    };

    const updateData: AgentApi.UpdateAgentRoleParams = {
      avatar: form.avatar || undefined,
      modelProviderId: form.modelProviderId || undefined,
      modelName: form.modelName || undefined,
      config: JSON.stringify(updatedConfig),
      soul: form.soul,
    };

    if (!role.isBuiltin) {
      updateData.name = form.name;
      updateData.description = form.description;
    }

    await updateAgentRoleApi(role.id, updateData);
    roles.value = await getAgentRolesApi();
    message.success('配置已保存');
    drawerVisible.value = false;
  } catch {
    message.error('保存失败');
  }
}

// ===== 创建角色 =====
async function createRole(form: EditFormState) {
  if (!form.name.trim()) {
    message.warning('请输入员工名称');
    return;
  }

  try {
    const roleType = form.name
      .trim()
      .toLowerCase()
      .replace(/\s+/g, '_')
      .replace(/[^a-z0-9_\u4e00-\u9fff]/g, '');
    const config = {
      modelParameters: {
        temperature: form.temperature,
        maxTokens: form.maxTokens,
      },
      tools: form.tools,
    };

    await createAgentRoleApi({
      name: form.name.trim(),
      roleType,
      description: form.description || undefined,
      modelProviderId: form.modelProviderId || undefined,
      modelName: form.modelName || undefined,
      config: JSON.stringify(config),
      soul: form.soul,
    });

    roles.value = await getAgentRolesApi();
    message.success('员工创建成功');
    drawerVisible.value = false;
  } catch {
    message.error('创建失败');
  }
}

// ===== 删除角色 =====
async function deleteRole(role: AgentApi.AgentRole) {
  try {
    await deleteAgentRoleApi(role.id);
    roles.value = await getAgentRolesApi();
    message.success('已删除');
    if (editingRoleId.value === role.id) {
      drawerVisible.value = false;
    }
  } catch {
    message.error('删除失败');
  }
}

// ===== 对话测试 =====
function openChatModal(role: AgentApi.AgentRole) {
  chatRoleId.value = role.id;
  chatRoleName.value = role.name;
  chatModalVisible.value = true;
}

import { updateAgentRoleApi } from '#/api/openstaff/agent';
</script>

<template>
  <Page title="员工管理">
    <Spin :spinning="loadingRoles">
      <!-- 顶部操作栏 -->
      <div style="margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center">
        <Typography.Title :level="5" style="margin: 0">
          团队成员（{{ roles.length }}）
        </Typography.Title>
        <Button type="primary" @click="openCreateDrawer">
          ＋ 新增员工
        </Button>
      </div>

      <!-- 空状态 -->
      <Empty v-if="!loadingRoles && roles.length === 0" description="暂无员工，点击上方按钮创建" />

      <!-- 卡片网格 -->
      <Row :gutter="[16, 16]">
        <Col
          v-for="role in roles"
          :key="role.id"
          :lg="6"
          :md="8"
          :sm="12"
          :xs="24"
        >
          <Card
            hoverable
            class="staff-card"
            :body-style="{ padding: '20px' }"
            @click="openConfigDrawer(role)"
          >
            <!-- 卡片头部：头像 + 标签 -->
            <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px">
              <img
                v-if="role.avatar"
                :src="role.avatar"
                :alt="role.name"
                style="width: 40px; height: 40px; border-radius: 8px; object-fit: cover"
              />
              <span v-else style="font-size: 36px; line-height: 1">
                {{ getRoleIcon(role.roleType) }}
              </span>
              <Space :size="4">
                <Tag v-if="role.isBuiltin" color="blue">内置</Tag>
                <Tag v-if="role.modelProviderName" color="green">
                  {{ role.modelProviderName }}
                </Tag>
              </Space>
            </div>

            <!-- 名称 -->
            <Typography.Title :level="5" :ellipsis="true" style="margin-bottom: 4px">
              {{ role.name }}
            </Typography.Title>

            <!-- 描述 -->
            <Typography.Paragraph
              type="secondary"
              :ellipsis="{ rows: 2 }"
              :content="role.description || '暂无描述'"
              style="margin-bottom: 12px; font-size: 13px; min-height: 40px"
            />

            <!-- 角色类型标签 -->
            <div style="margin-bottom: 12px">
              <Tag color="default">{{ role.roleType }}</Tag>
            </div>

            <!-- 操作按钮 -->
            <div style="display: flex; gap: 8px" @click.stop>
              <Button size="small" type="primary" ghost @click="openChatModal(role)">
                💬 对话测试
              </Button>
              <Popconfirm
                v-if="!role.isBuiltin"
                title="确定要删除该员工吗？"
                ok-text="确定"
                cancel-text="取消"
                @confirm="deleteRole(role)"
              >
                <Button size="small" danger>删除</Button>
              </Popconfirm>
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>

    <!-- 配置 Drawer -->
    <AgentConfigDrawer
      :open="drawerVisible"
      :mode="drawerMode"
      :editing-role="editingRole"
      :providers="providers"
      @update:open="drawerVisible = $event"
      @save="handleDrawerSave"
    />

    <!-- 对话测试 Modal -->
    <ChatTestModal
      :open="chatModalVisible"
      :role-id="chatRoleId"
      :role-name="chatRoleName"
      :providers="providers"
      @update:open="chatModalVisible = $event"
    />
  </Page>
</template>

<style scoped>
.staff-card {
  border-radius: 10px;
  transition: all 0.3s ease;
  border: 1px solid hsl(var(--border));
}

.staff-card:hover {
  box-shadow: 0 6px 20px hsl(var(--foreground) / 0.08);
  transform: translateY(-2px);
}
</style>
