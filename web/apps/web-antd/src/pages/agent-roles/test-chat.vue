<script setup lang="ts">
import type {
  AgentRoleDto,
  AgentSoulDto,
  ProviderAccountDto,
} from '@openstaff/api';

import { useQuery } from '@tanstack/vue-query';
import type { ISubscription } from '@microsoft/signalr';
import {
  getApiProviderAccountsByIdModels,
  postApiAgentRolesByIdTestChat,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, onUnmounted, reactive, ref, watch } from 'vue';

import AgentConversationPanel from '@/components/AgentConversationPanel.vue';
import { appLocale } from '@/app-preferences';
import {
  applySessionConversationEvent,
  createLocalConversationMessageId,
  createSessionConversationState,
  removePendingAssistantPlaceholder,
  startAssistantPlaceholder,
  type SessionConversationMessage,
} from '@/components/session-conversation-stream';
import { useNotification } from '@/composables/useNotification';
import { t } from '@/i18n';
import {
  formatSoulDisplayValue,
  formatSoulDisplayValues,
  loadSoulOptions,
} from './soul-options';

const props = defineProps<{
  open: boolean;
  providers: ProviderAccountDto[];
  role: AgentRoleDto | null;
}>();

const emit = defineEmits<{
  (event: 'update:open', value: boolean): void;
}>();

const { connected, streamTask } = useNotification();

const conversationState = createSessionConversationState();
const chatInput = ref('');
const chatLoading = ref(false);
const chatMessages = ref<SessionConversationMessage[]>([]);
const currentSubscription = ref<ISubscription<any> | null>(null);
const overrideForm = reactive({
  description: '',
  modelName: '',
  modelProviderId: '',
  name: '',
  soulAttitudes: '',
  soulCustom: '',
  soulStyle: '',
  soulTraits: '',
  temperature: 0.7,
});

const providerModelsQuery = useQuery({
  queryKey: ['test-chat-provider-models', () => overrideForm.modelProviderId],
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProviderAccountsByIdModels({
        path: { id: overrideForm.modelProviderId },
      }),
    ),
  enabled: computed(() => !!overrideForm.modelProviderId && props.open),
});

const soulOptionsQuery = useQuery({
  queryKey: ['agent-soul-options', 'test-chat', computed(() => appLocale.value)],
  queryFn: () => loadSoulOptions(appLocale.value),
  staleTime: 5 * 60 * 1000,
});

const roleTitle = computed(() => props.role?.name || t('role.unnamedRole'));
const hasOverride = computed(
  () =>
    !!(
      overrideForm.name ||
      overrideForm.description ||
      overrideForm.modelProviderId ||
      overrideForm.modelName ||
      overrideForm.temperature !== 0.7 ||
      overrideForm.soulTraits ||
      overrideForm.soulStyle ||
      overrideForm.soulAttitudes ||
      overrideForm.soulCustom
    ),
);

watch(
  () => [props.open, props.role?.id] as const,
  ([open]) => {
    if (!open) {
      resetState();
      return;
    }

    loadRoleDefaults();
  },
  { immediate: true },
);

onUnmounted(() => {
  disposeStream();
});

function closeModal() {
  emit('update:open', false);
}

function clearChat() {
  disposeStream();
  chatLoading.value = false;
  chatMessages.value = [];
  conversationState.pendingAssistantId = null;
}

async function sendTestMessage() {
  if (!props.role?.id || !chatInput.value.trim() || chatLoading.value) {
    return;
  }

  const userMessage = chatInput.value.trim();
  chatMessages.value.push({
    id: createLocalConversationMessageId('local'),
    role: 'user',
    content: userMessage,
    timestamp: new Date().toISOString(),
  });
  chatInput.value = '';
  chatLoading.value = true;

  try {
    const response = unwrapClientEnvelope(
      await postApiAgentRolesByIdTestChat({
        body: {
          message: userMessage,
          override: hasOverride.value ? buildOverridePayload() : undefined,
        },
        path: { id: props.role.id },
      }),
    );
    const taskId = response.taskId;
    if (!taskId) {
      throw new Error(t('role.testChatStartFailed'));
    }

    disposeStream();
    startAssistantPlaceholder(chatMessages.value, conversationState);
    currentSubscription.value = await streamTask(
      taskId,
      (event) => {
        const reduced = applySessionConversationEvent(
          chatMessages.value,
          conversationState,
          event,
        );
        if (reduced.clearSending) {
          chatLoading.value = false;
        }
      },
      () => {
        chatLoading.value = false;
        currentSubscription.value = null;
        conversationState.pendingAssistantId = null;
      },
      (error) => {
        chatLoading.value = false;
        currentSubscription.value = null;
        removePendingAssistantPlaceholder(chatMessages.value, conversationState);
        chatMessages.value.push({
          id: createLocalConversationMessageId('assistant-error'),
          role: 'assistant',
          content: `❌ ${error.message}`,
          timestamp: new Date().toISOString(),
        });
      },
    );
  } catch (error) {
    removePendingAssistantPlaceholder(chatMessages.value, conversationState);
    chatLoading.value = false;
    message.error(getErrorMessage(error, t('role.testChatStartFailed')));
  }
}

function loadRoleDefaults() {
  const soul = props.role?.soul ?? defaultSoul();
  const catalog = soulOptionsQuery.data.value;
  overrideForm.description = '';
  overrideForm.modelName = props.role?.modelName ?? '';
  overrideForm.modelProviderId = props.role?.modelProviderId ?? '';
  overrideForm.name = '';
  overrideForm.soulAttitudes = formatSoulDisplayValues(
    soul.attitudes,
    catalog?.attitudes,
  ).join(', ');
  overrideForm.soulCustom = soul.custom ?? '';
  overrideForm.soulStyle = formatSoulDisplayValue(soul.style, catalog?.styles);
  overrideForm.soulTraits = formatSoulDisplayValues(
    soul.traits,
    catalog?.traits,
  ).join(', ');
  overrideForm.temperature = parseTemperatureFromConfig(props.role?.config);
}

