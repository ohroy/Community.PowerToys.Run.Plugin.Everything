using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Everything
{
    public class Settings
    {
        public const int DefaultMaxSearchCount = 30;

        public string EditorPath { get; set; } = "";

        public List<MyContextMenu> ContextMenus = new List<MyContextMenu>();

        public int MaxSearchCount { get; set; } = DefaultMaxSearchCount;

        public bool UseLocationAsWorkingDir { get; set; } = false;
    }

    /// <summary>
    /// Context Menu
    /// </summary>
    public class MyContextMenu
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Argument { get; set; }
        public string Glyph { get; set; }
    }
}