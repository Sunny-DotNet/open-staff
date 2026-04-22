using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff;

public abstract class ServiceBase(IServiceProvider serviceProvider)
{
    protected T GetRequiredService<T>() where T : notnull => serviceProvider.GetRequiredService<T>();
    protected T? GetService<T>() => serviceProvider.GetService<T>();
    protected T CreateInstance<T> (params object[] parameters) => ActivatorUtilities.CreateInstance<T>(serviceProvider, parameters);
}
