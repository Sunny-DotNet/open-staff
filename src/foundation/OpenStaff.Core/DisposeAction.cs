using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff;

public class DisposeAction : IDisposable
{
    public static IDisposable Create(Action action) => new DisposeAction(action);
    private Action Action { get; }
    private DisposeAction(Action action)
    {
        Action = action;
    }


    public void Dispose() => Action?.Invoke();
}