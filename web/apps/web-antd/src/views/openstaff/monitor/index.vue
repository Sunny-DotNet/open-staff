<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Col, Progress, Row, Statistic, Table } from 'ant-design-vue';

import { $t } from '#/locales';

const totalTokens = ref(1_285_000);
const todayTokens = ref(42_300);
const avgLatency = ref(230);

const tokenByAgent = ref([
  { key: '1', agent: 'PM', tokens: 125_000, percentage: 10 },
  { key: '2', agent: 'Architect', tokens: 280_000, percentage: 22 },
  { key: '3', agent: 'Coder', tokens: 520_000, percentage: 40 },
  { key: '4', agent: 'Reviewer', tokens: 210_000, percentage: 16 },
  { key: '5', agent: 'Tester', tokens: 150_000, percentage: 12 },
]);

const agentColumns = [
  { title: '智能体', dataIndex: 'agent', key: 'agent' },
  { title: 'Token 用量', dataIndex: 'tokens', key: 'tokens' },
  { title: '占比', dataIndex: 'percentage', key: 'percentage' },
];

const latencyData = ref([
  { key: '1', endpoint: '/api/projects', avg: 120, p99: 350 },
  { key: '2', endpoint: '/api/agents/message', avg: 2800, p99: 8500 },
  { key: '3', endpoint: '/api/tasks', avg: 85, p99: 200 },
  { key: '4', endpoint: '/api/settings', avg: 45, p99: 120 },
]);

const latencyColumns = [
  { title: '接口', dataIndex: 'endpoint', key: 'endpoint' },
  { title: '平均延迟 (ms)', dataIndex: 'avg', key: 'avg' },
  { title: 'P99 延迟 (ms)', dataIndex: 'p99', key: 'p99' },
];
</script>

<template>
  <Page :title="$t('openstaff.monitor.title')">
    <Row :gutter="16" class="mb-4">
      <Col :span="8">
        <Card>
          <Statistic
            :value="totalTokens"
            :title="$t('openstaff.monitor.totalTokens')"
          />
        </Card>
      </Col>
      <Col :span="8">
        <Card>
          <Statistic
            :value="todayTokens"
            :title="$t('openstaff.monitor.todayTokens')"
          />
        </Card>
      </Col>
      <Col :span="8">
        <Card>
          <Statistic
            :value="avgLatency"
            :title="$t('openstaff.monitor.avgLatency')"
            suffix="ms"
          />
        </Card>
      </Col>
    </Row>

    <Row :gutter="16">
      <Col :span="12">
        <Card :title="$t('openstaff.monitor.byAgent')">
          <Table
            :columns="agentColumns"
            :data-source="tokenByAgent"
            :pagination="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'tokens'">
                {{ record.tokens.toLocaleString() }}
              </template>
              <template v-if="column.key === 'percentage'">
                <Progress
                  :percent="record.percentage"
                  :stroke-width="8"
                  size="small"
                />
              </template>
            </template>
          </Table>
        </Card>
      </Col>

      <Col :span="12">
        <Card :title="$t('openstaff.monitor.apiLatency')">
          <Table
            :columns="latencyColumns"
            :data-source="latencyData"
            :pagination="false"
            size="small"
          />
        </Card>
      </Col>
    </Row>
  </Page>
</template>
