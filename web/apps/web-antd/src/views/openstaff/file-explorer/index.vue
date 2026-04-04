<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import type { TreeProps } from 'ant-design-vue';

import { Card, Col, Empty, Row, Tree } from 'ant-design-vue';

import { $t } from '#/locales';

const treeData: TreeProps['treeData'] = [
  {
    title: 'src',
    key: 'src',
    children: [
      {
        title: 'auth',
        key: 'src/auth',
        children: [
          { title: 'auth.service.ts', key: 'src/auth/auth.service.ts', isLeaf: true },
          { title: 'auth.controller.ts', key: 'src/auth/auth.controller.ts', isLeaf: true },
          { title: 'auth.module.ts', key: 'src/auth/auth.module.ts', isLeaf: true },
        ],
      },
      {
        title: 'user',
        key: 'src/user',
        children: [
          { title: 'user.service.ts', key: 'src/user/user.service.ts', isLeaf: true },
          { title: 'user.controller.ts', key: 'src/user/user.controller.ts', isLeaf: true },
          { title: 'user.module.ts', key: 'src/user/user.module.ts', isLeaf: true },
          { title: 'user.entity.ts', key: 'src/user/user.entity.ts', isLeaf: true },
        ],
      },
      { title: 'app.module.ts', key: 'src/app.module.ts', isLeaf: true },
      { title: 'main.ts', key: 'src/main.ts', isLeaf: true },
    ],
  },
  { title: 'package.json', key: 'package.json', isLeaf: true },
  { title: 'tsconfig.json', key: 'tsconfig.json', isLeaf: true },
  { title: 'README.md', key: 'README.md', isLeaf: true },
];

const selectedFile = ref('');
const fileContent = ref('');

const mockFileContents: Record<string, string> = {
  'src/main.ts': `import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  await app.listen(3000);
}
bootstrap();`,
  'src/app.module.ts': `import { Module } from '@nestjs/common';
import { AuthModule } from './auth/auth.module';
import { UserModule } from './user/user.module';

@Module({
  imports: [AuthModule, UserModule],
})
export class AppModule {}`,
  'src/auth/auth.service.ts': `import { Injectable } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { UserService } from '../user/user.service';

@Injectable()
export class AuthService {
  constructor(
    private userService: UserService,
    private jwtService: JwtService,
  ) {}

  async validateUser(username: string, password: string) {
    const user = await this.userService.findByUsername(username);
    if (user) {
      return user;
    }
    return null;
  }

  async login(user: any) {
    const payload = { username: user.username, sub: user.id };
    return { access_token: this.jwtService.sign(payload) };
  }
}`,
};

function onSelectFile(selectedKeys: (number | string)[]) {
  const key = selectedKeys[0] as string;
  if (key) {
    selectedFile.value = key;
    fileContent.value =
      mockFileContents[key] || `// ${key}\n// 文件内容加载中...`;
  }
}
</script>

<template>
  <Page :title="$t('openstaff.fileExplorer.title')">
    <Row :gutter="16" class="h-[calc(100vh-200px)]">
      <!-- 左侧：文件树 -->
      <Col :span="6">
        <Card
          :title="$t('openstaff.fileExplorer.fileTree')"
          class="h-full overflow-auto"
        >
          <Tree
            :tree-data="treeData"
            default-expand-all
            show-icon
            @select="onSelectFile"
          />
        </Card>
      </Col>

      <!-- 右侧：文件内容 -->
      <Col :span="18">
        <Card
          :title="selectedFile || $t('openstaff.fileExplorer.selectFile')"
          class="h-full overflow-auto"
        >
          <div v-if="fileContent" class="rounded bg-gray-50 p-4 dark:bg-gray-900">
            <pre
              class="overflow-auto text-sm leading-relaxed"
            ><code>{{ fileContent }}</code></pre>
          </div>
          <Empty
            v-else
            :description="$t('openstaff.fileExplorer.selectFile')"
          />
        </Card>
      </Col>
    </Row>
  </Page>
</template>
