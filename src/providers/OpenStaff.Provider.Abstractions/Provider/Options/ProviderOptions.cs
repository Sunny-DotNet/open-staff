using OpenStaff.Provider.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Options;

/// <summary>
/// Provider 注册选项，维护可发现的协议类型列表。
/// Provider registration options that keep track of discoverable protocol types.
/// </summary>
public class ProviderOptions
{
    private readonly List<Type> _protocols = new();

    /// <summary>
    /// 当前已注册的协议类型集合。
    /// Currently registered protocol types.
    /// </summary>
    public IReadOnlyCollection<Type> Protocols => _protocols.AsReadOnly();

    /// <summary>
    /// 注册一个协议类型，重复注册会被忽略。
    /// Registers a protocol type and ignores duplicate registrations.
    /// </summary>
    /// <typeparam name="TProtocol">
    /// 要注册的协议类型。
    /// Protocol type to register.
    /// </typeparam>
    public void AddProtocol<TProtocol>() where TProtocol : IProtocol
    {
        if (_protocols.Contains(typeof(TProtocol))) return;
        _protocols.Add(typeof(TProtocol));
    }
}
