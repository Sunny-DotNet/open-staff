namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 返回运行时已接受的逻辑消息标识。
/// en: Returns the logical message identifier accepted by the runtime.
/// </summary>
/// <param name="MessageId">
/// zh-CN: 已被运行时登记的消息标识。
/// en: The message identifier registered by the runtime.
/// </param>
public readonly record struct CreateMessageResponse(Guid MessageId);
