using System;
using System.Windows;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Community.PowerToys.Run.Plugin.Everything.Everything;
using Community.PowerToys.Run.Plugin.Everything.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Everything;
using Wox.Plugin.Everything.Everything;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.Everything
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable
    {

        public const string DLL = "Everything.dll";
        private readonly EverythingApi _api = new EverythingApi();



        private PluginInitContext _context;

        private Settings _settings;
        private PluginJsonStorage<Settings> _storage;
        private CancellationTokenSource _updateSource;

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            if (_updateSource != null && !_updateSource.IsCancellationRequested)
            {
                _updateSource.Cancel();
                Log.Debug($"cancel init {_updateSource.Token.GetHashCode()} {Thread.CurrentThread.ManagedThreadId} {query.RawQuery}", GetType());
                _updateSource.Dispose();
            }
            var source = new CancellationTokenSource();
            _updateSource = source;
            var token = source.Token;

            var results = new List<Result>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                var keyword = query.Search;

                try
                {
                    if (token.IsCancellationRequested) { return results; }
                    var searchList = _api.Search(keyword, token, _settings.MaxSearchCount);
                    if (token.IsCancellationRequested) { return results; }
                    for (int i = 0; i < searchList.Count; i++)
                    {
                        if (token.IsCancellationRequested) { return results; }
                        SearchResult searchResult = searchList[i];
                        var r = CreateResult(keyword, searchResult, i);
                        results.Add(r);
                    }
                }
                catch (IPCErrorException)
                {
                    results.Add(new Result
                    {
                        Title = Resources.wox_plugin_everything_is_not_running,
                        IcoPath = "Images\\warning.png"
                    });
                }
                catch (Exception e)
                {
                    Log.Error("Query Error"+e,GetType());
                    results.Add(new Result
                    {
                        Title = Resources.wox_plugin_everything_query_error,
                        SubTitle = e.Message,
                        Action = _ =>
                        {
                            Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            _context.API.ShowMsg(Resources.wox_plugin_everything_copied, null, string.Empty);
                            return false;
                        },
                        IcoPath = "Images\\error.png"
                    });
                }
            }

            return results;
        }

        private Result CreateResult(string keyword, SearchResult searchResult, int index)
        {
            var path = searchResult.FullPath;

            string workingDir = null;
            if (_settings.UseLocationAsWorkingDir)
                workingDir = Path.GetDirectoryName(path);

            var r = new Result
            {
                Score = _settings.MaxSearchCount - index,
                Title = searchResult.FileName,
                TitleHighlightData = searchResult.FileNameHightData,
                SubTitle = searchResult.FullPath,
                SubTitleHighlightData = searchResult.FullPathHightData,
                IcoPath = searchResult.FullPath,
                Action = c =>
                {
                    bool hide;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true,
                            WorkingDirectory = workingDir
                        });
                        hide = true;
                    }
                    catch (Win32Exception)
                    {
                        var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                        var message = "Can't open this file";
                        _context.API.ShowMsg(name, message, string.Empty);
                        hide = false;
                    }

                    return hide;
                },
                ContextData = searchResult,
                
            };
            return r;
        }



        private List<MyContextMenu> GetDefaultContextMenu()
        {
            List<MyContextMenu> defaultContextMenus = new List<MyContextMenu>();
            MyContextMenu openFolderContextMenu = new MyContextMenu
            {
                Name = Resources.wox_plugin_everything_open_containing_folder,
                Command = "explorer.exe",
                Argument = " /select,\"{path}\"",
                Glyph = "\xE8B7" // Folder
            };

            defaultContextMenus.Add(openFolderContextMenu);

            string editorPath = string.IsNullOrEmpty(_settings.EditorPath) ? "notepad.exe" : _settings.EditorPath;

            MyContextMenu openWithEditorContextMenu = new MyContextMenu
            {
                Name = string.Format(Resources.wox_plugin_everything_open_with_editor, Path.GetFileNameWithoutExtension(editorPath)),
                Command = editorPath,
                Argument = " \"{path}\"",
                Glyph = "\xE70B"
            };

            defaultContextMenus.Add(openWithEditorContextMenu);

            return defaultContextMenus;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
            if (_settings.MaxSearchCount <= 0)
            {
                _settings.MaxSearchCount = Settings.DefaultMaxSearchCount;
            }

            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            const string sdk = "EverythingSDK";
            var sdkDirectory = Path.Combine(pluginDirectory, sdk, CpuType());
            var sdkPath = Path.Combine(sdkDirectory, DLL);
            Log.Debug($"sdk path <{sdkPath}>",GetType());
            // Constant.EverythingSDKPath = sdkPath;
            _api.Load(sdkPath);
        }

        public string Name { get => GetTranslatedPluginTitle(); }
        public string Description { get => GetTranslatedPluginDescription(); }

        private static string CpuType()
        {
            if (!Environment.Is64BitProcess)
            {
                return "x86";
            }
            else
            {
                return "x64";
            }
            
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.wox_plugin_everything_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.wox_plugin_everything_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            SearchResult record = selectedResult.ContextData as SearchResult;
            var contextMenus = new List<ContextMenuResult>();
            if (record == null) return contextMenus;

            List<MyContextMenu> availableContextMenus = new List<MyContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());
            availableContextMenus.AddRange(_settings.ContextMenus);

            if (record.Type == ResultType.File)
            {
                foreach (MyContextMenu contextMenu in availableContextMenus)
                {
                    var menu = contextMenu;
                    contextMenus.Add(new ContextMenuResult
                    {
                        Title = contextMenu.Name,
                        Action = _ =>
                        {
                            string argument = menu.Argument.Replace("{path}", record.FullPath);
                            try
                            {
                                Process.Start(menu.Command, argument);
                            }
                            catch
                            {
                                _context.API.ShowMsg(string.Format(Resources.wox_plugin_everything_canot_start, record.FullPath), string.Empty, string.Empty);
                                return false;
                            }
                            return true;
                        },
                        Glyph = contextMenu.Glyph,
                        FontFamily = "Segoe MDL2 Assets",
                    });
                }
            }

            var icoPath = "\xE8C8";
            contextMenus.Add(new ContextMenuResult
            {
                Title = Resources.wox_plugin_everything_copy_path,
                Action = (context) =>
                {
                    Clipboard.SetText(record.FullPath);
                    return true;
                },
                Glyph = icoPath,
                FontFamily = "Segoe MDL2 Assets",
            });

            contextMenus.Add(new ContextMenuResult
            {
                Title = Resources.wox_plugin_everything_copy,
                Action = (context) =>
                {
                    Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { record.FullPath });
                    return true;
                },
                Glyph = "\xf413", // copy to
                FontFamily = "Segoe MDL2 Assets",
            });

            if (record.Type == ResultType.File || record.Type == ResultType.Folder)
                contextMenus.Add(new ContextMenuResult
                {
                    Title = Resources.wox_plugin_everything_delete,
                    Action = (context) =>
                    {
                        try
                        {
                            if (record.Type == ResultType.File)
                                System.IO.File.Delete(record.FullPath);
                            else
                                System.IO.Directory.Delete(record.FullPath);
                        }
                        catch
                        {
                            _context.API.ShowMsg(string.Format(Resources.wox_plugin_everything_canot_delete, record.FullPath), string.Empty, string.Empty);
                            return false;
                        }

                        return true;
                    },
                    Glyph = "\xE74D",
                    FontFamily = "Segoe MDL2 Assets",
                });

            return contextMenus;
        }


        public IEnumerable<PluginAdditionalOption> AdditionalOptions { get; }

    }
}
