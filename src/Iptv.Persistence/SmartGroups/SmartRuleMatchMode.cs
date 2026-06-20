namespace Iptv.Persistence.SmartGroups;

public enum SmartRuleMatchMode
{
    ContainsAny = 0,
    NameStartsWith = 1,
    Regex = 2,
    GroupEquals = 3,
    CategoryEquals = 4
}
