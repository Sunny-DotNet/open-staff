using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Options;

/// <summary>
/// OpenStaff 基础选项 / Core OpenStaff options.
/// </summary>
public class OpenStaffOptions
{
    /// <summary>
    /// OpenStaff 工作目录；属性名保留历史拼写以兼容现有配置 / Working directory for OpenStaff data. The property name keeps its historical spelling for configuration compatibility.
    /// </summary>
    public required string WorkingDirectory { get; set; }

    /// <summary>
    /// 使用默认用户目录初始化选项 / Initialize options with the default user-scoped directory under <c>%USERPROFILE%\\.staff</c>.
    /// </summary>
    public OpenStaffOptions()
    {
        WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Environment.CurrentDirectory = WorkingDirectory;
    }
}
