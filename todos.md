1.IAgentProvider的CreateAgent要改成Task<AIAgent> CreateAgentAsync(AgentRole role,AgentContext context, ResolvedProvider provider);

创建AIAgent的时候需要一些额外的参数，比如工作目录，会话ID，这些都在AgentContext进行传输

2.GitHubCopilot的Agent允许配置的只有模型，其他参数都要在配置界面忽略掉，

3\.理论上这些Vendor的智能体支持的模型都是有限的，是不是可以考虑加一个IVendorAgentProvider=>IAgentProvider，然后追加一个签名GetModelsAsync供前端选择

4\.如何做好这个架构以便兼容各大厂的官方Agent



