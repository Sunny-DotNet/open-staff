<script lang="ts" setup>
import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Col, Empty, Row, Tag } from 'ant-design-vue';

import { $t } from '#/locales';

interface Task {
  id: string;
  title: string;
  description: string;
  status: 'blocked' | 'done' | 'in_progress' | 'pending';
  priority: 'critical' | 'high' | 'low' | 'medium';
  assignedAgent: string;
}

const tasks = ref<Task[]>([
  {
    id: '1',
    title: '需求分析文档',
    description: '编写详细的需求分析文档',
    status: 'done',
    priority: 'high',
    assignedAgent: 'PM',
  },
  {
    id: '2',
    title: '数据库设计',
    description: '设计数据库表结构和关系',
    status: 'in_progress',
    priority: 'high',
    assignedAgent: 'Architect',
  },
  {
    id: '3',
    title: 'API 接口开发',
    description: '实现 RESTful API 接口',
    status: 'in_progress',
    priority: 'medium',
    assignedAgent: 'Coder',
  },
  {
    id: '4',
    title: '前端页面开发',
    description: '开发用户管理前端页面',
    status: 'pending',
    priority: 'medium',
    assignedAgent: 'Coder',
  },
  {
    id: '5',
    title: '代码审查',
    description: '审查 API 接口代码质量',
    status: 'pending',
    priority: 'low',
    assignedAgent: 'Reviewer',
  },
  {
    id: '6',
    title: '集成测试',
    description: '编写并执行集成测试用例',
    status: 'pending',
    priority: 'medium',
    assignedAgent: 'Tester',
  },
  {
    id: '7',
    title: '第三方服务对接',
    description: '等待第三方 API 文档',
    status: 'blocked',
    priority: 'critical',
    assignedAgent: 'Coder',
  },
]);

const columns = computed(() => [
  {
    key: 'pending',
    title: $t('openstaff.taskBoard.pending'),
    color: 'default',
  },
  {
    key: 'in_progress',
    title: $t('openstaff.taskBoard.inProgress'),
    color: 'blue',
  },
  { key: 'done', title: $t('openstaff.taskBoard.done'), color: 'green' },
  {
    key: 'blocked',
    title: $t('openstaff.taskBoard.blocked'),
    color: 'red',
  },
]);

const priorityColorMap: Record<string, string> = {
  low: 'default',
  medium: 'blue',
  high: 'orange',
  critical: 'red',
};

const priorityTextMap: Record<string, string> = {
  low: 'openstaff.taskBoard.priorityLow',
  medium: 'openstaff.taskBoard.priorityMedium',
  high: 'openstaff.taskBoard.priorityHigh',
  critical: 'openstaff.taskBoard.priorityCritical',
};

function getTasksByStatus(status: string) {
  return tasks.value.filter((t) => t.status === status);
}
</script>

<template>
  <Page :title="$t('openstaff.taskBoard.title')">
    <Row :gutter="16">
      <Col v-for="column in columns" :key="column.key" :span="6">
        <Card>
          <template #title>
            <Tag :color="column.color">{{ column.title }}</Tag>
            <span class="ml-2 text-gray-400 text-sm">
              ({{ getTasksByStatus(column.key).length }})
            </span>
          </template>
          <div class="flex flex-col gap-3">
            <Card
              v-for="task in getTasksByStatus(column.key)"
              :key="task.id"
              size="small"
              class="shadow-sm"
            >
              <div class="mb-2 font-medium">{{ task.title }}</div>
              <div class="mb-2 text-gray-500 text-xs">
                {{ task.description }}
              </div>
              <div class="flex items-center justify-between">
                <Tag :color="priorityColorMap[task.priority]" size="small">
                  {{ $t(priorityTextMap[task.priority]) }}
                </Tag>
                <span class="text-gray-400 text-xs">
                  {{ task.assignedAgent }}
                </span>
              </div>
            </Card>
            <Empty
              v-if="getTasksByStatus(column.key).length === 0"
              :description="$t('openstaff.taskBoard.noTasks')"
            />
          </div>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
