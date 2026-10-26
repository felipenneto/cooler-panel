using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CoolerPanel {
    internal static class Startup {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name="CoolerPanel";
        internal static string Command { get { return "\"" + Application.ExecutablePath + "\" --tray"; } }
        internal static bool IsEnabled {
            get { using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key!=null && string.Equals(key.GetValue(Name) as string,Command,StringComparison.OrdinalIgnoreCase); }
        }
        internal static void SetEnabled(bool enabled) {
            using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
                if(enabled) key.SetValue(Name,Command,RegistryValueKind.String);
                else key.DeleteValue(Name,false);
            }
        }
    }
}
