<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Empty,
  Row,
  Spin,
  Statistic,
  Tag,
  Timeline,
  TimelineItem,
  Typography,
} from 'ant-design-vue';

import { requestClient } from '#/api/request';
import { useNotification } from '#/composables/useNotification';

interface Stats {
  projects: number;
  agents: number;
  agentRoles: number;
  tasks: { total: number; completed: number };
  events: number;
  sessions: number;
  modelProviders: number;
  uptime: number;
  recentSessions: Array<{
    id: string;
    status: string;
    createdAt: string;
    completedAt: string | null;
  }>;
}

interface HealthStatus {
  status: string;
  timestamp: string;
  version: string;
}

const loading = ref(false);
const stats = ref<Stats | null>(null);
const health = ref<HealthStatus | null>(null);
const healthError = ref(false);

const { connected } = useNotification();

function formatUptime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function formatTime(isoStr: string): string {
  const date = new Date(isoStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000);
  if (diff < 60) return `${diff} 秒前`;
  if (diff < 3600) return `${Math.floor(diff / 60)} 分钟前`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} 小时前`;
  return date.toLocaleDateString('zh-CN');
}

const sessionStatusMap: Record<string, { color: string; label: string }> = {
  running: { color: 'processing', label: '运行中' },
  completed: { color: 'success', label: '已完成' },
  cancelled: { color: 'warning', label: '已取消' },
  error: { color: 'error', label: '错误' },
};

async function fetchData() {
  loading.value = true;
  try {
    const [statsRes, healthRes] = await Promise.all([
      requestClient.get('/monitor/stats'),
      requestClient.get('/monitor/health').catch(() => null),
    ]);
    stats.value = statsRes as unknown as Stats;
    if (healthRes) {
      health.value = healthRes as unknown as HealthStatus;
      healthError.value = false;
    }
  } catch {
    healthError.value = true;
  } finally {
    loading.value = false;
  }
}

onMounted(fetchData);
</script>

<template>
  <Page title="仪表盘">
    <Spin :spinning="loading">
      <!-- 统计卡片 -->
      <Row :gutter="16" class="mb-4">
        <Col :span="4">
          <Card>
            <Statistic :value="stats?.projects ?? 0" title="项目">
              <template #prefix>📁</template>
            </Statistic>
          </Card>
        </Col>
        <Col :span="4">
          <Card>
            <Statistic :value="stats?.agentRoles ?? 0" title="代理体角色">
              <template #prefix>🤖</template>
            </Statistic>
          </Card>
        </Col>
        <Col :span="4">
          <Card>
            <Statistic :value="stats?.sessions ?? 0" title="会话">
              <template #prefix>💬</template>
            </Statistic>
          </Card>
        </Col>
        <Col :span="4">
          <Card>
            <Statistic :value="stats?.tasks?.total ?? 0" title="任务">
              <template #prefix>📋</template>
            </Statistic>
          </Card>
        </Col>
        <Col :span="4">
          <Card>
            <Statistic :value="stats?.modelProviders ?? 0" title="模型供应商">
              <template #prefix>🔗</template>
            </Statistic>
          </Card>
        </Col>
        <Col :span="4">
          <Card>
            <Statistic
              :value="formatUptime(stats?.uptime ?? 0)"
              title="运行时间"
            >
              <template #prefix>⏱️</template>
            </Statistic>
          </Card>
        </Col>
      </Row>

      <Row :gutter="16">
        <!-- 最近会话 -->
        <Col :span="16">
          <Card title="最近会话">
            <template #extra>
              <Button size="small" type="link" @click="fetchData">
                刷新
              </Button>
            </template>
            <div v-if="stats?.recentSessions?.length">
              <Timeline>
                <TimelineItem
                  v-for="session in stats.recentSessions"
                  :key="session.id"
                  :color="
                    session.status === 'completed'
                      ? 'green'
                      : session.status === 'running'
                        ? 'blue'
                        : session.status === 'error'
                          ? 'red'
                          : 'gray'
                  "
                >
                  <div class="flex items-center gap-2">
                    <Tag
                      :color="
                        sessionStatusMap[session.status]?.color ?? 'default'
                      "
                      size="small"
                    >
                      {{ sessionStatusMap[session.status]?.label ?? session.status }}
                    </Tag>
                    <Typography.Text code style="font-size: 12px">
                      {{ session.id.slice(0, 8) }}...
                    </Typography.Text>
                    <Typography.Text type="secondary" style="font-size: 12px">
                      {{ formatTime(session.createdAt) }}
                    </Typography.Text>
                  </div>
                </TimelineItem>
              </Timeline>
            </div>
            <Empty v-else description="暂无会话记录" />
          </Card>
        </Col>

        <!-- 系统状态 -->
        <Col :span="8">
          <Card title="系统状态">
            <div class="flex flex-col gap-4">
              <div class="flex items-center justify-between">
                <span>API 服务</span>
                <Tag :color="health?.status === 'healthy' ? 'green' : 'red'">
                  {{ health?.status === 'healthy' ? '正常' : '异常' }}
                </Tag>
              </div>
              <div class="flex items-center justify-between">
                <span>SignalR Hub</span>
                <Tag :color="connected ? 'green' : 'red'">
                  {{ connected ? '已连接' : '未连接' }}
                </Tag>
              </div>
              <div class="flex items-center justify-between">
                <span>版本</span>
                <Tag color="blue">{{ health?.version ?? '-' }}</Tag>
              </div>
            </div>
          </Card>

          <!-- 任务完成 -->
          <Card class="mt-4" title="任务概览">
            <div class="flex flex-col gap-3">
              <div class="flex items-center justify-between">
                <span>总任务</span>
                <Tag>{{ stats?.tasks?.total ?? 0 }}</Tag>
              </div>
              <div class="flex items-center justify-between">
                <span>已完成</span>
                <Tag color="green">{{ stats?.tasks?.completed ?? 0 }}</Tag>
              </div>
              <div class="flex items-center justify-between">
                <span>总事件</span>
                <Tag color="blue">{{ stats?.events ?? 0 }}</Tag>
              </div>
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
