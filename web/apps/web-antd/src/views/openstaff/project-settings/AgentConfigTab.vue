<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { ProjectApi } from '#/api/openstaff/project';

import { onMounted, ref } from 'vue';

import {
  Button,
  Card,
  Checkbox,
  Col,
  Divider,
  message,
  Row,
  Spin,
  Tag,
  Typography,
} from 'ant-design-vue';

import { getAgentRolesApi } from '#/api/openstaff/agent';
import { getProjectAgentsApi, setProjectAgentsApi } from '#/api/openstaff/project';
import { ROLE_COLORS } from '#/constants/agent';

const props = defineProps<{
  projectId: string;
  saving: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:saving', value: boolean): void;
}>();

const allRoles = ref<AgentApi.AgentRole[]>([]);
const selectedRoleIds = ref<string[]>([]);
const loadingRoles = ref(false);

async function fetchRoles() {
  loadingRoles.value = true;
  try {
    const [roles, projectAgents] = await Promise.all([
      getAgentRolesApi(),
      getProjectAgentsApi(props.projectId),
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

function toggleRole(roleId: string) {
  const idx = selectedRoleIds.value.indexOf(roleId);
  if (idx >= 0) {
    selectedRoleIds.value.splice(idx, 1);
  } else {
    selectedRoleIds.value.push(roleId);
  }
}

async function saveAgents() {
  emit('update:saving', true);
  try {
    await setProjectAgentsApi(props.projectId, selectedRoleIds.value);
    message.success('员工配置已保存');
  } catch {
    message.error('保存失败');
  } finally {
    emit('update:saving', false);
  }
}

onMounted(fetchRoles);
</script>

<template>
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
                    :color="ROLE_COLORS[role.roleType] || '#666'"
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
</template>
