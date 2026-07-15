using Underlings.Modules;

namespace Henchman.Helpers;

public static class Utils
{
    internal static bool IsPluginBusy => Running;

    public static TConfig? GetFeatureConfig<TUI, TConfig>()
            where TUI : ModuleUI => ModuleRegistry.GetConfig<TUI, TConfig>();

    public static string ToText(this Category c) => c switch
                                                    {
                                                            Category.Combat      => "Combat",
                                                            Category.Exploration => "Exploration",
                                                            Category.Economy     => "Economy",
                                                            Category.System      => "System",
                                                            _                    => throw new ArgumentOutOfRangeException()
                                                    };
}
