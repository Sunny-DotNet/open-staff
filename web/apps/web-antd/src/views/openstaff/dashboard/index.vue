<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Card,
  Col,
  Row,
  Statistic,
  Tag,
  Timeline,
  TimelineItem,
} from 'ant-design-vue';

import { $t } from '#/locales';

const projectCount = ref(12);
const activeAgentCount = ref(5);
const runningTaskCount = ref(8);

const recentActivities = ref([
  {
    id: '1',
    content: 'Architect 完成了系统设计方案',
    time: '10 分钟前',
    color: 'green',
  },
  {
    id: '2',
    content: 'Coder 提交了 auth 模块代码',
    time: '25 分钟前',
    color: 'blue',
  },
  {
    id: '3',
    content: 'Reviewer 发现了 2 个代码问题',
    time: '1 小时前',
    color: 'orange',
  },
  {
    id: '4',
    content: 'Tester 完成了单元测试编写',
    time: '2 小时前',
    color: 'green',
  },
  {
    id: '5',
    content: 'PM 创建了新项目「用户管理系统」',
    time: '3 小时前',
    color: 'blue',
  },
]);

const systemStatus = ref<{ label: string; status: 'error' | 'success' }[]>([
  { label: 'API 服务', status: 'success' },
  { label: 'SignalR Hub', status: 'success' },
  { label: '模型服务', status: 'success' },
]);
</script>

<template>
  <Page :title="$t('openstaff.dashboard.title')">
    <Row :gutter="16" class="mb-4">
      <Col :span="8">
        <Card>
          <Statistic
            :value="projectCount"
            :title="$t('openstaff.dashboard.totalProjects')"
          >
            <template #prefix>
              <span class="i-lucide-folder-kanban mr-1 text-blue-500" />
            </template>
          </Statistic>
        </Card>
      </Col>
      <Col :span="8">
        <Card>
          <Statistic
            :value="activeAgentCount"
            :title="$t('openstaff.dashboard.activeAgents')"
          >
            <template #prefix>
              <span class="i-lucide-bot mr-1 text-green-500" />
            </template>
          </Statistic>
        </Card>
      </Col>
      <Col :span="8">
        <Card>
          <Statistic
            :value="runningTaskCount"
            :title="$t('openstaff.dashboard.runningTasks')"
          >
            <template #prefix>
              <span class="i-lucide-list-checks mr-1 text-orange-500" />
            </template>
          </Statistic>
        </Card>
      </Col>
    </Row>

    <Row :gutter="16">
      <Col :span="16">
        <Card :title="$t('openstaff.dashboard.recentActivity')">
          <Timeline>
            <TimelineItem
              v-for="activity in recentActivities"
              :key="activity.id"
              :color="activity.color"
            >
              <p>{{ activity.content }}</p>
              <p class="text-gray-400 text-xs">{{ activity.time }}</p>
            </TimelineItem>
          </Timeline>
        </Card>
      </Col>
      <Col :span="8">
        <Card :title="$t('openstaff.dashboard.systemStatus')">
          <div class="flex flex-col gap-3">
            <div
              v-for="item in systemStatus"
              :key="item.label"
              class="flex items-center justify-between"
            >
              <span>{{ item.label }}</span>
              <Tag :color="item.status === 'success' ? 'green' : 'red'">
                {{
                  item.status === 'success'
                    ? $t('openstaff.dashboard.statusNormal')
                    : $t('openstaff.dashboard.statusAbnormal')
                }}
              </Tag>
            </div>
          </div>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