function resetState() {
  clearChat();
  chatInput.value = '';
  loadRoleDefaults();
}

function disposeStream() {
  currentSubscription.value?.dispose();
  currentSubscription.value = null;
}

function buildOverridePayload() {
  return {
    description: overrideForm.description.trim() || undefined,
    modelName: overrideForm.modelName || undefined,
    modelProviderId: overrideForm.modelProviderId || undefined,
    name: overrideForm.name.trim() || undefined,
    soul: buildSoulPayload(),
    temperature: overrideForm.temperature,
  };
}

function buildSoulPayload(): AgentSoulDto | undefined {
  const soul = {
    attitudes: splitCommaValues(overrideForm.soulAttitudes),
    custom: overrideForm.soulCustom.trim() || undefined,
    style: overrideForm.soulStyle.trim() || undefined,
    traits: splitCommaValues(overrideForm.soulTraits),
  };

  return soul.attitudes.length || soul.custom || soul.style || soul.traits.length
    ? soul
    : undefined;
}

function splitCommaValues(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

function parseTemperatureFromConfig(config: null | string | undefined) {
  if (!config) {
    return 0.7;
  }

  try {
    const parsed = JSON.parse(config) as {
      modelParameters?: { temperature?: number | string };
    };
    const temperature = Number(parsed.modelParameters?.temperature ?? 0.7);
    return Number.isNaN(temperature) ? 0.7 : temperature;
  } catch {
    return 0.7;
  }
}

function defaultSoul(): AgentSoulDto {
  return {
    attitudes: [],
    custom: '',
    style: '',
    traits: [],
  };
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <a-modal
    :open="open"
    :title="`${t('role.testChatTitle')} · ${roleTitle}`"
    :footer="null"
    :width="1200"
    destroy-on-close
    @cancel="closeModal"
  >
    <div class="grid h-[72vh] gap-4 lg:grid-cols-[1.35fr_0.95fr]">
      <section class="overflow-hidden rounded-2xl border border-border/70">
        <AgentConversationPanel
          v-model="chatInput"
          :agent-avatar="role?.avatar"
          :agent-name="roleTitle"
          :clearable="true"
          :clear-disabled="!chatMessages.length && !chatLoading"
          :empty-description="t('role.testChatEmpty')"
          :messages="chatMessages"
          :sending="chatLoading"
          :subtitle="connected ? t('role.signalrConnected') : t('role.signalrConnecting')"
          :subtitle-status="connected ? 'online' : 'connecting'"
          @clear="clearChat"
          @send="sendTestMessage"
        />
      </section>

      <section class="overflow-auto rounded-2xl border border-border/70 bg-background/75 p-4">
        <div class="mb-4">
          <h4 class="text-sm font-semibold">{{ t('role.testChatOverrideSection') }}</h4>
          <p class="mt-1 text-xs leading-6 text-muted-foreground">
            {{ t('role.testChatOverrideDescription') }}
          </p>
        </div>

        <a-form layout="vertical">
          <div class="grid gap-4 md:grid-cols-2">
            <a-form-item :label="t('role.name')">
              <a-input v-model:value="overrideForm.name" />
            </a-form-item>
            <a-form-item :label="t('role.description')">
              <a-input v-model:value="overrideForm.description" />
            </a-form-item>
          </div>

          <div class="grid gap-4 md:grid-cols-2">
            <a-form-item :label="t('role.modelProvider')">
              <a-select
                v-model:value="overrideForm.modelProviderId"
                allow-clear
                :options="
                  providers
                    .filter((provider) => provider.isEnabled)
                    .map((provider) => ({
                      label: provider.name ?? provider.id ?? '--',
                      value: provider.id ?? '',
                    }))
                "
              />
            </a-form-item>
            <a-form-item :label="t('role.model')">
              <a-select
                v-model:value="overrideForm.modelName"
                allow-clear
                show-search
                :disabled="!overrideForm.modelProviderId"
                :loading="providerModelsQuery.isFetching.value"
                :options="
                  (providerModelsQuery.data.value ?? []).map((model) => ({
                    label: model.id ?? '--',
                    value: model.id ?? '',
                  }))
                "
              />
            </a-form-item>
          </div>

          <a-form-item :label="t('role.temperature')">
            <a-slider v-model:value="overrideForm.temperature" :max="2" :min="0" :step="0.1" />
          </a-form-item>

          <div class="grid gap-4 md:grid-cols-2">
            <a-form-item :label="t('role.soulTraits')">
              <a-input
                v-model:value="overrideForm.soulTraits"
                :placeholder="t('role.commaSeparatedPlaceholder')"
              />
            </a-form-item>
            <a-form-item :label="t('role.soulAttitudes')">
              <a-input
                v-model:value="overrideForm.soulAttitudes"
                :placeholder="t('role.commaSeparatedPlaceholder')"
              />
            </a-form-item>
          </div>

          <a-form-item :label="t('role.soulStyle')">
            <a-input v-model:value="overrideForm.soulStyle" />
          </a-form-item>

          <a-form-item :label="t('role.soulCustom')">
            <a-textarea v-model:value="overrideForm.soulCustom" :rows="5" />
          </a-form-item>
        </a-form>
      </section>
    </div>
  </a-modal>
</template>
