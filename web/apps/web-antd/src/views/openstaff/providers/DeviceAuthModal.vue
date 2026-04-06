<script lang="ts" setup>
import { reactive, ref } from 'vue';

import {
  Badge,
  Button,
  Modal,
  Typography,
} from 'ant-design-vue';

import type { SettingsApi } from '#/api/openstaff/settings';
import {
  cancelDeviceAuthApi,
  initiateDeviceAuthApi,
  pollDeviceAuthApi,
} from '#/api/openstaff/settings';

const props = defineProps<{
  open: boolean;
  account: SettingsApi.ProviderAccount | null;
}>();

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void;
  (e: 'success'): void;
}>();

const deviceAuth = reactive({
  accountId: null as string | null,
  expiresIn: 0,
  interval: 5,
  polling: false,
  status: '',
  userCode: '',
  verificationUri: '',
});

let pollTimer: ReturnType<typeof setTimeout> | null = null;

function startPolling() {
  stopPolling();
  const interval = (deviceAuth.interval || 5) * 1000;
  pollTimer = setTimeout(doPollDeviceAuth, interval);
}

function stopPolling() {
  if (pollTimer) {
    clearTimeout(pollTimer);
    pollTimer = null;
  }
  deviceAuth.polling = false;
}

async function doPollDeviceAuth() {
  if (!deviceAuth.accountId) return;
  try {
    const result = await pollDeviceAuthApi(deviceAuth.accountId);
    if (result.status === 'success') {
      stopPolling();
      deviceAuth.status = 'success';
      emit('success');
    } else if (result.status === 'pending') {
      startPolling();
    } else {
      stopPolling();
      deviceAuth.status = result.status;
    }
  } catch {
    stopPolling();
    deviceAuth.status = 'error';
  }
}

async function startDeviceAuth() {
  if (!props.account) return;
  try {
    Object.assign(deviceAuth, {
      accountId: props.account.id,
      userCode: '',
      verificationUri: '',
      expiresIn: 0,
      interval: 5,
      polling: false,
      status: 'initiating',
    });

    const result = await initiateDeviceAuthApi(props.account.id);
    deviceAuth.userCode = result.userCode;
    deviceAuth.verificationUri = result.verificationUri;
    deviceAuth.expiresIn = result.expiresIn;
    deviceAuth.interval = result.interval;
    deviceAuth.status = 'waiting';
    deviceAuth.polling = true;

    window.open(result.verificationUri, '_blank');
    startPolling();
  } catch {
    deviceAuth.status = '';
    deviceAuth.accountId = null;
  }
}

async function cancelAuth() {
  if (deviceAuth.accountId) {
    try {
      await cancelDeviceAuthApi(deviceAuth.accountId);
    } catch { /* ignore */ }
  }
  stopPolling();
  deviceAuth.status = '';
  deviceAuth.accountId = null;
  emit('update:open', false);
}

// 当 open 变为 true 且有 account 时，自动开始授权
import { watch, onUnmounted } from 'vue';
watch(
  () => props.open,
  (val) => {
    if (val && props.account) {
      startDeviceAuth();
    } else if (!val) {
      cancelAuth();
    }
  },
);

onUnmounted(() => {
  stopPolling();
});
</script>

<template>
  <Modal
    :open="open"
    title="GitHub 设备码授权"
    :footer="null"
    @update:open="emit('update:open', $event)"
    @cancel="cancelAuth"
  >
    <div style="text-align: center; padding: 20px 0">
      <Typography.Title :level="4">
        请在 GitHub 中输入以下代码：
      </Typography.Title>
      <div style="font-size: 36px; font-weight: bold; letter-spacing: 6px; padding: 16px; background: var(--ant-color-fill-secondary, #f5f5f5); border-radius: 8px; margin: 16px 0">
        {{ deviceAuth.userCode }}
      </div>
      <Typography.Text type="secondary">
        浏览器应已自动打开验证页面
      </Typography.Text>
      <br />
      <Button
        type="link"
        :href="deviceAuth.verificationUri"
        target="_blank"
        style="margin-top: 8px"
      >
        手动打开验证页面 →
      </Button>
      <br /><br />
      <Badge status="processing" text="等待授权中..." />
      <br /><br />
      <Button @click="cancelAuth">取消</Button>
    </div>
  </Modal>
</template>
