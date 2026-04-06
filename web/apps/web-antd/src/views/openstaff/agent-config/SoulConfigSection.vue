<script lang="ts" setup>
import { computed } from 'vue';

import {
  Divider,
  Input,
  Select,
  SelectOption,
  Space,
  Tag,
} from 'ant-design-vue';

const CheckableTag = Tag.CheckableTag as any;

import {
  ATTITUDE_OPTIONS,
  STYLE_OPTIONS,
  TRAIT_OPTIONS,
} from '#/constants/agent';
import { toggleArrayItem } from '#/utils/array';

interface SoulConfig {
  attitudes?: string[];
  custom?: string;
  style?: string;
  traits?: string[];
}

const soul = defineModel<SoulConfig>('soul', { required: true });

const soulPromptPreview = computed(() => {
  const parts: string[] = [];
  if (soul.value.traits?.length)
    parts.push(`你的性格特征：${soul.value.traits.join('、')}`);
  if (soul.value.style) parts.push(`你的沟通风格：${soul.value.style}`);
  if (soul.value.attitudes?.length)
    parts.push(`你的工作态度：${soul.value.attitudes.join('、')}`);
  if (soul.value.custom) parts.push(soul.value.custom);
  return parts.length > 0 ? `${parts.join('。')}。` : '';
});

function toggleTrait(tag: string) {
  soul.value = { ...soul.value, traits: toggleArrayItem(soul.value.traits, tag) };
}

function toggleAttitude(tag: string) {
  soul.value = {
    ...soul.value,
    attitudes: toggleArrayItem(soul.value.attitudes, tag),
  };
}
</script>

<template>
  <Divider orientation="left" class="soul-divider">灵魂配置</Divider>

  <div class="soul-section">
    <div class="soul-label">🎭 性格特征</div>
    <Space :size="[4, 8]" wrap>
      <CheckableTag
        v-for="t in TRAIT_OPTIONS"
        :key="t"
        :checked="soul.traits?.includes(t) ?? false"
        @change="toggleTrait(t)"
      >
        {{ t }}
      </CheckableTag>
    </Space>
  </div>

  <div class="soul-section">
    <div class="soul-label">🗣️ 沟通风格</div>
    <Select
      v-model:value="soul.style"
      allow-clear
      placeholder="选择沟通风格"
      class="full-width"
    >
      <SelectOption v-for="s in STYLE_OPTIONS" :key="s" :value="s">
        {{ s }}
      </SelectOption>
    </Select>
  </div>

  <div class="soul-section">
    <div class="soul-label">🎯 工作态度</div>
    <Space :size="[4, 8]" wrap>
      <CheckableTag
        v-for="a in ATTITUDE_OPTIONS"
        :key="a"
        :checked="soul.attitudes?.includes(a) ?? false"
        @change="toggleAttitude(a)"
      >
        {{ a }}
      </CheckableTag>
    </Space>
  </div>

  <div class="soul-section">
    <div class="soul-label">📖 自定义性格描述</div>
    <Input.TextArea
      v-model:value="soul.custom"
      :rows="2"
      placeholder="（可选）自由描述该员工的个性特点…"
    />
  </div>

  <div v-if="soulPromptPreview" class="soul-preview">
    <strong>生成的灵魂 Prompt 预览：</strong><br />
    {{ soulPromptPreview }}
  </div>
</template>

<style scoped>
.soul-divider {
  margin: 8px 0 16px;
}

.soul-section {
  margin-bottom: 16px;
}

.soul-label {
  margin-bottom: 8px;
  font-weight: 500;
}

.full-width {
  width: 100%;
}

.soul-preview {
  margin-bottom: 16px;
  padding: 10px 12px;
  background: hsl(var(--accent));
  border-radius: 6px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  line-height: 1.6;
}
</style>
