1\.供应商Github Copilot需要实现设备登录获取token

2.MCP配置的逻辑不对，应该有个配置中心，对Config进行CRUD操作，现在点击配置后的弹窗，对于怎么操作时一头雾水的

3\.很多CRUD操作不协调，比如供应商管理,我打开一个Account进行编辑，之前配置的值不显示(理论上应该ApiKey不显示，其他都显示，可以在ApiKey加个特性决定时候编辑时隐藏)

4\.供应商管理应该加个查看模型的按钮，弹窗显示这个Account加载出来的models

5\.所有的AppService原则上都是传入一个标准的请求参数，比如TestChatAsync(TestChatRequest request,CancellationToken ct = default),而不是现在的TestChatAsync(Guid id, string message, CancellationToken ct = default);

6.TestChatAsync这里应该把AgentRole创建成一个AIAgent进行对话，前端页面应该在左边显示控制台(可以展开隐藏)，实时配置参数进行对话(TestChatRequest里有个AgentRoleInput?如果有值则用这个对象创建Agent,否则根据ID去数据库拿)，点击保存会将配置保存到数据库

7\.前端页面的Layout可以优化一下,根据对项目的了解，你设计个logo换上

