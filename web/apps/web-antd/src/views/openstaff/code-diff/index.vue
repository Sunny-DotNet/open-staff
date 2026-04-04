<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Col, List, ListItem, Row, Select, Tag, Tabs } from 'ant-design-vue';

import { $t } from '#/locales';

const TabPane = Tabs.TabPane;

interface Checkpoint {
  id: string;
  label: string;
  timestamp: string;
}

interface FileChange {
  path: string;
  additions: number;
  deletions: number;
  status: 'added' | 'deleted' | 'modified';
}

const checkpoints = ref<Checkpoint[]>([
  { id: 'cp1', label: '初始化项目结构', timestamp: '2024-01-15 10:00:00' },
  { id: 'cp2', label: '添加用户模块', timestamp: '2024-01-15 11:30:00' },
  { id: 'cp3', label: '实现认证逻辑', timestamp: '2024-01-15 14:00:00' },
]);

const selectedCheckpoint = ref('cp3');

const fileChanges = ref<FileChange[]>([
  { path: 'src/auth/auth.service.ts', additions: 45, deletions: 0, status: 'added' },
  { path: 'src/auth/auth.controller.ts', additions: 32, deletions: 0, status: 'added' },
  { path: 'src/auth/auth.module.ts', additions: 15, deletions: 0, status: 'added' },
  { path: 'src/user/user.service.ts', additions: 8, deletions: 3, status: 'modified' },
  { path: 'src/app.module.ts', additions: 2, deletions: 1, status: 'modified' },
]);

const selectedFile = ref('src/auth/auth.service.ts');

const diffContent = ref(`@@ -0,0 +1,45 @@
+import { Injectable, UnauthorizedException } from '@nestjs/common';
+import { JwtService } from '@nestjs/jwt';
+import { UserService } from '../user/user.service';
+import * as bcrypt from 'bcrypt';
+
+@Injectable()
+export class AuthService {
+  constructor(
+    private userService: UserService,
+    private jwtService: JwtService,
+  ) {}
+
+  async validateUser(username: string, password: string) {
+    const user = await this.userService.findByUsername(username);
+    if (user && await bcrypt.compare(password, user.password)) {
+      const { password: _, ...result } = user;
+      return result;
+    }
+    return null;
+  }
+
+  async login(user: any) {
+    const payload = { username: user.username, sub: user.id };
+    return {
+      access_token: this.jwtService.sign(payload),
+    };
+  }
+}`);

const statusColorMap: Record<string, string> = {
  added: 'green',
  modified: 'blue',
  deleted: 'red',
};
</script>

<template>
  <Page :title="$t('openstaff.codeDiff.title')">
    <Row :gutter="16">
      <!-- 左侧：文件变更列表 -->
      <Col :span="6">
        <Card :title="$t('openstaff.codeDiff.fileChanges')">
          <div class="mb-3">
            <Select
              v-model:value="selectedCheckpoint"
              :placeholder="$t('openstaff.codeDiff.selectCheckpoint')"
              class="w-full"
            >
              <Select.Option
                v-for="cp in checkpoints"
                :key="cp.id"
                :value="cp.id"
              >
                {{ cp.label }}
              </Select.Option>
            </Select>
          </div>
          <List :data-source="fileChanges" size="small">
            <template #renderItem="{ item }">
              <ListItem
                class="cursor-pointer"
                @click="selectedFile = item.path"
              >
                <div class="w-full">
                  <div
                    class="truncate text-sm"
                    :class="{ 'font-bold': item.path === selectedFile }"
                  >
                    {{ item.path }}
                  </div>
                  <div class="flex gap-2">
                    <Tag :color="statusColorMap[item.status]" size="small">
                      {{ item.status }}
                    </Tag>
                    <span class="text-xs text-green-500">
                      +{{ item.additions }}
                    </span>
                    <span class="text-xs text-red-500">
                      -{{ item.deletions }}
                    </span>
                  </div>
                </div>
              </ListItem>
            </template>
          </List>
        </Card>
      </Col>

      <!-- 右侧：代码差异视图 -->
      <Col :span="18">
        <Card>
          <Tabs>
            <TabPane key="diff" tab="Diff">
              <div class="rounded bg-gray-50 p-4 dark:bg-gray-900">
                <div class="mb-2 text-sm text-gray-500">
                  {{ selectedFile }}
                </div>
                <pre
                  class="overflow-auto text-sm leading-relaxed"
                ><code>{{ diffContent }}</code></pre>
              </div>
            </TabPane>
            <TabPane key="preview" tab="Preview">
              <div class="p-4 text-center text-gray-400">
                文件预览功能待实现
              </div>
            </TabPane>
          </Tabs>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
