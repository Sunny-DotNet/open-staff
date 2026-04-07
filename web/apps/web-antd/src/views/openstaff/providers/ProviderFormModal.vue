<script lang="ts" setup>
import { computed, reactive, ref, watch } from 'vue';

import {
  Form,
  FormItem,
  Input,
  InputPassword,
  message,
  Modal,
  Select,
  Space,
  Switch,
  Tag,
} from 'ant-design-vue';

import type { SettingsApi } from '#/api/openstaff/settings';
import {
  createProviderAccountApi,
  updateProviderAccountApi,
} from '#/api/openstaff/settings';
import { getLogoUrl } from '#/constants/provider';

const props = defineProps<{
  open: boolean;
  editingAccount: SettingsApi.ProviderAccount | null;
  protocols: SettingsApi.ProtocolMetadata[];
}>();

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void;
  (e: 'saved'): void;
}>();

const saving = ref(false);

const formState = reactive({
  envConfig: {} as Record<string, string | boolean | number>,
  isEnabled: true,
  name: '',
  protocolType: '',
});

const currentProtocol = computed(() =>
  props.protocols.find((p) => p.providerKey === formState.protocolType),
);

function buildDefaultEnvConfig(proto: SettingsApi.ProtocolMetadata): Record<string, string | boolean | number> {
  const config: Record<string, string | boolean | number> = {};
  for (const field of proto.envSchema) {
    if (field.fieldType === 'bool') {
      config[field.name] = field.defaultValue === 'True' || field.defaultValue === 'true';
    } else if (field.fieldType === 'number') {
      config[field.name] = Number(field.defaultValue) || 0;
    } else {
      config[field.name] = field.defaultValue ?? '';
    }
  }
  return config;
}

watch(
  () => props.open,
  (val) => {
    if (!val) return;
    if (props.editingAccount) {
      formState.name = props.editingAccount.name;
      formState.protocolType = props.editingAccount.protocolType;
      formState.isEnabled = props.editingAccount.isEnabled;
      // 以 schema 默认值为基础，覆盖已有值（secret 字段后端不返回，保持空）
      const proto = props.protocols.find(
        (p) => p.providerKey === props.editingAccount!.protocolType,
      );
      const base = proto ? buildDefaultEnvConfig(proto) : {};
      const raw = props.editingAccount.envConfig ?? {};
      // 按 schema 类型强制转换后端返回值
      if (proto) {
        for (const field of proto.envSchema) {
          if (field.name in raw) {
            if (field.fieldType === 'bool') {
              base[field.name] = raw[field.name] === true || raw[field.name] === 'True' || raw[field.name] === 'true';
            } else if (field.fieldType === 'number') {
              base[field.name] = Number(raw[field.name]) || 0;
            } else {
              base[field.name] = raw[field.name] ?? '';
            }
          }
        }
      }
      formState.envConfig = base;
    } else {
      const defaultProto = props.protocols[0];
      formState.protocolType = defaultProto?.providerKey ?? '';
      formState.name = '';
      formState.isEnabled = true;
      formState.envConfig = defaultProto ? buildDefaultEnvConfig(defaultProto) : {};
    }
  },
);

function onProtocolTypeChange(key: string) {
  const proto = props.protocols.find((p) => p.providerKey === key);
  if (proto && !props.editingAccount) {
    formState.envConfig = buildDefaultEnvConfig(proto);
  }
}

async function handleSave() {
  if (!formState.name.trim()) {
    message.warning('请输入供应商名称');
    return;
  }
  saving.value = true;
  try {
    const envConfig = { ...formState.envConfig };
    if (props.editingAccount) {
      const proto = currentProtocol.value;
      if (proto) {
        for (const field of proto.envSchema) {
          if (field.fieldType === 'secret' && !envConfig[field.name]) {
            delete envConfig[field.name];
          }
        }
      }
      await updateProviderAccountApi(props.editingAccount.id, {
        name: formState.name,
        envConfig,
        isEnabled: formState.isEnabled,
      });
      message.success('供应商已更新');
    } else {
      await createProviderAccountApi({
        name: formState.name,
        protocolType: formState.protocolType,
        envConfig,
        isEnabled: formState.isEnabled,
      });
      message.success('供应商已创建');
    }
    emit('update:open', false);
    emit('saved');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('保存失败: ' + msg);
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <Modal
    :open="open"
    :title="editingAccount ? '编辑供应商' : '添加供应商'"
    :confirm-loading="saving"
    @ok="handleSave"
    @update:open="emit('update:open', $event)"
    :okText="editingAccount ? '保存' : '创建'"
    cancelText="取消"
    :width="560"
  >
    <Form layout="vertical" style="margin-top: 16px">
      <!-- 协议类型 -->
      <FormItem label="供应商协议" required>
        <Select
          v-model:value="formState.protocolType"
          :disabled="!!editingAccount"
          @change="onProtocolTypeChange"
          placeholder="选择协议类型"
        >
          <Select.Option
            v-for="proto in protocols"
            :key="proto.providerKey"
            :value="proto.providerKey"
          >
            <Space>
              <img
                v-if="proto.logo"
                :src="getLogoUrl(proto.logo)"
                :alt="proto.providerKey"
                style="width: 16px; height: 16px; vertical-align: middle"
              />
              <span v-else>🔌</span>
              <span>{{ proto.providerName }}</span>
              <Tag v-if="proto.isVendor" style="font-size: 10px; margin-left: 4px">厂商</Tag>
            </Space>
          </Select.Option>
        </Select>
      </FormItem>

      <!-- 名称 -->
      <FormItem label="名称" required>
        <Input
          v-model:value="formState.name"
          placeholder="例如：我的 OpenAI 账户"
        />
      </FormItem>

      <!-- 动态 EnvConfig 字段 -->
      <template v-if="currentProtocol">
        <FormItem
          v-for="field in currentProtocol.envSchema"
          :key="field.name"
          :label="field.name"
        >
          <!-- boolean 字段 -->
          <Switch
            v-if="field.fieldType === 'bool'"
            v-model:checked="formState.envConfig[field.name]"
          />

          <!-- secret 字段 -->
          <InputPassword
            v-else-if="field.fieldType === 'secret'"
            v-model:value="formState.envConfig[field.name]"
            :placeholder="editingAccount ? '留空保持不变' : `输入 ${field.name}`"
          />

          <!-- 普通字段 -->
          <Input
            v-else
            v-model:value="formState.envConfig[field.name]"
            :placeholder="field.defaultValue ? String(field.defaultValue) : `输入 ${field.name}`"
          />
        </FormItem>
      </template>

      <!-- 启用开关 -->
      <FormItem label="启用">
        <Switch
          v-model:checked="formState.isEnabled"
          checked-children="启用"
          un-checked-children="禁用"
        />
      </FormItem>
    </Form>
  </Modal>
</template>
