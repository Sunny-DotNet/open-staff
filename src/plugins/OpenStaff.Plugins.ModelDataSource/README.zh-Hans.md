# OpenStaff.Plugins.ModelDataSource

## 项目用途

`OpenStaff.Plugins.ModelDataSource` 为 OpenStaff 提供共享的模型目录能力。它定义了供 Provider 和应用服务读取模型供应商与模型能力的统一契约，并内置默认的 `models.dev` 实现。

## 在插件体系中的角色

这个项目属于 Provider 生态中的插件式模块，不属于 marketplace 家族。它的职责是向以下组件提供规范化后的模型元数据：

- Provider 协议实现
- 暴露模型目录接口的应用服务
- 启动时需要初始化模型目录的 API 层
- 可选读取模型目录信息的厂商级 Agent Provider

## 关键抽象与数据源

- `IModelDataSource`
  - 生命周期方法：`InitializeAsync`、`RefreshAsync`
  - 状态属性：`SourceId`、`DisplayName`、`IsReady`、`LastUpdatedUtc`
  - 查询方法：
    - `GetVendorsAsync`
    - `GetModelsAsync`
    - `GetModelsByVendorAsync`
    - `GetModelAsync`

- 模型记录与枚举
  - `ModelVendor`
  - `ModelData`
  - `ModelLimits`
  - `ModelPricing`
  - `ModelModality`
  - `ModelCapability`

- `ModelsDevModelDataSource`
  - 下载 `https://models.dev/api.json`
  - 解析按供应商分组的 JSON 结构
  - 归一化模态、能力、限制、价格和发布时间
  - 将原始数据缓存到 `%USERPROFILE%\.staff\models-dev.json`

- `ModelDataSourceModule`
  - 依赖 `OpenStaffCoreModule`
  - 以单例方式注册 `ModelsDevModelDataSource`
  - 将 `IModelDataSource` 解析到同一个单例实例

## 依赖关系

直接项目依赖：

- `OpenStaff.Core`

默认实现的运行时依赖还包括：

- 远程拉取所需的 `HttpClient`
- 用于缓存落盘的本地文件系统
- 用于解析的 `System.Text.Json`

## 集成点

- `OpenStaff.Application\OpenStaffApplicationModule` 引入了 `ModelDataSourceModule`
- `OpenStaff.Provider.Abstractions\ProviderAbstractionsModule` 依赖该模块，因此 Provider 协议始终可以解析 `IModelDataSource`
- `OpenStaff.HttpApi.Host\Program.cs` 在应用启动时调用 `InitializeAsync()` 初始化共享数据源
- `OpenStaff.Application\ModelData\ModelDataAppService` 基于该数据源提供状态、刷新、供应商和模型查询能力
- `OpenStaff.HttpApi\Controllers\ModelDataController` 通过 `api/models-dev` 暴露对应接口
- Provider 协议基类会利用该数据源枚举厂商模型，并筛选支持文本输入、文本输出和函数调用的模型

## 运行与维护说明

- 默认数据源标识为 `models.dev`。
- 如果本地缓存文件已存在，初始化会先读取缓存，再后台异步刷新远程数据。
- 如果没有缓存，初始化会以前台刷新方式完成首轮加载，确保数据源可用。
- 远程刷新失败时，只要本地缓存可用，就会回退到缓存。
- 本地缓存损坏时会被忽略，后续远程刷新仍有机会恢复目录数据。
- 解析后的供应商和模型索引保存在并发字典中，并在成功解析后整体替换。
- 如果启动阶段尚未完成初始化，`EnsureReady()` 会在首次查询时尝试按需加载本地缓存。
- 默认单例会自行创建 `HttpClient`；测试或自定义组合时也可以显式注入。
- 接口能力比当前默认实现更宽，后续可以替换为其他模型目录来源，而无需改动消费方。
