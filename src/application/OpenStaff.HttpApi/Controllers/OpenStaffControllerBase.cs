using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Controllers;

public abstract class OpenStaffControllerBase: ControllerBase
{
    protected IServiceProvider ServiceProvider => HttpContext.RequestServices;
    protected TService GetRequiredService<TService>() 
        where TService : notnull
        => ServiceProvider.GetRequiredService<TService>();
}
public abstract class OpenStaffServiceControllerBase<TService> : OpenStaffControllerBase 
    where TService : class
{
    protected TService Service=> GetRequiredService<TService>();
}
