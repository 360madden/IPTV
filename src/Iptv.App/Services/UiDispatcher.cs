using System.Windows;

namespace Iptv.App.Services;

public static class UiDispatcher
{
    public static void Run(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
