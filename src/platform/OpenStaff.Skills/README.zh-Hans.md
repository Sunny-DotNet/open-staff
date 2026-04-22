# OpenStaff.Skills 需求设计

## 1. 文档目标

本文档定义 `src\platform\OpenStaff.Skills` 的首版需求范围，目标是把模块边界先收窄、收清楚。

首版 `OpenStaff.Skills` 只解决 4 件事：

- Skill 搜索
- Skill 查看
- Skill 安装
- Skill 卸载

其它能力先不放进这一版，以免模块一开始就变复杂。

---

## 2. 设计原则

### 2.1 简单优先

首版只做最直接的能力，不预埋过多抽象，不提前拆出太多层。

### 2.2 模块独立

搜索、查看、安装、卸载这些规则由 `OpenStaff.Skills` 自己负责，外层 HTTP、CLI、前端只负责调用。

### 2.3 文件系统真源

Skill 安装结果以受管目录为准：

- 安装：把 Skill 下载到 `~/.staff/skills`
- 卸载：删除对应 Skill 目录

### 2.4 单一数据源优先

首版目录源只接 `skills.sh`，只做：

- 搜索
- 查看单个 Skill 详情

---

## 3. 范围

### 3.1 本期范围

#### A. 搜索

- 从 `skills.sh` 搜索 Skill
- 支持关键字检索
- 支持查看搜索结果基础信息

#### B. 查看

- 查看单个 Skill 的详情
- 展示 owner、repo、skillId、名称、描述、仓库地址等基础信息

#### C. 安装

- 直接把 Skill 下载到 `~/.staff/skills`
- 支持覆盖已有目录或重新下载
- 已安装状态以本地目录是否存在为准

#### D. 卸载

- 直接删除受管目录中的 Skill 目录
- 删除后即视为卸载完成

### 3.2 明确不在本期范围

- Skill 绑定
- Skill 运行时注入
- `AgentSkillsProvider` 接线
- `run_skill_script`
- 多来源聚合
- 安装记录、索引、复杂状态机
- 复杂卸载检查

---

## 4. 模块定位

首版内部结构只保留：

```text
OpenStaff.Skills
└─ Skills
   ├─ Models
   ├─ Sources
   └─ Services
```

### 4.1 Models

放模块自己的基础模型，例如：

- `SkillCatalogEntry`
- `SkillCatalogSource`
- `InstalledSkill`

### 4.2 Sources

放目录源接入逻辑。

首版只需要：

- `skills.sh` source

### 4.3 Services

放模块主业务能力，例如：

- 搜索 Skill
- 获取 Skill 详情
- 下载 Skill
- 删除 Skill

---

## 5. 核心模型

### 5.1 SkillCatalogSource

表示一个目录源。

关键字段：

- `Key`
- `DisplayName`

### 5.2 SkillCatalogEntry

表示搜索结果或详情对象。

关键字段：

- `SourceKey`
- `Owner`
- `Repo`
- `SkillId`
- `Name`
- `DisplayName`
- `Description`
- `RepositoryUrl`

### 5.3 InstalledSkill

表示一个已下载到受管目录的 Skill。

关键字段：

- `SkillId`
- `Owner`
- `Repo`
- `Directory`

---

## 6. 核心服务边界

首版建议只保留少量核心服务：

- `ISkillCatalogService`
  - 搜索
  - 详情
- `ISkillInstallService`
  - 下载到受管目录
  - 扫描已安装目录
  - 删除 Skill 目录

不再继续提前拆出绑定、运行时、CLI、持久化等独立层。

---

## 7. 外部适配边界

以下都属于外层适配：

- `SkillsController`
- 应用层 API Service
- 前端页面

它们只负责把请求转给 `OpenStaff.Skills`，不自己决定搜索源、安装目录或卸载规则。

---

## 8. 一句话结论

`OpenStaff.Skills` 首版先做成一个**很轻的 Skill 目录与下载模块**：`skills.sh` 负责搜索和查看，安装就是下载到受管目录，卸载就是删除目录。
