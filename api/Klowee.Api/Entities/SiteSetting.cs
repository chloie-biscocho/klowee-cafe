using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
