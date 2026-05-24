namespace PITS.Plugins;

public interface IHasSidebarContent
{
    Type? SidebarUserInfoComponentType { get; }
    Type? SidebarFooterComponentType { get; }
}
