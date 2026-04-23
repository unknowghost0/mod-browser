using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using DNA.CastleMinerZ;
using DNA.Drawing;
using DNA.Drawing.UI;
using DNA.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ModBrowser
{
    internal sealed class ModBrowserScreen : Screen
    {
        private enum BrowserViewMode
        {
            Browser = 0,
            Publish = 1,
            CreateMod = 2
        }

        private enum BrowserSourceMode
        {
            All = 0,
            Official = 1,
            Community = 2
        }

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue",
            "decimal","default","delegate","do","double","else","enum","event","explicit","extern","false","finally",
            "fixed","float","for","foreach","goto","if","implicit","in","int","interface","internal","is","lock","long",
            "namespace","new","null","object","operator","out","override","params","private","protected","public","readonly",
            "ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string","struct","switch","this",
            "throw","true","try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile",
            "while","var","get","set","value","partial","where","yield","async","await"
        };

        private sealed class CreateModField
        {
            public string Label;
            public string Key;
            public string Value;
            public string Placeholder;
            public bool Multiline;
        }

        private sealed class ManagedReferenceSpec
        {
            public string Include;
            public string HintPath;
            public bool WriteSpecificVersionFalse;
            public bool WritePrivate;
            public bool PrivateValue;
        }

        private static readonly HashSet<string> AutoManagedAssetFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets", "Audio", "Content", "Effects", "Embedded", "Fonts", "Images", "Resources", "Shaders", "Sounds", "Sprites", "Textures"
        };

        private static readonly HashSet<string> AutoManagedReferenceFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Libs", "Libraries", "References"
        };

        private static readonly HashSet<string> AutoManagedIgnoredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "CommunityEntry"
        };

        internal static bool IsOpen { get; private set; }

        private readonly CastleMinerZGame _game;
        private Texture2D _white;
        private SpriteFont _titleFont;
        private SpriteFont _smallFont;
        private bool _initialized;

        private readonly List<ModCatalogEntry> _mods = new List<ModCatalogEntry>();
        private readonly List<ModCatalogEntry> _allMods = new List<ModCatalogEntry>();
        private readonly List<int> _visibleIndices = new List<int>();
        private int _selectedIndex = -1;
        private int _scroll;
        private bool _mouseLeftWasDown;
        private bool _focusSearch;
        private string _searchText = "";

        private Rectangle _panelRect;
        private Rectangle _leftRect;
        private Rectangle _rightRect;
        private Rectangle _statusRect;
        private Rectangle _searchRect;
        private Rectangle _sourceAllRect;
        private Rectangle _sourceOfficialRect;
        private Rectangle _sourceCommunityRect;
        private Rectangle _publishViewRect;
        private Rectangle _createModViewRect;
        private Rectangle _backToBrowserRect;
        private Rectangle _refreshRect;
        private Rectangle _installRect;
        private Rectangle _closeRect;
        private Rectangle _createEditorRect;
        private Rectangle _createInfoRect;
        private Rectangle _createFieldFilterRect;
        private Rectangle _createEditorSearchRect;
        private Rectangle _createFieldsTabRect;
        private Rectangle _createTemplateTabRect;
        private Rectangle _createModSelectorRect;
        private bool _createShowTemplateTab;
        private Rectangle _createSaveDraftRect;
        private Rectangle _createPublishRect;
        private Rectangle _createGenerateRect;
        private Rectangle _createBuildRect;
        private Rectangle _createDiagnosticsRect;
        private Rectangle _createResetRect;
        private Rectangle _createVisualStudioRect;
        private Rectangle _createMoveToModsRect;
        private Rectangle _createPromptPanelRect;
        private Rectangle _createPromptInputRect;
        private Rectangle _createPromptConfirmRect;
        private Rectangle _createPromptCancelRect;
        private Rectangle _reloadNoticePanelRect;
        private Rectangle _reloadNoticeCloseRect;
        private Rectangle _reloadNoticeStayRect;
        private Rectangle _publishPromptPanelRect;
        private Rectangle _publishPromptConfirmRect;
        private Rectangle _publishPromptCancelRect;
        private Rectangle _buildPopupPanelRect;
        private Rectangle _buildPopupBarRect;
        private Rectangle _buildPopupOkRect;
        private Rectangle _msbuildPromptPanelRect;
        private Rectangle _msbuildPromptYesRect;
        private Rectangle _msbuildPromptNoRect;
        private Rectangle _buildModSelectorPanelRect;
        private Rectangle _buildModSelectorConfirmRect;
        private Rectangle _buildModSelectorCancelRect;
        private BrowserViewMode _viewMode = BrowserViewMode.Browser;
        private BrowserSourceMode _sourceMode = BrowserSourceMode.All;
        private readonly List<CreateModField> _createFields = new List<CreateModField>();
        private readonly List<CreateModField> _createCodeFields = new List<CreateModField>();
        private string[] _availableModFolders = new string[0];
        private int _selectedModFolderIndex;
        private string _currentModRootPath = "";
        private readonly List<int> _visibleCreateFieldIndices = new List<int>();
        private bool _createGroupExpanded = true;
        private int _createSelectedFieldIndex;
        private int _createCaretIndex;
        private bool _focusCreateEditor;
        private bool _focusCreateFieldFilter;
        private bool _focusCreateEditorSearch;
        private string _createFieldFilterText = "";
        private string _createEditorSearchText = "";
        private int _createEditorScroll;
        private bool _showCreateFolderPrompt;
        private string _createFolderPromptText = "";
        private bool _showPublishPrompt;
        private bool _showBuildPopup;
        private bool _showMsbuildPrompt;
        private volatile bool _buildBusy;
        private bool _buildCompleted;
        private bool _buildSucceeded;
        private string _buildPopupMessage = "Press BUILD to compile the current mod.";
        private string _buildPendingProjectPath = "";
        private string _buildLastSummary = "Warnings: 0 | Errors: 0";
        private int _buildLastWarningCount;
        private int _buildLastErrorCount;
        private List<string> _buildDiagLines = new List<string>();
        private bool _showBuildModSelectorPrompt;
        private string[] _buildModSelectorOptions = new string[0];
        private int _buildModSelectorSelectedIndex;
        private bool _downloadedAnyThisSession;
        private bool _showReloadNoticePrompt;
        private volatile bool _loading;
        private volatile bool _installing;
        private string _status = "Press REFRESH to load catalog.";
        private Color _statusColor = Color.LightGray;
        private bool _showCreateModWarning;
        private Rectangle _createModWarningPanelRect;
        private Rectangle _createModWarningOkButtonRect;
        private Rectangle _createModWarningBackButtonRect;
        private readonly object _previewSync = new object();
        private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> _previewByteCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _previewDownloadsInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _previewRequestedUrl;
        private string _previewLoadedUrl;
        private string _previewFailedUrl;
        private volatile bool _previewLoading;
        private byte[] _pendingPreviewBytes;
        private string _pendingPreviewUrl;
        private Texture2D _previewTexture;

        public ModBrowserScreen(CastleMinerZGame game) : base(true, true)
        {
            _game = game;
            ShowMouseCursor = true;
            CaptureMouse = false;
            ResetCreateModFields();
            RebuildVisibleCreateFieldIndices();
        }

        public override void OnPushed()
        {
            base.OnPushed();
            IsOpen = true;
            _downloadedAnyThisSession = false;
            _showReloadNoticePrompt = false;
            _showPublishPrompt = false;
            EnsureCreateTemplatesLoadedIfAny();
            BeginRefresh();
        }

        public override void OnPoped()
        {
            base.OnPoped();
            IsOpen = false;
            CaptureMouse = false;
            if (_previewTexture != null)
            {
                _previewTexture.Dispose();
                _previewTexture = null;
            }
            foreach (var kv in _previewCache)
            {
                if (kv.Value != null)
                    kv.Value.Dispose();
            }
            _previewCache.Clear();
            lock (_previewSync)
            {
                _previewByteCache.Clear();
                _previewDownloadsInFlight.Clear();
            }
        }

        protected override void OnDraw(GraphicsDevice device, SpriteBatch spriteBatch, GameTime gameTime)
        {
            EnsureInit(device);
            Layout(device);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
            spriteBatch.Draw(_white, Screen.Adjuster.ScreenRect, new Color(0, 0, 0, 190));
            spriteBatch.Draw(_white, _panelRect, new Color(18, 21, 27, 235));
            DrawBorder(spriteBatch, _panelRect, new Color(84, 115, 164, 255));

            var titlePos = new Vector2(_panelRect.Left + 20, _panelRect.Top + 16);
            spriteBatch.DrawString(_titleFont, "Mod Browser", titlePos + new Vector2(1, 1), Color.Black);
            spriteBatch.DrawString(_titleFont, "Mod Browser", titlePos, Color.White);

            spriteBatch.Draw(_white, _leftRect, new Color(26, 31, 39, 255));
            spriteBatch.Draw(_white, _rightRect, new Color(26, 31, 39, 255));
            spriteBatch.Draw(_white, _statusRect, new Color(20, 24, 31, 255));
            DrawBorder(spriteBatch, _leftRect, new Color(62, 75, 95, 255));
            DrawBorder(spriteBatch, _rightRect, new Color(62, 75, 95, 255));
            DrawBorder(spriteBatch, _statusRect, new Color(62, 75, 95, 255));

            DrawViewModeButtons(spriteBatch);
            if (_viewMode == BrowserViewMode.Browser)
            {
                DrawButtons(spriteBatch);
                DrawList(spriteBatch);
                DrawDetails(spriteBatch);
            }
            else
            {
                DrawCreateModView(spriteBatch);
            }
            DrawStatus(spriteBatch);
            ConsumePendingPreview(device);
            if (_showReloadNoticePrompt)
                DrawReloadNoticePrompt(spriteBatch);
            if (_showBuildModSelectorPrompt)
                DrawBuildModSelectorPrompt(spriteBatch);
            if (_showCreateModWarning)
                DrawCreateModWarningPrompt(spriteBatch);
            if (_showPublishPrompt)
                DrawPublishPrompt(spriteBatch);
            if (_showBuildPopup)
                DrawBuildPopup(spriteBatch);
            if (_showMsbuildPrompt)
                DrawMsbuildPrompt(spriteBatch);

            spriteBatch.End();
        }

        protected override bool OnPlayerInput(InputManager input, GameController controller, KeyboardInput chatpad, GameTime gameTime)
        {
            if (!CastleMinerZGame.Instance.IsActive)
                return false;

            if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                PopMe();
                return false;
            }

            var mouse = input.Mouse;
            var point = mouse.Position;
            bool leftDown = mouse.LeftButtonDown;
            bool leftPressed = mouse.LeftButtonPressed || (leftDown && !_mouseLeftWasDown);
            _mouseLeftWasDown = leftDown;

            if (_showReloadNoticePrompt)
            {
                if (leftPressed)
                {
                    if (_reloadNoticeCloseRect.Contains(point))
                    {
                        // Note: RequestReload no longer exists in new ModManager (DLLs hooked to memory)
                        PopMe();
                        return false;
                    }

                    if (_reloadNoticeStayRect.Contains(point))
                    {
                        _showReloadNoticePrompt = false;
                        return false;
                    }
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    // Note: RequestReload no longer exists in new ModManager (DLLs hooked to memory)
                    PopMe();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showReloadNoticePrompt = false;
                    return false;
                }

                return false;
            }

            if (_showCreateModWarning)
            {
                if (leftPressed && _createModWarningOkButtonRect.Contains(point))
                {
                    _showCreateModWarning = false;
                    _viewMode = BrowserViewMode.CreateMod;
                    _focusSearch = false;
                    EnsureCreateTemplatesLoadedIfAny();
                    RebuildVisibleCreateFieldIndices();
                    return false;
                }
                if (leftPressed && _createModWarningBackButtonRect.Contains(point))
                {
                    _showCreateModWarning = false;
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    _showCreateModWarning = false;
                    _viewMode = BrowserViewMode.CreateMod;
                    _focusSearch = false;
                    EnsureCreateTemplatesLoadedIfAny();
                    RebuildVisibleCreateFieldIndices();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showCreateModWarning = false;
                    return false;
                }
                return false;
            }

            if (_showBuildModSelectorPrompt)
            {
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Up) && _buildModSelectorSelectedIndex > 0)
                    _buildModSelectorSelectedIndex--;
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Down) && _buildModSelectorSelectedIndex < _buildModSelectorOptions.Length - 1)
                    _buildModSelectorSelectedIndex++;

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    _showBuildModSelectorPrompt = false;
                    ConfirmBuildModSelection();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showBuildModSelectorPrompt = false;
                    _status = "Build canceled.";
                    _statusColor = Color.LightGray;
                    return false;
                }
                return false;
            }

            if (_showPublishPrompt)
            {
                if (leftPressed)
                {
                    if (_publishPromptConfirmRect.Contains(point))
                    {
                        _showPublishPrompt = false;
                        CreateModStarterFiles();
                        return false;
                    }

                    if (_publishPromptCancelRect.Contains(point))
                    {
                        _showPublishPrompt = false;
                        _status = "Publish canceled.";
                        _statusColor = Color.LightGray;
                        return false;
                    }
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    _showPublishPrompt = false;
                    CreateModStarterFiles();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showPublishPrompt = false;
                    _status = "Publish canceled.";
                    _statusColor = Color.LightGray;
                    return false;
                }
                return false;
            }

            if (_showBuildPopup)
            {
                if (_buildCompleted && leftPressed && _buildPopupOkRect.Contains(point))
                    _showBuildPopup = false;
                if (_buildCompleted && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                    _showBuildPopup = false;
                if (_buildCompleted && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                    _showBuildPopup = false;
                return false;
            }

            if (_showMsbuildPrompt)
            {
                if (leftPressed)
                {
                    if (_msbuildPromptYesRect.Contains(point))
                    {
                        _showMsbuildPrompt = false;
                        try { Process.Start("https://visualstudio.microsoft.com/downloads/"); } catch { }
                        _status = "Opening Visual Studio download page...";
                        _statusColor = Color.LightGreen;
                        return false;
                    }
                    if (_msbuildPromptNoRect.Contains(point))
                    {
                        _showMsbuildPrompt = false;
                        _status = "Build canceled.";
                        _statusColor = Color.LightGray;
                        return false;
                    }
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showMsbuildPrompt = false;
                    return false;
                }
                return false;
            }

            if (_showCreateFolderPrompt)
            {
                if (leftPressed)
                {
                    if (_createPromptConfirmRect.Contains(point))
                    {
                        ConfirmCreateFolderPrompt();
                        return false;
                    }

                    if (_createPromptCancelRect.Contains(point))
                    {
                        _showCreateFolderPrompt = false;
                        _status = "Create mod canceled.";
                        _statusColor = Color.LightGray;
                        return false;
                    }
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    ConfirmCreateFolderPrompt();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _showCreateFolderPrompt = false;
                    _status = "Create mod canceled.";
                    _statusColor = Color.LightGray;
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Back) && _createFolderPromptText.Length > 0)
                {
                    _createFolderPromptText = _createFolderPromptText.Substring(0, _createFolderPromptText.Length - 1);
                    return false;
                }

                return false;
            }

            if (leftPressed)
            {
                if (_publishViewRect.Contains(point))
                {
                    _showCreateModWarning = true;
                    return false;
                }
                if (_createModViewRect.Contains(point))
                {
                    _showCreateModWarning = true;
                    return false;
                }
                if (_backToBrowserRect.Contains(point))
                {
                    _viewMode = BrowserViewMode.Browser;
                    return false;
                }

                if (_viewMode != BrowserViewMode.Browser)
                {
                    if (_viewMode == BrowserViewMode.CreateMod)
                    {
                        if (_createSaveDraftRect.Contains(point))
                        {
                            SaveCreateModDraft();
                            return false;
                        }
                        if (_createPublishRect.Contains(point))
                        {
                            OpenPublishPrompt();
                            return false;
                        }
                        if (_createGenerateRect.Contains(point))
                        {
                            OpenCreateFolderPrompt();
                            return false;
                        }
                        if (_createResetRect.Contains(point))
                        {
                            ResetCreateModFields();
                            _status = "Create Mod fields reset.";
                            _statusColor = Color.LightGreen;
                            return false;
                        }
                        if (_createVisualStudioRect.Contains(point))
                        {
                            LaunchVisualStudio();
                            return false;
                        }
                        if (_createMoveToModsRect.Contains(point) && _createShowTemplateTab)
                        {
                            MoveCompiledModsToSteamMods();
                            return false;
                        }
                        if (_createBuildRect.Contains(point) && !_createShowTemplateTab)
                        {
                            TryStartBuild();
                            return false;
                        }
                        if (_createFieldsTabRect.Contains(point))
                        {
                            _createShowTemplateTab = false;
                            RebuildVisibleCreateFieldIndices(); // Update indices for Mod Files tab.
                            return false;
                        }
                        if (_createTemplateTabRect.Contains(point))
                        {
                            _createShowTemplateTab = true;
                            RebuildVisibleCreateFieldIndices(); // Update indices for Template tab.
                            return false;
                        }
                        if (_createEditorRect.Contains(point))
                        {
                            _focusCreateEditor = true;
                            _focusCreateFieldFilter = false;
                            _focusCreateEditorSearch = false;
                            SetCreateCaretFromEditorClick(point);
                            return false;
                        }
                        if (_createFieldFilterRect.Contains(point))
                        {
                            _focusCreateFieldFilter = true;
                            _focusCreateEditor = false;
                            _focusCreateEditorSearch = false;
                            return false;
                        }
                        if (_createEditorSearchRect.Contains(point))
                        {
                            _focusCreateEditorSearch = true;
                            _focusCreateEditor = false;
                            _focusCreateFieldFilter = false;
                            return false;
                        }
                        if (_leftRect.Contains(point))
                        {
                            _focusCreateEditor = false;
                            _focusCreateFieldFilter = false;
                            _focusCreateEditorSearch = false;

                            int rowH = Math.Max(32, _smallFont.LineSpacing + 10);
                            int listTop = _createFieldFilterRect.Bottom + 6;
                            int row = (point.Y - listTop) / rowH;
                            if (_viewMode == BrowserViewMode.CreateMod)
                            {
                                if (row == 0)
                                {
                                    _createGroupExpanded = !_createGroupExpanded;
                                    return false;
                                }
                                if (_createGroupExpanded)
                                {
                                    int fileRow = row - 1;
                                    if (fileRow >= 0 && fileRow < _visibleCreateFieldIndices.Count)
                                    {
                                        SetSelectedCreateField(_visibleCreateFieldIndices[fileRow]);
                                        _focusCreateEditor = true;
                                    }
                                }
                            }
                            return false;
                        }
                    }

                    if (_closeRect.Contains(point))
                    {
                        RequestCloseOrShowReloadPrompt();
                        return false;
                    }
                    return false;
                }

                if (_refreshRect.Contains(point))
                {
                    BeginRefresh();
                    return false;
                }
                if (_installRect.Contains(point))
                {
                    BeginInstallSelected();
                    return false;
                }
                if (_closeRect.Contains(point))
                {
                    RequestCloseOrShowReloadPrompt();
                    return false;
                }
                if (_searchRect.Contains(point))
                {
                    _focusSearch = true;
                    return false;
                }
                if (_sourceAllRect.Contains(point))
                {
                    _sourceMode = BrowserSourceMode.All;
                    ApplySourceFilter();
                    return false;
                }
                if (_sourceOfficialRect.Contains(point))
                {
                    _sourceMode = BrowserSourceMode.Official;
                    ApplySourceFilter();
                    return false;
                }
                if (_sourceCommunityRect.Contains(point))
                {
                    _sourceMode = BrowserSourceMode.Community;
                    ApplySourceFilter();
                    return false;
                }
                if (_leftRect.Contains(point))
                {
                    _focusSearch = false;
                    int rowH = _smallFont.LineSpacing + 10;
                    int listTop = GetBrowserListTop();
                    int row = (point.Y - listTop) / rowH;
                    int vis = _scroll + row;
                    if (vis >= 0 && vis < _visibleIndices.Count)
                        _selectedIndex = _visibleIndices[vis];
                }
                else
                {
                    _focusSearch = false;
                }
            }

            int wheel = mouse.DeltaWheel;
            if (_viewMode == BrowserViewMode.Browser && wheel != 0 && _leftRect.Contains(point))
            {
                _scroll += wheel > 0 ? -1 : 1;
                int visibleRows = GetBrowserVisibleRows(_smallFont.LineSpacing + 10);
                int maxScroll = Math.Max(0, _visibleIndices.Count - visibleRows);
                if (_scroll < 0) _scroll = 0;
                if (_scroll > maxScroll) _scroll = maxScroll;
            }
            if (_viewMode == BrowserViewMode.CreateMod && wheel != 0 && _createEditorRect.Contains(point))
            {
                _createEditorScroll += wheel > 0 ? -3 : 3;
                var scrollField = GetSelectedCreateField();
                if (scrollField != null)
                {
                    string[] scrollLines = SplitEditorLines(scrollField.Value ?? "");
                    int lineStep = _smallFont.LineSpacing + 2;
                    int maxVis = Math.Max(1, (_createEditorRect.Height - 16) / lineStep);
                    int maxEditorScroll = Math.Max(0, scrollLines.Length - maxVis);
                    if (_createEditorScroll < 0) _createEditorScroll = 0;
                    if (_createEditorScroll > maxEditorScroll) _createEditorScroll = maxEditorScroll;
                }
                else
                {
                    if (_createEditorScroll < 0) _createEditorScroll = 0;
                }
            }

            if (_focusSearch)
            {
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Back) && _searchText.Length > 0)
                {
                    _searchText = _searchText.Substring(0, _searchText.Length - 1);
                    RebuildVisibleIndices();
                    ClampScrollToVisible();
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _focusSearch = false;
                    return false;
                }
            }

            if (_viewMode == BrowserViewMode.CreateMod)
            {
                // Mod switching with Ctrl+Left/Right
                bool ctrl = IsCtrlHeld();
                if (ctrl && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Left) && _availableModFolders.Length > 1)
                {
                    if (_selectedModFolderIndex > 0)
                        LoadModAtIndex(_selectedModFolderIndex - 1);
                    return false;
                }
                if (ctrl && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Right) && _availableModFolders.Length > 1)
                {
                    if (_selectedModFolderIndex < _availableModFolders.Length - 1)
                        LoadModAtIndex(_selectedModFolderIndex + 1);
                    return false;
                }
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateEditor)
            {
                var currentField = GetSelectedCreateField();
                bool shift = IsShiftHeld();
                bool ctrl = IsCtrlHeld();

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Back))
                {
                    BackspaceSelectedCreateField();
                    return false;
                }

                if (ctrl && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.C))
                {
                    if (currentField != null && !string.IsNullOrEmpty(currentField.Value))
                    {
                        System.Windows.Forms.Clipboard.SetText(currentField.Value);
                    }
                    return false;
                }

                if (ctrl && input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.V))
                {
                    try
                    {
                        string pastedText = System.Windows.Forms.Clipboard.GetText();
                        if (!string.IsNullOrEmpty(pastedText))
                        {
                            InsertIntoSelectedCreateField(pastedText);
                        }
                    }
                    catch { }
                    return false;
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Tab))
                {
                    MoveCreateSelection(shift ? -1 : 1);
                    return false;
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Left))
                {
                    MoveCreateCaretHorizontal(-1);
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Right))
                {
                    MoveCreateCaretHorizontal(1);
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Home))
                {
                    MoveCreateCaretHome();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.End))
                {
                    MoveCreateCaretEnd();
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Up))
                {
                    MoveCreateCaretVertical(-1);
                    return false;
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Down))
                {
                    MoveCreateCaretVertical(1);
                    return false;
                }

                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    if (shift && currentField != null && currentField.Multiline)
                        InsertIntoSelectedCreateField("\n");
                    return false;
                }
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateFieldFilter)
            {
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Back) && _createFieldFilterText.Length > 0)
                {
                    _createFieldFilterText = _createFieldFilterText.Substring(0, _createFieldFilterText.Length - 1);
                    RebuildVisibleCreateFieldIndices();
                }
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _focusCreateFieldFilter = false;
                    return false;
                }
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateEditorSearch)
            {
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Back) && _createEditorSearchText.Length > 0)
                    _createEditorSearchText = _createEditorSearchText.Substring(0, _createEditorSearchText.Length - 1);
                if (input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    _focusCreateEditorSearch = false;
                    return false;
                }
            }

            return base.OnPlayerInput(input, controller, chatpad, gameTime);
        }

        protected override bool OnChar(GameTime gameTime, char c)
        {
            if (_showCreateFolderPrompt)
            {
                if (!char.IsControl(c))
                    _createFolderPromptText += c;
                return false;
            }

            if (_focusSearch)
            {
                if (!char.IsControl(c))
                {
                    _searchText += c;
                    RebuildVisibleIndices();
                    ClampScrollToVisible();
                }
                return false;
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateEditor)
            {
                if (!char.IsControl(c))
                {
                    InsertIntoSelectedCreateField(c.ToString());
                }
                return false;
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateFieldFilter)
            {
                if (!char.IsControl(c))
                {
                    _createFieldFilterText += c;
                    RebuildVisibleCreateFieldIndices();
                }
                return false;
            }

            if (_viewMode == BrowserViewMode.CreateMod && _focusCreateEditorSearch)
            {
                if (!char.IsControl(c))
                    _createEditorSearchText += c;
                return false;
            }

            return base.OnChar(gameTime, c);
        }

        private void EnsureInit(GraphicsDevice device)
        {
            if (_initialized) return;
            _white = new Texture2D(device, 1, 1);
            _white.SetData(new[] { Color.White });
            _titleFont = _game._largeFont ?? _game._medFont;
            _smallFont = _game._smallFont ?? _game._medFont;
            _initialized = true;
        }

        private void Layout(GraphicsDevice device)
        {
            var safe = device.Viewport.Bounds;
            int panelW = (int)(safe.Width * 0.86f);
            int panelH = (int)(safe.Height * 0.82f);
            int panelX = safe.Center.X - panelW / 2;
            int panelY = safe.Center.Y - panelH / 2;
            _panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            int contentTop = panelY + 74;
            int contentBottom = panelY + panelH - 96;
            int contentH = contentBottom - contentTop;
            int leftW = (int)(panelW * 0.37f);
            _leftRect = new Rectangle(panelX + 18, contentTop, leftW, contentH);
            _rightRect = new Rectangle(_leftRect.Right + 14, contentTop, panelW - leftW - 50, contentH);
            _statusRect = new Rectangle(panelX + 18, contentBottom + 8, panelW - 36, 34);
            int sourceTabGap = 4;
            int sourceTabW = Math.Max(72, (_leftRect.Width - 14 - (sourceTabGap * 2)) / 3);
            _sourceAllRect = new Rectangle(_leftRect.Left + 6, _leftRect.Top + 6, sourceTabW, _smallFont.LineSpacing + 8);
            _sourceOfficialRect = new Rectangle(_sourceAllRect.Right + sourceTabGap, _sourceAllRect.Top, sourceTabW, _sourceAllRect.Height);
            _sourceCommunityRect = new Rectangle(_sourceOfficialRect.Right + sourceTabGap, _sourceAllRect.Top, sourceTabW, _sourceAllRect.Height);
            _searchRect = new Rectangle(_leftRect.Left + 6, _sourceAllRect.Bottom + 6, _leftRect.Width - 12, _smallFont.LineSpacing + 6);
            int modeY = panelY + 20;
            int modeW = 126;
            int modeGap = 8;
            _backToBrowserRect = new Rectangle(panelX + panelW - 18 - modeW, modeY, modeW, 26);
            _createModViewRect = new Rectangle(_backToBrowserRect.Left - modeGap - modeW, modeY, modeW, 26);
            _publishViewRect = Rectangle.Empty;

            int by = panelY + panelH - 48;
            int bw = 140;
            int gap = 10;
            _closeRect = new Rectangle(panelX + panelW - 18 - bw, by, bw, 30);
            _installRect = new Rectangle(_closeRect.Left - gap - bw, by, bw, 30);
            _refreshRect = new Rectangle(_installRect.Left - gap - bw, by, bw, 30);
            _createBuildRect = new Rectangle(_closeRect.Left - gap - 120, by, 120, 30);
            _createMoveToModsRect = new Rectangle(_closeRect.Left - gap - 120, by, 120, 30);
            _createVisualStudioRect = new Rectangle(_createBuildRect.Left - gap - 90, by, 90, 30);
            _createResetRect = new Rectangle(_createVisualStudioRect.Left - gap - bw, by, bw, 30);
            _createGenerateRect = new Rectangle(_createResetRect.Left - gap - bw, by, bw, 30);
            _createPublishRect = new Rectangle(_createGenerateRect.Left - gap - bw, by, bw, 30);
            _createSaveDraftRect = new Rectangle(_createPublishRect.Left - gap - bw, by, bw, 30);
            int createTabGap = 6;
            int createTabW = (_leftRect.Width - 12 - createTabGap) / 2;
            _createFieldsTabRect = new Rectangle(_leftRect.Left + 6, _leftRect.Top + 6, createTabW, 30);
            _createTemplateTabRect = new Rectangle(_createFieldsTabRect.Right + createTabGap, _leftRect.Top + 6, createTabW, 30);
            _createModSelectorRect = new Rectangle(_leftRect.Left + 6, _createFieldsTabRect.Bottom + 6, _leftRect.Width - 12, _smallFont.LineSpacing + 8);
            _createFieldFilterRect = new Rectangle(_leftRect.Left + 6, _createModSelectorRect.Bottom + 6, _leftRect.Width - 12, _smallFont.LineSpacing + 8);

            int createEditorSearchWidth = Math.Max(220, Math.Min(360, (int)(_rightRect.Width * 0.34f)));
            int createEditorSearchHeight = Math.Max(20, _smallFont.LineSpacing + 6);
            _createEditorSearchRect = new Rectangle(_rightRect.Right - createEditorSearchWidth - 10, _rightRect.Top + 4, createEditorSearchWidth, createEditorSearchHeight);

            int createHeaderHeight = Math.Max(_smallFont.LineSpacing + 12, _createEditorSearchRect.Height + 8);
            int createInfoHeight = Math.Max(146, _smallFont.LineSpacing * 6 + 18);
            int diagH = Math.Max(26, _smallFont.LineSpacing + 8);
            int createEditorTop = _rightRect.Top + createHeaderHeight + 8;
            int createEditorHeight = Math.Max(180, _rightRect.Height - createHeaderHeight - createInfoHeight - diagH - 30);
            _createEditorRect = new Rectangle(_rightRect.Left + 8, createEditorTop, _rightRect.Width - 16, createEditorHeight);
            _createDiagnosticsRect = new Rectangle(_rightRect.Left + 8, _createEditorRect.Bottom + 4, _rightRect.Width - 16, diagH);
            _createInfoRect = new Rectangle(_rightRect.Left + 8, _createDiagnosticsRect.Bottom + 4, _rightRect.Width - 16, createInfoHeight);

            int promptW = Math.Max(420, Math.Min(620, _panelRect.Width - 80));
            int promptH = 170;
            _createPromptPanelRect = new Rectangle(_panelRect.Center.X - (promptW / 2), _panelRect.Center.Y - (promptH / 2), promptW, promptH);
            _createPromptInputRect = new Rectangle(_createPromptPanelRect.Left + 16, _createPromptPanelRect.Top + 64, _createPromptPanelRect.Width - 32, _smallFont.LineSpacing + 10);
            _createPromptCancelRect = new Rectangle(_createPromptPanelRect.Right - 16 - 120, _createPromptPanelRect.Bottom - 16 - 30, 120, 30);
            _createPromptConfirmRect = new Rectangle(_createPromptCancelRect.Left - 10 - 140, _createPromptCancelRect.Top, 140, 30);

            int noticeW = Math.Min(700, _panelRect.Width - 120);
            int noticeH = 170;
            _reloadNoticePanelRect = new Rectangle(_panelRect.Center.X - (noticeW / 2), _panelRect.Center.Y - (noticeH / 2), noticeW, noticeH);
            _reloadNoticeCloseRect = new Rectangle(_reloadNoticePanelRect.Right - 16 - 150, _reloadNoticePanelRect.Bottom - 16 - 34, 150, 34);
            _reloadNoticeStayRect = new Rectangle(_reloadNoticeCloseRect.Left - 10 - 130, _reloadNoticeCloseRect.Top, 130, 34);

            int publishW = Math.Min(680, _panelRect.Width - 120);
            int publishH = 170;
            _publishPromptPanelRect = new Rectangle(_panelRect.Center.X - (publishW / 2), _panelRect.Center.Y - (publishH / 2), publishW, publishH);
            _publishPromptCancelRect = new Rectangle(_publishPromptPanelRect.Right - 16 - 120, _publishPromptPanelRect.Bottom - 16 - 30, 120, 30);
            _publishPromptConfirmRect = new Rectangle(_publishPromptCancelRect.Left - 10 - 140, _publishPromptCancelRect.Top, 140, 30);

            // Build compile popup  (taller to fit diagnostic lines)
            int buildPopupW = Math.Max(460, Math.Min(640, _panelRect.Width - 100));
            int buildPopupH = 270;
            _buildPopupPanelRect = new Rectangle(_panelRect.Center.X - buildPopupW / 2, _panelRect.Center.Y - buildPopupH / 2, buildPopupW, buildPopupH);
            _buildPopupBarRect = new Rectangle(_buildPopupPanelRect.Left + 16, _buildPopupPanelRect.Top + 70, _buildPopupPanelRect.Width - 32, 16);
            _buildPopupOkRect = new Rectangle(_buildPopupPanelRect.Center.X - 55, _buildPopupPanelRect.Bottom - 14 - 28, 110, 28);

            // MSBuild not-found prompt
            int msbuildW = Math.Max(400, Math.Min(580, _panelRect.Width - 120));
            int msbuildH = 158;
            _msbuildPromptPanelRect = new Rectangle(_panelRect.Center.X - msbuildW / 2, _panelRect.Center.Y - msbuildH / 2, msbuildW, msbuildH);
            _msbuildPromptNoRect = new Rectangle(_msbuildPromptPanelRect.Right - 16 - 100, _msbuildPromptPanelRect.Bottom - 14 - 28, 100, 28);
            _msbuildPromptYesRect = new Rectangle(_msbuildPromptNoRect.Left - 10 - 130, _msbuildPromptNoRect.Top, 130, 28);

            // Build mod selector prompt
            int modSelectorW = Math.Max(350, Math.Min(500, _panelRect.Width - 120));
            int modSelectorH = Math.Min(300, Math.Max(150, _buildModSelectorOptions.Length * 26 + 100));
            _buildModSelectorPanelRect = new Rectangle(_panelRect.Center.X - modSelectorW / 2, _panelRect.Center.Y - modSelectorH / 2, modSelectorW, modSelectorH);
            _buildModSelectorConfirmRect = new Rectangle(_buildModSelectorPanelRect.Center.X - 55, _buildModSelectorPanelRect.Bottom - 14 - 28, 110, 28);
            _buildModSelectorCancelRect = new Rectangle(_buildModSelectorConfirmRect.Left - 10 - 100, _buildModSelectorConfirmRect.Top, 100, 28);
        }

        private void DrawButtons(SpriteBatch sb)
        {
            DrawButton(sb, _refreshRect, _loading ? "REFRESHING..." : "REFRESH", new Color(58, 65, 80, 255));
            DrawButton(sb, _installRect, _installing ? "DOWNLOADING..." : "DOWNLOAD", new Color(70, 118, 78, 255));
            DrawButton(sb, _closeRect, "CLOSE", new Color(80, 58, 58, 255));
        }

        private void DrawViewModeButtons(SpriteBatch sb)
        {
            DrawModeButton(sb, _createModViewRect, "CREATE MOD", _viewMode == BrowserViewMode.CreateMod);
            DrawModeButton(sb, _backToBrowserRect, "BROWSER", _viewMode == BrowserViewMode.Browser);
        }

        private void DrawModeButton(SpriteBatch sb, Rectangle rect, string text, bool active)
        {
            Color fill = active ? new Color(66, 99, 148, 255) : new Color(42, 50, 63, 255);
            sb.Draw(_white, rect, fill);
            DrawBorder(sb, rect, new Color(118, 136, 170, 255));
            var size = _smallFont.MeasureString(text);
            var pos = new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
            sb.DrawString(_smallFont, text, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, text, pos, Color.White);
        }

        private void DrawList(SpriteBatch sb)
        {
            DrawModeButton(sb, _sourceAllRect, "ALL", _sourceMode == BrowserSourceMode.All);
            DrawModeButton(sb, _sourceOfficialRect, "OFFICIAL", _sourceMode == BrowserSourceMode.Official);
            DrawModeButton(sb, _sourceCommunityRect, "COMMUNITY", _sourceMode == BrowserSourceMode.Community);

            var headerPos = new Vector2(_leftRect.Left + 10, _sourceAllRect.Bottom + 16);
            sb.DrawString(_smallFont, "Catalog Mods", headerPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Catalog Mods", headerPos, Color.White);

            sb.Draw(_white, _searchRect, _focusSearch ? new Color(34, 45, 62, 255) : new Color(20, 27, 37, 255));
            DrawBorder(sb, _searchRect, new Color(86, 112, 154, 255));
            string searchDisplay = string.IsNullOrWhiteSpace(_searchText) ? "Find..." : _searchText;
            Color searchColor = string.IsNullOrWhiteSpace(_searchText) ? new Color(150, 160, 176, 255) : Color.White;
            var searchPos = new Vector2(_searchRect.Left + 6, _searchRect.Top + 2);
            sb.DrawString(_smallFont, Clip(searchDisplay, _searchRect.Width - 10), searchPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(searchDisplay, _searchRect.Width - 10), searchPos, searchColor);

            int rowH = _smallFont.LineSpacing + 10;
            int y = GetBrowserListTop();
            int visibleRows = GetBrowserVisibleRows(rowH);
            for (int i = 0; i < visibleRows; i++)
            {
                int vis = _scroll + i;
                if (vis >= _visibleIndices.Count) break;
                int idx = _visibleIndices[vis];

                var row = new Rectangle(_leftRect.Left + 6, y, _leftRect.Width - 12, rowH);
                bool sel = idx == _selectedIndex;
                sb.Draw(_white, row, sel ? new Color(70, 98, 145, 255) : new Color(30, 36, 45, 255));

                string name = _mods[idx].Name ?? _mods[idx].Id ?? "Unknown";
                string ver = (_mods[idx].Version ?? "").Trim();
                string line = name;
                if (!string.IsNullOrWhiteSpace(ver))
                    line = name + "  [" + ver + "]";
                line = Clip(line, _leftRect.Width - 28);
                var p = new Vector2(row.Left + 8, row.Top + 4);
                sb.DrawString(_smallFont, line, p + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, line, p, Color.White);
                y += rowH;
            }

            if (_visibleIndices.Count == 0 && !_loading)
            {
                var p = new Vector2(_leftRect.Left + 10, _leftRect.Top + 62);
                string msg = _mods.Count == 0 ? "No mods found in catalog." : "No mods match search.";
                sb.DrawString(_smallFont, msg, p + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, msg, p, Color.LightGray);
            }
        }

        private void DrawDetails(SpriteBatch sb)
        {
            var headerPos = new Vector2(_rightRect.Left + 10, _rightRect.Top + 8);
            sb.DrawString(_smallFont, "Details", headerPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Details", headerPos, Color.White);

            if (_selectedIndex < 0 || _selectedIndex >= _mods.Count)
            {
                var p0 = new Vector2(_rightRect.Left + 10, _rightRect.Top + 38);
                sb.DrawString(_smallFont, "Select a mod from the left list.", p0 + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, "Select a mod from the left list.", p0, Color.LightGray);
                return;
            }

            var m = _mods[_selectedIndex];
            EnsurePreviewRequested(m);
            int y = _rightRect.Top + 38;
            DrawDetailLine(sb, "Name", m.Name, ref y);
            DrawDetailLine(sb, "Author", m.Author, ref y);
            DrawDetailLine(sb, "Version", m.Version, ref y);
            if (!string.IsNullOrWhiteSpace(m.DllUrl))
                DrawDetailLine(sb, "DLL URL", m.DllUrl, ref y);
            if (!string.IsNullOrWhiteSpace(m.PageUrl))
                DrawDetailLine(sb, "Page URL", m.PageUrl, ref y);

            string desc = string.IsNullOrWhiteSpace(m.Description) ? "(No description)" : m.Description;
            var dp = new Vector2(_rightRect.Left + 10, y + 6);
            DrawWrappedText(sb, "Description: " + desc, dp, _rightRect.Width - 20, 2, new Color(210, 214, 220, 255));
            DrawPreview(sb, m, y + (_smallFont.LineSpacing * 2) + 14);
        }

        private void DrawCreateModView(SpriteBatch sb)
        {
            var currentField = GetSelectedCreateField();
            bool publishMode = false;
            Color modFilesColor = _createShowTemplateTab ? new Color(45, 65, 100, 255) : new Color(56, 78, 118, 255);
            Color templateColor = _createShowTemplateTab ? new Color(56, 78, 118, 255) : new Color(45, 65, 100, 255);
            DrawButton(sb, _createFieldsTabRect, "Mod Files", modFilesColor);
            DrawButton(sb, _createTemplateTabRect, "Template", templateColor);

            // Mod selector
            if (_availableModFolders.Length > 0)
            {
                sb.Draw(_white, _createModSelectorRect, new Color(20, 27, 37, 255));
                DrawBorder(sb, _createModSelectorRect, new Color(86, 112, 154, 255));
                string modDisplay = "Mod: " + _availableModFolders[_selectedModFolderIndex];
                if (_availableModFolders.Length > 1)
                    modDisplay += " (" + (_selectedModFolderIndex + 1) + "/" + _availableModFolders.Length + ")";
                var modPos = new Vector2(_createModSelectorRect.Left + 6, _createModSelectorRect.Top + 3);
                sb.DrawString(_smallFont, Clip(modDisplay, _createModSelectorRect.Width - 12), modPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, Clip(modDisplay, _createModSelectorRect.Width - 12), modPos, Color.LightBlue);
                if (_availableModFolders.Length > 1)
                {
                    var hintPos = new Vector2(_createModSelectorRect.Left + 6, _createModSelectorRect.Top + _smallFont.LineSpacing + 1);
                    sb.DrawString(_smallFont, "Ctrl+Left/Right to switch", hintPos, new Color(120, 140, 160, 200));
                }
            }

            sb.Draw(_white, _createFieldFilterRect, _focusCreateFieldFilter ? new Color(34, 45, 62, 255) : new Color(20, 27, 37, 255));
            DrawBorder(sb, _createFieldFilterRect, new Color(86, 112, 154, 255));
            string leftFilter = string.IsNullOrWhiteSpace(_createFieldFilterText) ? "Find file..." : _createFieldFilterText;
            Color leftFilterColor = string.IsNullOrWhiteSpace(_createFieldFilterText) ? new Color(150, 160, 176, 255) : Color.White;
            var leftFilterPos = new Vector2(_createFieldFilterRect.Left + 6, _createFieldFilterRect.Top + 3);
            sb.DrawString(_smallFont, Clip(leftFilter, _createFieldFilterRect.Width - 12), leftFilterPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(leftFilter, _createFieldFilterRect.Width - 12), leftFilterPos, leftFilterColor);

            int rowHeight = Math.Max(34, _smallFont.LineSpacing + 10);
            int listTop = _createFieldFilterRect.Bottom + 6;
            int maxVisibleRows = Math.Max(1, (_leftRect.Bottom - listTop - 10) / rowHeight);
            int drawRows = _createGroupExpanded ? _visibleCreateFieldIndices.Count + 1 : 1;
            int count = Math.Min(maxVisibleRows, drawRows);
            for (int i = 0; i < count; i++)
            {
                bool isGroupRow = !publishMode && i == 0;
                int fieldIndex = -1;
                if (!isGroupRow)
                    fieldIndex = _visibleCreateFieldIndices[i - 1];
                var row = new Rectangle(_leftRect.Left + 1, listTop + (i * rowHeight), _leftRect.Width - 10, rowHeight);
                bool sel = !isGroupRow && fieldIndex == _createSelectedFieldIndex;
                sb.Draw(_white, row, sel ? new Color(70, 98, 145, 255) : new Color(33, 39, 49, 255));
                DrawBorder(sb, row, new Color(42, 50, 63, 255));

                string rowText;
                if (isGroupRow)
                {
                    string projectName = GetCreateTemplateProjectName();
                    rowText = (_createGroupExpanded ? "[-] " : "[+] ") + projectName;
                }
                else
                {
                    var activeFields = GetActiveCreateFields();
                    rowText = activeFields[fieldIndex].Label;
                    string projectName = GetCreateTemplateProjectName();
                    rowText = projectName + "\\" + rowText;
                }

                var textPos = new Vector2(row.Left + 10, row.Top + 6);
                string clipped = Clip(rowText, row.Width - 20);
                sb.DrawString(_smallFont, clipped, textPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, clipped, textPos, sel ? Color.White : new Color(220, 224, 230, 255));
            }

            var fieldsToDisplay = GetActiveCreateFields();
            if (fieldsToDisplay.Count == 0)
            {
                var emptyPos = new Vector2(_leftRect.Left + 10, listTop + 8);
                sb.DrawString(_smallFont, "No mod template yet. Click NEW MOD SETUP.", emptyPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, "No mod template yet. Click NEW MOD SETUP.", emptyPos, new Color(210, 218, 230, 255));
            }

            // Left-pane scrollbar style (visual, like Config).
            var scrollTrack = new Rectangle(_leftRect.Right - 8, listTop, 6, Math.Max(30, (_leftRect.Bottom - listTop - 8)));
            sb.Draw(_white, scrollTrack, new Color(26, 32, 42, 255));
            DrawBorder(sb, scrollTrack, new Color(56, 70, 92, 255));
            int totalRows = Math.Max(1, drawRows);
            int visibleRowsForThumb = Math.Max(1, maxVisibleRows);
            float thumbRatio = MathHelper.Clamp(visibleRowsForThumb / (float)totalRows, 0.12f, 1f);
            int thumbH = Math.Max(16, (int)(scrollTrack.Height * thumbRatio));
            int thumbY = scrollTrack.Top;
            var thumb = new Rectangle(scrollTrack.Left + 1, thumbY, Math.Max(2, scrollTrack.Width - 2), thumbH);
            sb.Draw(_white, thumb, new Color(120, 148, 196, 220));

            int headerHeight = Math.Max(_smallFont.LineSpacing + 12, _createEditorSearchRect.Height + 8);
            var editorHeaderRect = new Rectangle(_rightRect.Left, _rightRect.Top, _rightRect.Width, headerHeight);
            sb.Draw(_white, editorHeaderRect, new Color(34, 42, 54, 255));

            string editorTitle = "Editor";
            if (currentField != null && !string.IsNullOrWhiteSpace(currentField.Label))
                editorTitle = "Editor - " + currentField.Label;
            int titleLeftPad = 64;
            int titleMaxWidth = Math.Max(100, _createEditorSearchRect.Left - (_rightRect.Left + titleLeftPad) - 12);
            string clippedTitle = Clip(editorTitle, titleMaxWidth);
            var editorTitlePos = new Vector2(_rightRect.Left + titleLeftPad, _rightRect.Top + 6);
            sb.DrawString(_smallFont, clippedTitle, editorTitlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, clippedTitle, editorTitlePos, Color.White);

            sb.Draw(_white, _createEditorSearchRect, _focusCreateEditorSearch ? new Color(34, 45, 62, 255) : new Color(20, 27, 37, 255));
            DrawBorder(sb, _createEditorSearchRect, new Color(86, 112, 154, 255));
            string editorSearch = string.IsNullOrWhiteSpace(_createEditorSearchText) ? "Find in field..." : _createEditorSearchText;
            Color editorSearchColor = string.IsNullOrWhiteSpace(_createEditorSearchText) ? new Color(150, 160, 176, 255) : Color.White;
            var editorSearchPos = new Vector2(_createEditorSearchRect.Left + 6, _createEditorSearchRect.Top + 3);
            sb.DrawString(_smallFont, Clip(editorSearch, _createEditorSearchRect.Width - 12), editorSearchPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(editorSearch, _createEditorSearchRect.Width - 12), editorSearchPos, editorSearchColor);

            sb.Draw(_white, _createEditorRect, _focusCreateEditor ? new Color(24, 30, 40, 255) : new Color(20, 27, 37, 255));
            DrawBorder(sb, _createEditorRect, new Color(62, 75, 95, 255));

            int lineNumberWidth = 56;
            int contentTop = editorHeaderRect.Bottom + 8;
            var lineGutter = new Rectangle(_createEditorRect.Left + 8, contentTop, lineNumberWidth, Math.Max(40, _createEditorRect.Bottom - contentTop - 8));
            sb.Draw(_white, lineGutter, new Color(16, 20, 28, 255));

            if (currentField != null)
            {
                string rawText = currentField.Value ?? "";
                bool showingPlaceholder = string.IsNullOrWhiteSpace(rawText) && !_focusCreateEditor;
                string editText = rawText;
                if (showingPlaceholder)
                    editText = currentField.Placeholder ?? "";

                Color editColor = showingPlaceholder ? new Color(150, 160, 176, 255) : Color.White;
                int lineStep = _smallFont.LineSpacing + 2;
                int textLeft = _createEditorRect.Left + 8 + lineNumberWidth + 10;
                int textWidth = _createEditorRect.Width - lineNumberWidth - 28;
                string[] lines = SplitEditorLines(editText);
                int maxVisibleLines = Math.Max(1, (_createEditorRect.Bottom - contentTop - 12) / lineStep);
                int linesToDraw = Math.Min(maxVisibleLines, lines.Length);

                for (int i = 0; i < linesToDraw; i++)
                {
                    int srcLine = _createEditorScroll + i;
                    if (srcLine >= lines.Length) break;
                    int lineY = contentTop + 4 + (i * lineStep);
                    string lineNumber = (srcLine + 1).ToString();
                    var lineNumPos = new Vector2(lineGutter.Left + 10, lineY);
                    sb.DrawString(_smallFont, lineNumber, lineNumPos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(_smallFont, lineNumber, lineNumPos, Color.LightGray);

                    string lineText = (lines[srcLine] ?? "").Replace("\t", "    ");
                    string clippedLineForDisplay = Clip(lineText, textWidth);
                    if (!string.IsNullOrWhiteSpace(_createEditorSearchText))
                    {
                        int matchIdx = clippedLineForDisplay.IndexOf(_createEditorSearchText, StringComparison.OrdinalIgnoreCase);
                        if (matchIdx >= 0)
                        {
                            string prefix = clippedLineForDisplay.Substring(0, matchIdx);
                            string match = clippedLineForDisplay.Substring(matchIdx, _createEditorSearchText.Length);
                            float prefixWidth = _smallFont.MeasureString(prefix).X;
                            float matchWidth = _smallFont.MeasureString(match).X;
                            var hiRect = new Rectangle((int)(textLeft + prefixWidth) - 2, lineY - 2, (int)matchWidth + 4, _smallFont.LineSpacing + 6);
                            sb.Draw(_white, hiRect, new Color(58, 92, 54, 150));
                        }
                    }

                    var textPos = new Vector2(textLeft, lineY);
                    DrawEditorLineWithSyntax(sb, clippedLineForDisplay, textPos, editColor, currentField.Label);
                }

                // Right-side scrollbar for editor
                int editorScrollTrackH = Math.Max(20, _createEditorRect.Height - 16);
                var editorScrollTrack = new Rectangle(_createEditorRect.Right - 7, _createEditorRect.Top + 8, 5, editorScrollTrackH);
                sb.Draw(_white, editorScrollTrack, new Color(22, 28, 38, 255));
                if (lines.Length > maxVisibleLines && maxVisibleLines > 0)
                {
                    float editorThumbRatio = MathHelper.Clamp((float)maxVisibleLines / lines.Length, 0.06f, 1f);
                    int editorThumbH = Math.Max(12, (int)(editorScrollTrackH * editorThumbRatio));
                    int editorThumbY = editorScrollTrack.Top + (int)((editorScrollTrackH - editorThumbH) * ((float)_createEditorScroll / Math.Max(1, lines.Length - maxVisibleLines)));
                    var editorThumb = new Rectangle(editorScrollTrack.Left, editorThumbY, editorScrollTrack.Width, editorThumbH);
                    sb.Draw(_white, editorThumb, new Color(86, 130, 196, 210));
                }

                if (_focusCreateEditor && !showingPlaceholder)
                    DrawCreateEditorCaret(sb, rawText, lineGutter, contentTop, lineNumberWidth);
            }

            DrawCreateInfoPanel(sb, currentField, !publishMode);

            // Slim diagnostics strip between editor and info panel
            sb.Draw(_white, _createDiagnosticsRect, new Color(17, 21, 29, 255));
            DrawBorder(sb, _createDiagnosticsRect, new Color(62, 75, 95, 255));
            int warnCount = _buildLastWarningCount;
            int errCount = _buildLastErrorCount;
            Color warnColor = warnCount > 0 ? new Color(255, 200, 60, 255) : new Color(120, 130, 145, 255);
            Color errColor = errCount > 0 ? new Color(255, 90, 80, 255) : new Color(120, 130, 145, 255);
            string diagText = "Warnings: " + warnCount + "   Errors: " + errCount;
            var diagPos = new Vector2(_createDiagnosticsRect.Left + 10, _createDiagnosticsRect.Top + (_createDiagnosticsRect.Height - _smallFont.LineSpacing) / 2);
            // Draw warning count in yellow and error count in red
            string diagWarnings = "Warnings: ";
            string diagWarnNum = warnCount.ToString();
            string diagSep = "   Errors: ";
            string diagErrNum = errCount.ToString();
            var dpos = diagPos;
            sb.DrawString(_smallFont, diagWarnings, dpos + new Vector2(1,1), Color.Black);
            sb.DrawString(_smallFont, diagWarnings, dpos, new Color(180, 185, 195, 255));
            dpos.X += _smallFont.MeasureString(diagWarnings).X;
            sb.DrawString(_smallFont, diagWarnNum, dpos + new Vector2(1,1), Color.Black);
            sb.DrawString(_smallFont, diagWarnNum, dpos, warnColor);
            dpos.X += _smallFont.MeasureString(diagWarnNum).X;
            sb.DrawString(_smallFont, diagSep, dpos + new Vector2(1,1), Color.Black);
            sb.DrawString(_smallFont, diagSep, dpos, new Color(180, 185, 195, 255));
            dpos.X += _smallFont.MeasureString(diagSep).X;
            sb.DrawString(_smallFont, diagErrNum, dpos + new Vector2(1,1), Color.Black);
            sb.DrawString(_smallFont, diagErrNum, dpos, errColor);
            // Show last build summary on the right side of diag strip
            if (!string.IsNullOrWhiteSpace(_buildLastSummary) && _buildCompleted)
            {
                string sumClipped = Clip(_buildLastSummary, _createDiagnosticsRect.Width - 200);
                var sumSize = _smallFont.MeasureString(sumClipped);
                var sumPos = new Vector2(_createDiagnosticsRect.Right - (int)sumSize.X - 10, diagPos.Y);
                Color sumColor = _buildSucceeded ? new Color(100, 200, 100, 255) : new Color(255, 100, 80, 255);
                sb.DrawString(_smallFont, sumClipped, sumPos + new Vector2(1,1), Color.Black);
                sb.DrawString(_smallFont, sumClipped, sumPos, sumColor);
            }

            DrawButton(sb, _createSaveDraftRect, "SAVE DRAFT", new Color(58, 65, 80, 255));
            DrawButton(sb, _createPublishRect, "PUBLISH", new Color(66, 90, 136, 255));
            DrawButton(sb, _createGenerateRect, "MAKE A MOD", new Color(70, 118, 78, 255));
            DrawButton(sb, _createResetRect, "RESET", new Color(58, 65, 80, 255));
            DrawButton(sb, _createVisualStudioRect, "OPEN VS", new Color(66, 100, 136, 255));
            if (_createShowTemplateTab)
            {
                DrawButton(sb, _createMoveToModsRect, "Move it to !Mods", new Color(70, 100, 70, 255));
            }
            else
            {
                Color buildBtnColor = _buildBusy ? new Color(50, 80, 50, 255) : new Color(50, 140, 80, 255);
                DrawButton(sb, _createBuildRect, _buildBusy ? "BUILDING..." : "BUILD", buildBtnColor);
            }
            DrawButton(sb, _closeRect, "CLOSE", new Color(80, 58, 58, 255));

            if (_showCreateFolderPrompt)
                DrawCreateFolderPrompt(sb);
        }

        private void DrawCreateInfoPanel(SpriteBatch sb, CreateModField currentField, bool createOnlyMode)
        {
            sb.Draw(_white, _createInfoRect, new Color(21, 25, 33, 255));
            DrawBorder(sb, _createInfoRect, new Color(62, 75, 95, 255));

            int x = _createInfoRect.Left + 10;
            int y = _createInfoRect.Top + 8;
            int maxWidth = _createInfoRect.Width - 20;
            string selectedLabel = currentField != null && !string.IsNullOrWhiteSpace(currentField.Label) ? currentField.Label : "No field selected";
            string selectedValue = currentField != null ? (currentField.Value ?? "") : "";
            string placeholder = currentField != null ? (currentField.Placeholder ?? "") : "";
            string displayValue;
            if (currentField != null && currentField.Multiline)
            {
                // Avoid reformatting whole file bodies every frame; this was causing frame drops.
                displayValue = "(multiline code content)";
            }
            else
            {
                displayValue = !string.IsNullOrWhiteSpace(selectedValue) ? selectedValue : placeholder;
                displayValue = CollapseSingleLine(displayValue);
            }

            DrawCreateInfoTextLine(sb, x, ref y, maxWidth, "Field", selectedLabel, Color.White);
            DrawCreateInfoTextLine(sb, x, ref y, maxWidth, "Current", string.IsNullOrWhiteSpace(displayValue) ? "-" : displayValue, new Color(196, 205, 220, 255));

            y += 4;
            DrawCreateInfoSectionTitle(sb, x, ref y, "Created Files");

            if (createOnlyMode)
            {
                string createModName = GetCreateTemplateProjectName();
                string createModRoot = Path.Combine(GetCreatedModsRoot(), createModName);
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[Folder]", createModRoot);
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", createModName + ".csproj");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", createModName + ".cs");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "Patching\\GamePatches.cs");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "Properties\\AssemblyInfo.cs");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "README.md");
                return;
            }

            string modName = SanitizeName(GetFieldValue("mod_name"), "MyNewMod");
            string category = NormalizeCreateCategory(GetFieldValue("content_type"));
            string previewFileName = NormalizePreviewFileName(GetFieldValue("preview_file"));
            string modRoot = Path.Combine(GetCreatedModsRoot(), modName);
            string entryRoot = Path.Combine(modRoot, "CommunityEntry");

            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[Folder]", modRoot);
            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", modName + ".csproj");
            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", modName + ".cs");
            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "README.md");
            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "Patching\\GamePatches.cs");
            DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "Properties\\AssemblyInfo.cs");

            if (!createOnlyMode)
            {
                y += 2;
                DrawCreateInfoSectionTitle(sb, x, ref y, "Community PR Entry");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[Folder]", entryRoot);
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "mod.json");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", "README.md");
                DrawCreateInfoNodeLine(sb, x, ref y, maxWidth, "[File]", previewFileName);
            }
        }

        private void DrawCreateFolderPrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 170));
            sb.Draw(_white, _createPromptPanelRect, new Color(21, 27, 36, 248));
            DrawBorder(sb, _createPromptPanelRect, new Color(100, 124, 170, 255));

            var titlePos = new Vector2(_createPromptPanelRect.Left + 16, _createPromptPanelRect.Top + 14);
            sb.DrawString(_smallFont, "Name the mod folder", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Name the mod folder", titlePos, Color.White);

            sb.Draw(_white, _createPromptInputRect, new Color(18, 24, 34, 255));
            DrawBorder(sb, _createPromptInputRect, new Color(86, 112, 154, 255));
            string display = _createFolderPromptText ?? "";
            var inputPos = new Vector2(_createPromptInputRect.Left + 6, _createPromptInputRect.Top + 3);
            string shown = Clip(display, _createPromptInputRect.Width - 12);
            Color textColor = string.IsNullOrWhiteSpace(display) ? new Color(150, 160, 176, 255) : Color.White;
            if (string.IsNullOrWhiteSpace(display))
                shown = "Type mod folder name...";
            sb.DrawString(_smallFont, shown, inputPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, shown, inputPos, textColor);
            DrawPromptCaret(sb, display, inputPos);

            DrawButton(sb, _createPromptConfirmRect, "CREATE", new Color(70, 118, 78, 255));
            DrawButton(sb, _createPromptCancelRect, "CANCEL", new Color(80, 58, 58, 255));
        }

        private void DrawPromptCaret(SpriteBatch sb, string rawInput, Vector2 inputPos)
        {
            string caretSource = rawInput ?? "";
            int caretX = (int)(inputPos.X + _smallFont.MeasureString(caretSource).X);
            int caretTop = (int)inputPos.Y;
            int caretHeight = _smallFont.LineSpacing + 1;
            sb.Draw(_white, new Rectangle(caretX, caretTop, 2, caretHeight), new Color(210, 228, 255, 245));
        }

        private void OpenPublishPrompt()
        {
            _showPublishPrompt = true;
        }

        private void DrawBuildModSelectorPrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 170));
            sb.Draw(_white, _buildModSelectorPanelRect, new Color(21, 27, 36, 248));
            DrawBorder(sb, _buildModSelectorPanelRect, new Color(100, 124, 170, 255));

            var titlePos = new Vector2(_buildModSelectorPanelRect.Left + 16, _buildModSelectorPanelRect.Top + 14);
            sb.DrawString(_smallFont, "Select Mod to Build", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Select Mod to Build", titlePos, Color.White);

            int listTop = _buildModSelectorPanelRect.Top + 50;
            for (int i = 0; i < _buildModSelectorOptions.Length; i++)
            {
                int itemY = listTop + (i * 26);
                if (itemY >= _buildModSelectorConfirmRect.Top)
                    break;

                Color itemColor = i == _buildModSelectorSelectedIndex ? new Color(70, 130, 200, 255) : new Color(58, 65, 80, 255);
                var itemRect = new Rectangle(_buildModSelectorPanelRect.Left + 8, itemY, _buildModSelectorPanelRect.Width - 16, 24);
                sb.Draw(_white, itemRect, itemColor);

                var textPos = new Vector2(itemRect.Left + 4, itemRect.Top + 2);
                sb.DrawString(_smallFont, _buildModSelectorOptions[i], textPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, _buildModSelectorOptions[i], textPos, Color.White);
            }

            DrawButton(sb, _buildModSelectorConfirmRect, "BUILD", new Color(70, 118, 78, 255));
            DrawButton(sb, _buildModSelectorCancelRect, "CANCEL", new Color(80, 58, 58, 255));
        }

        private void DrawCreateModWarningPrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 170));

            int panelW = 550;
            int panelH = 200;
            _createModWarningPanelRect = new Rectangle(_panelRect.Center.X - panelW / 2, _panelRect.Center.Y - panelH / 2, panelW, panelH);

            sb.Draw(_white, _createModWarningPanelRect, new Color(21, 27, 36, 248));
            DrawBorder(sb, _createModWarningPanelRect, new Color(255, 150, 0, 255));

            var titlePos = new Vector2(_createModWarningPanelRect.Left + 16, _createModWarningPanelRect.Top + 14);
            sb.DrawString(_smallFont, "⚠️ NOTICE", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "⚠️ NOTICE", titlePos, new Color(255, 200, 0, 255));

            var textPos = new Vector2(_createModWarningPanelRect.Left + 16, _createModWarningPanelRect.Top + 50);
            string msg = "Create Mod is still being tested.";
            string msg2 = "Don't expect it to work well.";
            sb.DrawString(_smallFont, msg, textPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, msg, textPos, new Color(210, 218, 230, 255));
            sb.DrawString(_smallFont, msg2, textPos + new Vector2(0, _smallFont.LineSpacing + 4) + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, msg2, textPos + new Vector2(0, _smallFont.LineSpacing + 4), new Color(210, 218, 230, 255));

            int buttonY = _createModWarningPanelRect.Bottom - 40;
            _createModWarningOkButtonRect = new Rectangle(_createModWarningPanelRect.Left + 40, buttonY, 100, 28);
            _createModWarningBackButtonRect = new Rectangle(_createModWarningPanelRect.Right - 140, buttonY, 100, 28);

            DrawButton(sb, _createModWarningOkButtonRect, "OK", new Color(70, 118, 78, 255));
            DrawButton(sb, _createModWarningBackButtonRect, "BACK", new Color(80, 58, 58, 255));
        }

        private void DrawPublishPrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 170));
            sb.Draw(_white, _publishPromptPanelRect, new Color(21, 27, 36, 248));
            DrawBorder(sb, _publishPromptPanelRect, new Color(100, 124, 170, 255));

            var titlePos = new Vector2(_publishPromptPanelRect.Left + 16, _publishPromptPanelRect.Top + 14);
            var textPos = new Vector2(_publishPromptPanelRect.Left + 16, _publishPromptPanelRect.Top + 50);
            sb.DrawString(_smallFont, "Publish Community Entry", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Publish Community Entry", titlePos, Color.White);

            string line1 = "This creates CommunityEntry files (mod.json, README, preview placeholder)";
            string line2 = "under !Mods\\ModBrowser\\Created Mod\\<ModName>\\CommunityEntry.";
            sb.DrawString(_smallFont, line1, textPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line1, textPos, new Color(210, 218, 230, 255));
            sb.DrawString(_smallFont, line2, textPos + new Vector2(0, _smallFont.LineSpacing + 4) + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line2, textPos + new Vector2(0, _smallFont.LineSpacing + 4), new Color(210, 218, 230, 255));

            DrawButton(sb, _publishPromptConfirmRect, "PUBLISH", new Color(66, 90, 136, 255));
            DrawButton(sb, _publishPromptCancelRect, "CANCEL", new Color(80, 58, 58, 255));
        }

        private void RequestCloseOrShowReloadPrompt()
        {
            if (_downloadedAnyThisSession)
            {
                _showReloadNoticePrompt = true;
                return;
            }
            PopMe();
        }

        private void DrawReloadNoticePrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 170));
            sb.Draw(_white, _reloadNoticePanelRect, new Color(21, 27, 36, 248));
            DrawBorder(sb, _reloadNoticePanelRect, new Color(100, 124, 170, 255));

            var titlePos = new Vector2(_reloadNoticePanelRect.Left + 16, _reloadNoticePanelRect.Top + 14);
            var textPos = new Vector2(_reloadNoticePanelRect.Left + 16, _reloadNoticePanelRect.Top + 50);
            sb.DrawString(_smallFont, "Reload Needed", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Reload Needed", titlePos, Color.White);

            string line1 = "You downloaded mod files this session.";
            string line2 = "Unload/reload mods (or restart game) so new files are detected.";
            sb.DrawString(_smallFont, line1, textPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line1, textPos, new Color(210, 218, 230, 255));
            sb.DrawString(_smallFont, line2, textPos + new Vector2(0, _smallFont.LineSpacing + 4) + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line2, textPos + new Vector2(0, _smallFont.LineSpacing + 4), new Color(210, 218, 230, 255));

            DrawButton(sb, _reloadNoticeStayRect, "STAY", new Color(58, 65, 80, 255));
            DrawButton(sb, _reloadNoticeCloseRect, "CLOSE NOW", new Color(80, 58, 58, 255));
        }

        private CreateModField GetSelectedCreateField()
        {
            var activeFields = GetActiveCreateFields();
            if (_createSelectedFieldIndex < 0 || _createSelectedFieldIndex >= activeFields.Count)
                return null;
            return activeFields[_createSelectedFieldIndex];
        }

        private string GetFieldValue(string key)
        {
            for (int i = 0; i < _createFields.Count; i++)
            {
                if (string.Equals(_createFields[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return (_createFields[i].Value ?? "").Trim();
            }
            return "";
        }

        private void SetFieldValue(string key, string value)
        {
            for (int i = 0; i < _createFields.Count; i++)
            {
                if (string.Equals(_createFields[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _createFields[i].Value = value ?? "";
                    return;
                }
            }
        }

        private List<CreateModField> GetActiveCreateFields()
        {
            if (_viewMode == BrowserViewMode.CreateMod)
                return _createShowTemplateTab ? GetTemplateFields() : _createCodeFields;
            return _createFields;
        }

        private List<CreateModField> GetTemplateFields()
        {
            var templateFields = new List<CreateModField>();
            if (string.IsNullOrWhiteSpace(_currentModRootPath) || !Directory.Exists(_currentModRootPath))
                return templateFields;

            var dllFiles = new List<string>();

            // Check bin\Release\
            string binPath = Path.Combine(_currentModRootPath, "bin", "Release");
            if (Directory.Exists(binPath))
            {
                var binDlls = Directory.GetFiles(binPath, "*.dll");
                dllFiles.AddRange(binDlls.Select(f => "bin\\Release\\" + Path.GetFileName(f)));
            }

            // Check template\ folder
            string templatePath = Path.Combine(_currentModRootPath, "template");
            if (Directory.Exists(templatePath))
            {
                var templateDlls = Directory.GetFiles(templatePath, "*.dll");
                dllFiles.AddRange(templateDlls.Select(f => "template\\" + Path.GetFileName(f)));
            }

            // Add each DLL as a field
            for (int i = 0; i < dllFiles.Count; i++)
            {
                string dllPath = Path.Combine(_currentModRootPath, dllFiles[i]);
                templateFields.Add(new CreateModField
                {
                    Key = "template_dll_" + i,
                    Label = dllFiles[i],
                    Value = "[Compiled DLL - read-only]",
                    Placeholder = "",
                    Multiline = false
                });
            }

            if (templateFields.Count == 0)
                templateFields.Add(new CreateModField
                {
                    Key = "template_empty",
                    Label = "(No compiled DLLs yet)",
                    Value = "Build the mod to generate DLLs.",
                    Placeholder = "",
                    Multiline = false
                });

            return templateFields;
        }

        private void ResetCreateCodeTemplates()
        {
            _createCodeFields.Clear();
        }

        private string GetCreateCodeFieldValue(string key)
        {
            for (int i = 0; i < _createCodeFields.Count; i++)
            {
                if (string.Equals(_createCodeFields[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return _createCodeFields[i].Value ?? "";
            }
            return "";
        }

        private void SetCreateCodeFieldValue(string key, string value)
        {
            for (int i = 0; i < _createCodeFields.Count; i++)
            {
                if (string.Equals(_createCodeFields[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _createCodeFields[i].Value = value ?? "";
                    return;
                }
            }
        }

        private string GetCreateTemplateProjectName()
        {
            string csproj = GetCreateCodeFieldValue("file_csproj");
            string assemblyName = ExtractXmlTagValue(csproj, "AssemblyName");
            if (!string.IsNullOrWhiteSpace(assemblyName))
                return SanitizeName(assemblyName, "MyNewMod");

            string rootNamespace = ExtractXmlTagValue(csproj, "RootNamespace");
            if (!string.IsNullOrWhiteSpace(rootNamespace))
                return SanitizeName(rootNamespace, "MyNewMod");

            return "MyNewMod";
        }

        private static string ExtractXmlTagValue(string text, string tagName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(tagName))
                return "";

            string openTag = "<" + tagName + ">";
            string closeTag = "</" + tagName + ">";
            int start = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "";
            start += openTag.Length;
            int end = text.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
            if (end <= start)
                return "";
            return text.Substring(start, end - start).Trim();
        }

        private static string ExtractNamespaceFromMain(string mainText, string fallback)
        {
            if (string.IsNullOrWhiteSpace(mainText))
                return SanitizeIdentifier(fallback, "MyNewMod");

            string[] lines = mainText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("namespace ", StringComparison.Ordinal))
                {
                    string ns = line.Substring("namespace ".Length).Trim().Trim('{', ' ');
                    if (!string.IsNullOrWhiteSpace(ns))
                        return SanitizeIdentifier(ns, fallback);
                }
            }

            return SanitizeIdentifier(fallback, "MyNewMod");
        }

        private static string ExtractVersionFromAssemblyInfo(string asmText)
        {
            if (string.IsNullOrWhiteSpace(asmText))
                return "";

            const string marker = "AssemblyVersion(\"";
            int start = asmText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "";
            start += marker.Length;
            int end = asmText.IndexOf('"', start);
            if (end <= start)
                return "";
            return asmText.Substring(start, end - start).Trim();
        }

        private void ResetCreateModFields()
        {
            _createFields.Clear();
            _createFields.Add(new CreateModField { Key = "content_type", Label = "Content Type", Value = "mod", Placeholder = "mod | texture-pack | weapon-addon" });
            _createFields.Add(new CreateModField { Key = "mod_name", Label = "Mod Name", Value = "MyNewMod", Placeholder = "MyNewMod" });
            _createFields.Add(new CreateModField { Key = "slug", Label = "Slug", Value = "my-new-mod", Placeholder = "my-new-mod" });
            _createFields.Add(new CreateModField { Key = "namespace", Label = "Namespace", Value = "MyNewMod", Placeholder = "MyNewMod" });
            _createFields.Add(new CreateModField { Key = "author", Label = "Author", Value = "unknowghost", Placeholder = "unknowghost" });
            _createFields.Add(new CreateModField { Key = "maintainers", Label = "Maintainers", Value = "unknowghost", Placeholder = "unknowghost" });
            _createFields.Add(new CreateModField { Key = "version", Label = "Version", Value = "1.0.0", Placeholder = "1.0.0" });
            _createFields.Add(new CreateModField { Key = "summary", Label = "Summary", Value = "One-paragraph summary of what this entry does.", Placeholder = "Short summary for the community browser" });
            _createFields.Add(new CreateModField { Key = "description", Label = "Description", Value = "New CastleForge mod", Placeholder = "What your mod does...", Multiline = true });
            _createFields.Add(new CreateModField { Key = "game_version", Label = "Game Version", Value = "v1.9.9.8.5 Steam", Placeholder = "v1.9.9.8.5 Steam" });
            _createFields.Add(new CreateModField { Key = "castleforge_version", Label = "CastleForge Version", Value = "core-v0.1.0+", Placeholder = "core-v0.1.0+" });
            _createFields.Add(new CreateModField { Key = "license", Label = "License", Value = "MIT", Placeholder = "MIT" });
            _createFields.Add(new CreateModField { Key = "repo_url", Label = "GitHub Repo URL", Value = "", Placeholder = "https://github.com/you/repo" });
            _createFields.Add(new CreateModField { Key = "releases_url", Label = "Releases URL", Value = "", Placeholder = "https://github.com/you/repo/releases" });
            _createFields.Add(new CreateModField { Key = "tags", Label = "Tags", Value = "community, mod", Placeholder = "community, mod, qol" });
            _createFields.Add(new CreateModField { Key = "preview_file", Label = "Preview File", Value = "preview.png", Placeholder = "preview.png or preview.gif" });
            ResetCreateCodeTemplates();
            _createSelectedFieldIndex = 0;
            _createCaretIndex = (_createFields.Count > 0 ? (_createFields[0].Value ?? "").Length : 0);
            _focusCreateEditor = false;
            _focusCreateFieldFilter = false;
            _focusCreateEditorSearch = false;
            _createFieldFilterText = "";
            _createEditorSearchText = "";
            _showPublishPrompt = false;
            RebuildVisibleCreateFieldIndices();
        }

        private void RebuildVisibleCreateFieldIndices()
        {
            var activeFields = GetActiveCreateFields();
            _visibleCreateFieldIndices.Clear();
            string query = (_createFieldFilterText ?? "").Trim().ToLowerInvariant();
            bool showAll = string.IsNullOrWhiteSpace(query);

            for (int i = 0; i < activeFields.Count; i++)
            {
                if (showAll)
                {
                    _visibleCreateFieldIndices.Add(i);
                    continue;
                }

                string hay = ((activeFields[i].Label ?? "") + " " + (activeFields[i].Value ?? "") + " " + (activeFields[i].Placeholder ?? "")).ToLowerInvariant();
                if (hay.Contains(query))
                    _visibleCreateFieldIndices.Add(i);
            }

            if (_visibleCreateFieldIndices.Count == 0)
            {
                _createSelectedFieldIndex = -1;
                _createCaretIndex = 0;
                return;
            }

            if (_visibleCreateFieldIndices.Count > 0 && !_visibleCreateFieldIndices.Contains(_createSelectedFieldIndex))
                SetSelectedCreateField(_visibleCreateFieldIndices[0]);
        }

        private void OpenCreateFolderPrompt()
        {
            _createFolderPromptText = "";
            _showCreateFolderPrompt = true;
        }

        private void ConfirmCreateFolderPrompt()
        {
            string modName = SanitizeName(_createFolderPromptText, "");
            if (string.IsNullOrWhiteSpace(modName))
                modName = GenerateNextModName();
            BuildCreateCodeTemplatesFor(modName);
            _showCreateFolderPrompt = false;
            CreateLocalModOnlyFiles();
        }

        private string GenerateNextModName()
        {
            string root = GetCreatedModsRoot();
            if (!Directory.Exists(root))
                return "MyNewMod_1";
            var dirs = Directory.GetDirectories(root);
            int maxNum = 0;
            for (int i = 0; i < dirs.Length; i++)
            {
                string folderName = new DirectoryInfo(dirs[i]).Name;
                if (folderName.StartsWith("MyNewMod_", StringComparison.OrdinalIgnoreCase))
                {
                    string numPart = folderName.Substring("MyNewMod_".Length);
                    if (int.TryParse(numPart, out int num) && num > maxNum)
                        maxNum = num;
                }
            }
            return "MyNewMod_" + (maxNum + 1);
        }

        private void EnsureCreateTemplatesLoadedIfAny()
        {
            if (_createCodeFields.Count > 0)
                return;

            try
            {
                string root = GetCreatedModsRoot();
                if (!Directory.Exists(root))
                    return;

                var dirs = Directory.GetDirectories(root);
                if (dirs == null || dirs.Length == 0)
                    return;

                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                _availableModFolders = dirs.Select(d => new DirectoryInfo(d).Name).ToArray();
                _selectedModFolderIndex = 0;
                LoadModAtIndex(0);
            }
            catch
            {
            }
        }

        private void LoadModAtIndex(int index)
        {
            if (index < 0 || index >= _availableModFolders.Length)
                return;

            try
            {
                string modName = _availableModFolders[index];
                string modRoot = Path.Combine(GetCreatedModsRoot(), modName);
                _currentModRootPath = modRoot;
                _selectedModFolderIndex = index;

                string csprojPath = Path.Combine(modRoot, modName + ".csproj");
                if (!File.Exists(csprojPath))
                {
                    var anyCsproj = Directory.GetFiles(modRoot, "*.csproj", SearchOption.TopDirectoryOnly);
                    if (anyCsproj.Length > 0)
                        csprojPath = anyCsproj[0];
                }

                string mainPath = Path.Combine(modRoot, modName + ".cs");
                if (!File.Exists(mainPath))
                {
                    var anyCs = Directory.GetFiles(modRoot, "*.cs", SearchOption.TopDirectoryOnly);
                    if (anyCs.Length > 0)
                        mainPath = anyCs[0];
                }

                string patchPath = Path.Combine(modRoot, "Patching", "GamePatches.cs");
                string asmPath = Path.Combine(modRoot, "Properties", "AssemblyInfo.cs");
                string readmePath = Path.Combine(modRoot, "README.md");

                _createCodeFields.Clear();
                _createCodeFields.Add(new CreateModField { Key = "file_main", Label = Path.GetFileName(mainPath), Value = SafeReadText(mainPath), Placeholder = "", Multiline = true });
                _createCodeFields.Add(new CreateModField { Key = "file_patches", Label = "Patching\\GamePatches.cs", Value = SafeReadText(patchPath), Placeholder = "", Multiline = true });
                _createCodeFields.Add(new CreateModField { Key = "file_assemblyinfo", Label = "Properties\\AssemblyInfo.cs", Value = SafeReadText(asmPath), Placeholder = "", Multiline = true });
                _createCodeFields.Add(new CreateModField { Key = "file_csproj", Label = Path.GetFileName(csprojPath), Value = SafeReadText(csprojPath), Placeholder = "", Multiline = true });
                _createCodeFields.Add(new CreateModField { Key = "file_readme", Label = "README.md", Value = SafeReadText(readmePath), Placeholder = "", Multiline = true });

                _createSelectedFieldIndex = 0;
                _createCaretIndex = 0;
                RebuildVisibleCreateFieldIndices();
            }
            catch
            {
            }
        }

        private void BuildCreateCodeTemplatesFor(string modNameRaw)
        {
            string modName = SanitizeName(modNameRaw, "MyNewMod");
            string ns = SanitizeIdentifier(modName, "MyNewMod");
            string version = "1.0.0";
            string author = "unknowghost";
            string description = "New CastleForge mod";
            string guid = Guid.NewGuid().ToString().ToUpperInvariant();

            _createCodeFields.Clear();
            _createCodeFields.Add(new CreateModField { Key = "file_main", Label = modName + ".cs", Value = BuildStarterMainClass(modName, ns, version), Placeholder = "", Multiline = true });
            _createCodeFields.Add(new CreateModField { Key = "file_patches", Label = "Patching\\GamePatches.cs", Value = BuildStarterPatches(ns), Placeholder = "", Multiline = true });
            _createCodeFields.Add(new CreateModField { Key = "file_assemblyinfo", Label = "Properties\\AssemblyInfo.cs", Value = BuildStarterAssemblyInfo(modName, version), Placeholder = "", Multiline = true });
            _createCodeFields.Add(new CreateModField { Key = "file_csproj", Label = modName + ".csproj", Value = BuildStarterCsproj(modName, guid), Placeholder = "", Multiline = true });
            _createCodeFields.Add(new CreateModField { Key = "file_readme", Label = "README.md", Value = BuildStarterReadme(modName, author, description, version), Placeholder = "", Multiline = true });

            _createSelectedFieldIndex = 0;
            _createCaretIndex = 0;
            RebuildVisibleCreateFieldIndices();
        }

        private string BuildCreatePreviewText()
        {
            string modName = SanitizeName(GetFieldValue("mod_name"), "MyNewMod");
            string ns = SanitizeIdentifier(GetFieldValue("namespace"), modName);
            string category = NormalizeCreateCategory(GetFieldValue("content_type"));
            string categoryLabel = GetCommunityCategoryLabel(category);
            string modFolder = Path.Combine(GetCreatedModsRoot(), modName);
            string entryFolder = Path.Combine(modFolder, "CommunityEntry");
            string author = GetFieldValue("author");
            string version = GetFieldValue("version");
            string description = GetFieldValue("description");
            string repoUrl = GetFieldValue("repo_url");
            var sb = new StringBuilder();
            sb.Append("Mod Name: ").Append(modName);
            sb.Append("\nCategory: ").Append(categoryLabel);
            sb.Append("\nNamespace: ").Append(ns);
            if (!string.IsNullOrWhiteSpace(author))
                sb.Append("\nAuthor: ").Append(author);
            if (!string.IsNullOrWhiteSpace(version))
                sb.Append("\nVersion: ").Append(version);
            if (!string.IsNullOrWhiteSpace(description))
                sb.Append("\nDescription: ").Append(description);
            sb.Append("\n\nFolder:");
            sb.Append("\n").Append(modFolder);
            sb.Append("\n\nFiles:");
            sb.Append("\n- ").Append(modName).Append(".csproj");
            sb.Append("\n- ").Append(modName).Append(".cs");
            sb.Append("\n- Patching\\GamePatches.cs");
            sb.Append("\n- Properties\\AssemblyInfo.cs");
            sb.Append("\n- README.md");
            sb.Append("\n\nCommunity entry:");
            sb.Append("\n").Append(entryFolder);
            sb.Append("\n- mod.json");
            sb.Append("\n- README.md");
            sb.Append("\n- ").Append(NormalizePreviewFileName(GetFieldValue("preview_file")));
            if (!string.IsNullOrWhiteSpace(repoUrl))
                sb.Append("\n\nRepo URL: ").Append(repoUrl);
            return sb.ToString();
        }

        private void SetSelectedCreateField(int index)
        {
            var activeFields = GetActiveCreateFields();
            if (index < 0 || index >= activeFields.Count)
                return;

            _createSelectedFieldIndex = index;
            SyncCreateCaretToSelection();
        }

        private void MoveCreateSelection(int delta)
        {
            if (_visibleCreateFieldIndices.Count == 0)
                return;

            int visibleIndex = _visibleCreateFieldIndices.IndexOf(_createSelectedFieldIndex);
            if (visibleIndex < 0)
                visibleIndex = 0;

            visibleIndex += delta;
            if (visibleIndex < 0)
                visibleIndex = _visibleCreateFieldIndices.Count - 1;
            if (visibleIndex >= _visibleCreateFieldIndices.Count)
                visibleIndex = 0;

            SetSelectedCreateField(_visibleCreateFieldIndices[visibleIndex]);
        }

        private void SyncCreateCaretToSelection()
        {
            _createCaretIndex = 0;
            _createEditorScroll = 0;
        }

        private void InsertIntoSelectedCreateField(string text)
        {
            var field = GetSelectedCreateField();
            if (field == null || string.IsNullOrEmpty(text))
                return;

            string value = field.Value ?? "";
            int insertIndex = _createCaretIndex;
            if (insertIndex < 0)
                insertIndex = 0;
            if (insertIndex > value.Length)
                insertIndex = value.Length;

            field.Value = value.Substring(0, insertIndex) + text + value.Substring(insertIndex);
            _createCaretIndex = insertIndex + text.Length;
        }

        private void BackspaceSelectedCreateField()
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;

            string value = field.Value ?? "";
            if (string.IsNullOrEmpty(value) || _createCaretIndex <= 0)
                return;

            if (_createCaretIndex > value.Length)
                _createCaretIndex = value.Length;

            field.Value = value.Remove(_createCaretIndex - 1, 1);
            _createCaretIndex--;
        }

        private static bool IsShiftHeld()
        {
            var state = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            return state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) ||
                   state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
        }

        private static bool IsCtrlHeld()
        {
            var state = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            return state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
                   state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
        }

        private static string CollapseSingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string collapsed = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            while (collapsed.Contains("  "))
                collapsed = collapsed.Replace("  ", " ");
            return collapsed.Trim();
        }

        private static string[] SplitEditorLines(string text)
        {
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            if (lines.Length == 0)
                return new[] { "" };
            return lines;
        }

        private static void GetCaretLineAndColumn(string text, int caretIndex, out int lineIndex, out int columnIndex)
        {
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            if (caretIndex < 0)
                caretIndex = 0;
            if (caretIndex > normalized.Length)
                caretIndex = normalized.Length;

            lineIndex = 0;
            columnIndex = 0;
            for (int i = 0; i < caretIndex; i++)
            {
                if (normalized[i] == '\n')
                {
                    lineIndex++;
                    columnIndex = 0;
                }
                else
                {
                    columnIndex++;
                }
            }
        }

        private static int GetCaretIndexFromLineAndColumn(string text, int targetLine, int targetColumn)
        {
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            if (targetLine < 0)
                targetLine = 0;
            if (targetColumn < 0)
                targetColumn = 0;

            int index = 0;
            int line = 0;
            while (index < normalized.Length && line < targetLine)
            {
                if (normalized[index] == '\n')
                    line++;
                index++;
            }

            int col = 0;
            while (index < normalized.Length && normalized[index] != '\n' && col < targetColumn)
            {
                index++;
                col++;
            }

            return index;
        }

        private void SetCreateCaretFromEditorClick(Point point)
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;

            string text = field.Value ?? "";
            int lineNumberWidth = 56;
            int lineStep = _smallFont.LineSpacing + 2;
            int headerHeight = Math.Max(_smallFont.LineSpacing + 12, _createEditorSearchRect.Height + 8);
            int contentTop = _rightRect.Top + headerHeight + 8 + 4;
            int textLeft = _createEditorRect.Left + 8 + lineNumberWidth + 10;
            int textRight = _createEditorRect.Right - 10;
            int maxVisibleLines = Math.Max(1, (_createEditorRect.Bottom - (contentTop - 4) - 12) / lineStep);

            int line = (point.Y - contentTop) / lineStep;
            if (line < 0) line = 0;
            if (line >= maxVisibleLines) line = maxVisibleLines - 1;

            string[] lines = SplitEditorLines(text);
            if (line >= lines.Length)
                line = Math.Max(0, lines.Length - 1);

            int clickX = point.X;
            if (clickX < textLeft) clickX = textLeft;
            if (clickX > textRight) clickX = textRight;

            string currentLine = line < lines.Length ? (lines[line] ?? "") : "";
            int column = 0;
            if (point.X >= textRight - 2)
                column = currentLine.Length;
            else
            {
                while (column < currentLine.Length)
                {
                    string prefix = currentLine.Substring(0, column + 1);
                    int px = textLeft + (int)_smallFont.MeasureString(prefix).X;
                    if (px >= clickX)
                        break;
                    column++;
                }
            }
            if (column > currentLine.Length)
                column = currentLine.Length;

            _createCaretIndex = GetCaretIndexFromLineAndColumn(text, line, column);
        }

        private void MoveCreateCaretHorizontal(int delta)
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;

            string text = field.Value ?? "";
            _createCaretIndex += delta;
            if (_createCaretIndex < 0) _createCaretIndex = 0;
            if (_createCaretIndex > text.Length) _createCaretIndex = text.Length;
        }

        private void MoveCreateCaretHome()
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;
            int line;
            int col;
            GetCaretLineAndColumn(field.Value ?? "", _createCaretIndex, out line, out col);
            _createCaretIndex = GetCaretIndexFromLineAndColumn(field.Value ?? "", line, 0);
        }

        private void MoveCreateCaretEnd()
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;
            string text = field.Value ?? "";
            int line;
            int col;
            GetCaretLineAndColumn(text, _createCaretIndex, out line, out col);
            string[] lines = SplitEditorLines(text);
            int endCol = (line >= 0 && line < lines.Length) ? lines[line].Length : 0;
            _createCaretIndex = GetCaretIndexFromLineAndColumn(text, line, endCol);
        }

        private void MoveCreateCaretVertical(int deltaLine)
        {
            var field = GetSelectedCreateField();
            if (field == null)
                return;

            string text = field.Value ?? "";
            int line;
            int col;
            GetCaretLineAndColumn(text, _createCaretIndex, out line, out col);
            int targetLine = line + deltaLine;
            if (targetLine < 0) targetLine = 0;
            string[] lines = SplitEditorLines(text);
            if (targetLine >= lines.Length) targetLine = lines.Length - 1;
            int targetCol = col;
            if (targetLine >= 0 && targetLine < lines.Length && targetCol > lines[targetLine].Length)
                targetCol = lines[targetLine].Length;
            _createCaretIndex = GetCaretIndexFromLineAndColumn(text, targetLine, targetCol);
        }

        private void DrawCreateEditorCaret(SpriteBatch sb, string rawText, Rectangle lineGutter, int contentTop, int lineNumberWidth)
        {
            if (((Environment.TickCount / 500) & 1) != 0)
                return;

            int lineStep = _smallFont.LineSpacing + 2;
            int textLeft = _createEditorRect.Left + 8 + lineNumberWidth + 10;
            int textWidth = _createEditorRect.Width - lineNumberWidth - 28;
            int textRight = textLeft + textWidth;
            int maxVisibleLines = Math.Max(1, (_createEditorRect.Bottom - contentTop - 12) / lineStep);

            int lineIndex;
            int columnIndex;
            GetCaretLineAndColumn(rawText, _createCaretIndex, out lineIndex, out columnIndex);

            // Check if caret is visible on screen
            int visibleLineIndex = lineIndex - _createEditorScroll;
            if (visibleLineIndex < 0 || visibleLineIndex >= maxVisibleLines)
                return; // Caret is not visible

            string[] lines = SplitEditorLines(rawText);
            string currentLine = lineIndex < lines.Length ? (lines[lineIndex] ?? "").Replace("\t", "    ") : "";
            if (columnIndex < 0)
                columnIndex = 0;
            if (columnIndex > currentLine.Length)
                columnIndex = currentLine.Length;

            string prefix = columnIndex > 0 ? currentLine.Substring(0, columnIndex) : "";
            int lineY = contentTop + 4 + (visibleLineIndex * lineStep);
            int caretX = textLeft + (int)_smallFont.MeasureString(prefix).X;
            if (caretX > textRight)
                caretX = textRight;

            var caretRect = new Rectangle(caretX, lineY - 1, 2, _smallFont.LineSpacing + 4);
            sb.Draw(_white, caretRect, new Color(136, 196, 255, 255));
        }

        private void DrawEditorLineWithSyntax(SpriteBatch sb, string line, Vector2 pos, Color defaultColor, string label)
        {
            string lowerLabel = (label ?? "").ToLowerInvariant();
            if (lowerLabel.EndsWith(".cs", StringComparison.Ordinal))
            {
                DrawCSharpSyntaxLine(sb, line, pos, defaultColor);
                return;
            }

            if (lowerLabel.EndsWith(".csproj", StringComparison.Ordinal) || lowerLabel.EndsWith(".xml", StringComparison.Ordinal))
            {
                DrawXmlSyntaxLine(sb, line, pos, defaultColor);
                return;
            }

            DrawTextToken(sb, line, ref pos, defaultColor);
        }

        private void DrawCSharpSyntaxLine(SpriteBatch sb, string line, Vector2 pos, Color defaultColor)
        {
            int i = 0;
            while (i < line.Length)
            {
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    DrawTextToken(sb, line.Substring(i), ref pos, new Color(115, 172, 114, 255));
                    return;
                }

                char c = line[i];
                if (c == '"' || c == '\'')
                {
                    int start = i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length)
                        {
                            i += 2;
                            continue;
                        }
                        if (line[i] == c)
                        {
                            i++;
                            break;
                        }
                        i++;
                    }
                    DrawTextToken(sb, line.Substring(start, i - start), ref pos, new Color(230, 176, 110, 255));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i++;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;
                    string token = line.Substring(start, i - start);
                    Color color = CSharpKeywords.Contains(token) ? new Color(104, 171, 232, 255) : defaultColor;
                    DrawTextToken(sb, token, ref pos, color);
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int start = i++;
                    while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
                        i++;
                    DrawTextToken(sb, line.Substring(start, i - start), ref pos, new Color(209, 206, 104, 255));
                    continue;
                }

                DrawTextToken(sb, c.ToString(), ref pos, defaultColor);
                i++;
            }
        }

        private void DrawXmlSyntaxLine(SpriteBatch sb, string line, Vector2 pos, Color defaultColor)
        {
            int i = 0;
            while (i < line.Length)
            {
                int open = line.IndexOf('<', i);
                if (open < 0)
                {
                    DrawTextToken(sb, line.Substring(i), ref pos, defaultColor);
                    return;
                }

                if (open > i)
                    DrawTextToken(sb, line.Substring(i, open - i), ref pos, defaultColor);

                int close = line.IndexOf('>', open);
                if (close < 0)
                {
                    DrawTextToken(sb, line.Substring(open), ref pos, new Color(104, 171, 232, 255));
                    return;
                }

                DrawTextToken(sb, line.Substring(open, close - open + 1), ref pos, new Color(104, 171, 232, 255));
                i = close + 1;
            }
        }

        private void DrawTextToken(SpriteBatch sb, string token, ref Vector2 pos, Color color)
        {
            if (string.IsNullOrEmpty(token))
                return;

            sb.DrawString(_smallFont, token, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, token, pos, color);
            pos.X += _smallFont.MeasureString(token).X;
        }

        private void DrawCreateInfoTextLine(SpriteBatch sb, int x, ref int y, int maxWidth, string label, string value, Color valueColor)
        {
            string text = label + ": " + value;
            string clipped = Clip(text, maxWidth);
            var pos = new Vector2(x, y);
            sb.DrawString(_smallFont, clipped, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, clipped, pos, valueColor);
            y += _smallFont.LineSpacing + 2;
        }

        private void DrawCreateInfoSectionTitle(SpriteBatch sb, int x, ref int y, string title)
        {
            var pos = new Vector2(x, y);
            sb.DrawString(_smallFont, title, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, title, pos, new Color(255, 214, 92, 255));
            y += _smallFont.LineSpacing + 2;
        }

        private void DrawCreateInfoNodeLine(SpriteBatch sb, int x, ref int y, int maxWidth, string prefix, string value)
        {
            int bottomLimit = _createInfoRect.Bottom - (_smallFont.LineSpacing + 8);
            if (y > bottomLimit)
                return;

            string text = prefix + " " + value;
            string clipped = Clip(text, maxWidth);
            var pos = new Vector2(x, y);
            sb.DrawString(_smallFont, clipped, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, clipped, pos, prefix == "[Folder]" ? new Color(142, 198, 255, 255) : new Color(224, 228, 234, 255));
            y += _smallFont.LineSpacing + 1;
        }

        private string GetModsSourceRoot()
        {
            string current = Path.GetDirectoryName(typeof(ModBrowserScreen).Assembly.Location) ?? ".";
            string dir = current;
            for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(dir); i++)
            {
                if (string.Equals(Path.GetFileName(dir), "CastleForge-main", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(dir, "CastleForge", "Mods");
                dir = Path.GetDirectoryName(dir);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "cmz modded", "CastleForge-main", "CastleForge", "Mods");
        }

        private void SaveCreateModDraft()
        {
            try
            {
                string draftDir = Path.Combine(Path.GetDirectoryName(typeof(ModBrowserScreen).Assembly.Location) ?? ".", "ModBrowser", "!CreateDrafts");
                Directory.CreateDirectory(draftDir);
                string draftPath = Path.Combine(draftDir, _viewMode == BrowserViewMode.CreateMod ? "CreateModCodeDraft.ini" : "CreateModDraft.ini");
                var activeFields = GetActiveCreateFields();

                var lines = new List<string>();
                for (int i = 0; i < activeFields.Count; i++)
                {
                    lines.Add(activeFields[i].Key + "=" + (activeFields[i].Value ?? ""));
                }

                File.WriteAllLines(draftPath, lines.ToArray(), Encoding.UTF8);
                _status = "Draft saved: " + draftPath;
                _statusColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                _status = "Draft save failed: " + ex.Message;
                _statusColor = Color.OrangeRed;
            }
        }

        private void CreateModStarterFiles()
        {
            try
            {
                string modName = SanitizeName(GetFieldValue("mod_name"), "MyNewMod");
                string ns = SanitizeIdentifier(GetFieldValue("namespace"), modName);
                string category = NormalizeCreateCategory(GetFieldValue("content_type"));
                string categoryFolder = GetCommunityCategoryFolder(category);
                string author = GetFieldValue("author");
                if (string.IsNullOrWhiteSpace(author))
                    author = "unknowghost";
                string maintainers = GetFieldValue("maintainers");
                if (string.IsNullOrWhiteSpace(maintainers))
                    maintainers = author;
                string version = GetFieldValue("version");
                if (string.IsNullOrWhiteSpace(version))
                    version = "1.0.0";
                string slugSource = GetFieldValue("slug");
                string slug = Slugify(string.IsNullOrWhiteSpace(slugSource) ? modName : slugSource);
                SetFieldValue("slug", slug);
                string summary = GetFieldValue("summary");
                string description = GetFieldValue("description");
                string repoUrl = GetFieldValue("repo_url");
                string releasesUrl = GetFieldValue("releases_url");
                string gameVersion = GetFieldValue("game_version");
                string castleForgeVersion = GetFieldValue("castleforge_version");
                string license = GetFieldValue("license");
                string tags = GetFieldValue("tags");
                string previewFileName = NormalizePreviewFileName(GetFieldValue("preview_file"));

                string modsRoot = GetCreatedModsRoot();
                string modRoot = Path.Combine(modsRoot, modName);
                string patchingDir = Path.Combine(modRoot, "Patching");
                string propsDir = Path.Combine(modRoot, "Properties");
                Directory.CreateDirectory(modRoot);
                Directory.CreateDirectory(patchingDir);
                Directory.CreateDirectory(propsDir);

                string guid = Guid.NewGuid().ToString().ToUpperInvariant();
                WriteFileUtf8(Path.Combine(modRoot, modName + ".csproj"), BuildStarterCsproj(modName, guid));
                WriteFileUtf8(Path.Combine(modRoot, modName + ".cs"), BuildStarterMainClass(modName, ns, version));
                WriteFileUtf8(Path.Combine(patchingDir, "GamePatches.cs"), BuildStarterPatches(ns));
                WriteFileUtf8(Path.Combine(propsDir, "AssemblyInfo.cs"), BuildStarterAssemblyInfo(modName, version));
                WriteFileUtf8(Path.Combine(modRoot, "README.md"), BuildStarterReadme(modName, author, description, version));
                SyncManagedCreateProjectFile(modRoot, modName, guid);

                string entryRoot = Path.Combine(modRoot, "CommunityEntry");
                Directory.CreateDirectory(entryRoot);
                WriteFileUtf8(
                    Path.Combine(entryRoot, "mod.json"),
                    BuildCommunityEntryJson(modName, slug, category, author, maintainers, summary, gameVersion, castleForgeVersion, license, repoUrl, releasesUrl, tags, previewFileName));
                WriteFileUtf8(
                    Path.Combine(entryRoot, "README.md"),
                    BuildCommunityEntryReadme(modName, category, author, maintainers, version, summary, description, gameVersion, castleForgeVersion, license, repoUrl, releasesUrl, previewFileName));
                WriteFileUtf8(
                    Path.Combine(entryRoot, "OPEN-PR.txt"),
                    BuildCommunityEntryGuide(modName, categoryFolder));
                WritePlaceholderPreview(Path.Combine(entryRoot, previewFileName));

                _status = "Created files in !Mods\\ModBrowser\\Created Mod\\" + modName;
                _statusColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                _status = "Create files failed: " + ex.Message;
                _statusColor = Color.OrangeRed;
            }
        }

        private void CreateLocalModOnlyFiles()
        {
            try
            {
                if (_createCodeFields.Count == 0)
                {
                    _status = "No template yet. Click NEW MOD SETUP first.";
                    _statusColor = Color.Orange;
                    return;
                }

                string csprojText = GetCreateCodeFieldValue("file_csproj");
                string mainText = GetCreateCodeFieldValue("file_main");
                string patchesText = GetCreateCodeFieldValue("file_patches");
                string asmText = GetCreateCodeFieldValue("file_assemblyinfo");
                string readmeText = GetCreateCodeFieldValue("file_readme");

                string modName = SanitizeName(GetCreateTemplateProjectName(), "MyNewMod");
                string ns = ExtractNamespaceFromMain(mainText, modName);
                string version = ExtractVersionFromAssemblyInfo(asmText);
                if (string.IsNullOrWhiteSpace(version))
                    version = "1.0.0";

                string modRoot = Path.Combine(GetCreatedModsRoot(), modName);
                string patchingDir = Path.Combine(modRoot, "Patching");
                string propsDir = Path.Combine(modRoot, "Properties");
                string csprojPath = Path.Combine(modRoot, modName + ".csproj");
                Directory.CreateDirectory(modRoot);
                Directory.CreateDirectory(patchingDir);
                Directory.CreateDirectory(propsDir);

                string guid = NormalizeProjectGuid(ExtractXmlTagValue(csprojText, "ProjectGuid"), Guid.NewGuid().ToString().ToUpperInvariant());
                if (string.IsNullOrWhiteSpace(csprojText))
                    csprojText = BuildStarterCsproj(modName, guid);
                if (string.IsNullOrWhiteSpace(mainText))
                    mainText = BuildStarterMainClass(modName, ns, version);
                if (string.IsNullOrWhiteSpace(patchesText))
                    patchesText = BuildStarterPatches(ns);
                if (string.IsNullOrWhiteSpace(asmText))
                    asmText = BuildStarterAssemblyInfo(modName, version);
                if (string.IsNullOrWhiteSpace(readmeText))
                    readmeText = BuildStarterReadme(modName, "unknowghost", "New CastleForge mod", version);

                WriteFileUtf8(csprojPath, csprojText);
                WriteFileUtf8(Path.Combine(modRoot, modName + ".cs"), mainText);
                WriteFileUtf8(Path.Combine(patchingDir, "GamePatches.cs"), patchesText);
                WriteFileUtf8(Path.Combine(propsDir, "AssemblyInfo.cs"), asmText);
                WriteFileUtf8(Path.Combine(modRoot, "README.md"), readmeText);
                SyncManagedCreateProjectFile(modRoot, modName, guid);
                SetCreateCodeFieldValue("file_csproj", SafeReadText(csprojPath));

                _status = "Created local mod files in !Mods\\ModBrowser\\Created Mod\\" + modName;
                _statusColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                _status = "Create mod failed: " + ex.Message;
                _statusColor = Color.OrangeRed;
            }
        }

        private static void WriteFileUtf8(string path, string content)
        {
            File.WriteAllText(path, content ?? "", new UTF8Encoding(false));
        }

        private static string SafeReadText(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return "";
                return File.ReadAllText(path);
            }
            catch
            {
                return "";
            }
        }

        private static string SanitizeName(string value, string fallback)
        {
            string raw = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
                raw = fallback;
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                    sb.Append(ch);
            }
            if (sb.Length == 0)
                sb.Append(fallback);
            return sb.ToString();
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string raw = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
                raw = fallback;
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    sb.Append(ch);
            }
            if (sb.Length == 0 || !char.IsLetter(sb[0]))
                sb.Insert(0, "M");
            return sb.ToString();
        }

        private static string Slugify(string value)
        {
            string raw = (value ?? "").Trim().ToLowerInvariant();
            var sb = new StringBuilder();
            bool lastDash = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                    lastDash = false;
                }
                else if (!lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
            string outText = sb.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(outText) ? "new-mod" : outText;
        }

        private static string NormalizeCreateCategory(string value)
        {
            string raw = (value ?? "").Trim().ToLowerInvariant();
            if (raw.Contains("texture"))
                return "texture-pack";
            if (raw.Contains("weapon"))
                return "weapon-addon";
            return "mod";
        }

        private static string GetCommunityCategoryFolder(string category)
        {
            switch (NormalizeCreateCategory(category))
            {
                case "texture-pack":
                    return "TexturePacks";
                case "weapon-addon":
                    return "WeaponAddons";
                default:
                    return "Mods";
            }
        }

        private static string GetCommunityCategoryLabel(string category)
        {
            switch (NormalizeCreateCategory(category))
            {
                case "texture-pack":
                    return "Texture Pack";
                case "weapon-addon":
                    return "Weapon Addon";
                default:
                    return "Mod";
            }
        }

        private static string GetCreatedModsRoot()
        {
            // GetModsInstallRoot() returns the !Mods directory (parent of ModBrowser folder)
            // So we append "Created Mod" to get the actual created mods directory
            return Path.Combine(GetModsInstallRoot(), "Created Mod");
        }

        // ModBrowser.dll lives at {gameRoot}\!Mods\ModBrowser.dll in the Steam install.
        private static string GetModsInstallRoot()
        {
            return Path.GetDirectoryName(typeof(ModBrowserScreen).Assembly.Location) ?? ".";
        }

        private static string GetGameInstallRoot()
        {
            string mods = GetModsInstallRoot();
            return Path.GetDirectoryName(mods) ?? mods;
        }

        private static string ResolveReferencePath(string fileName)
        {
            string mods = GetModsInstallRoot();
            string game = GetGameInstallRoot();
            string[] candidates =
            {
                Path.Combine(game, fileName),
                Path.Combine(mods, fileName),
                Path.Combine(mods, "Patching", fileName),
                Path.Combine(game, "Patching", fileName)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return candidates[i];
            }
            return Path.Combine(game, fileName);
        }

        private static string NormalizePreviewFileName(string value)
        {
            string raw = Path.GetFileName((value ?? "").Trim());
            if (string.IsNullOrWhiteSpace(raw))
                raw = "preview.png";

            string ext = Path.GetExtension(raw);
            if (string.IsNullOrWhiteSpace(ext))
                raw += ".png";

            return raw;
        }

        private static string NormalizeProjectGuid(string raw, string fallback)
        {
            string candidate = (raw ?? "").Trim().Trim('{', '}');
            Guid parsed;
            if (Guid.TryParse(candidate, out parsed))
                return parsed.ToString().ToUpperInvariant();
            return fallback;
        }

        private static void SyncManagedCreateProjectFile(string modRoot, string modName, string fallbackGuidNoBraces)
        {
            string csprojPath = Path.Combine(modRoot, modName + ".csproj");
            string existingText = SafeReadText(csprojPath);
            string syncedText = BuildManagedCreateProjectText(modRoot, modName, existingText, fallbackGuidNoBraces);
            WriteFileUtf8(csprojPath, syncedText);
        }

        private static string BuildManagedCreateProjectText(string modRoot, string modName, string existingCsprojText, string fallbackGuidNoBraces)
        {
            string guid = NormalizeProjectGuid(ExtractXmlTagValue(existingCsprojText, "ProjectGuid"), fallbackGuidNoBraces);
            XDocument doc;

            try
            {
                string source = string.IsNullOrWhiteSpace(existingCsprojText) ? BuildStarterCsproj(modName, guid) : existingCsprojText;
                doc = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                doc = XDocument.Parse(BuildStarterCsproj(modName, guid), LoadOptions.PreserveWhitespace);
            }

            XElement project = doc.Root;
            if (project == null)
                return BuildStarterCsproj(modName, guid);

            XNamespace ns = project.Name.Namespace;
            if (ns == XNamespace.None)
                return BuildStarterCsproj(modName, guid);

            doc.Declaration = new XDeclaration("1.0", "utf-8", null);

            EnsureProjectProperty(project, ns, "ProjectGuid", "{" + guid + "}");
            EnsureProjectProperty(project, ns, "AssemblyName", modName);
            if (string.IsNullOrWhiteSpace(GetProjectProperty(project, ns, "RootNamespace")))
                EnsureProjectProperty(project, ns, "RootNamespace", modName);
            if (string.IsNullOrWhiteSpace(GetProjectProperty(project, ns, "TargetFrameworkVersion")))
                EnsureProjectProperty(project, ns, "TargetFrameworkVersion", "v4.8.1");
            if (string.IsNullOrWhiteSpace(GetProjectProperty(project, ns, "PlatformTarget")))
                EnsureProjectProperty(project, ns, "PlatformTarget", "x86");

            RemoveAutoManagedProjectItems(project, ns);

            var compileItems = new List<string>();
            var embeddedItems = new List<string>();
            var contentItems = new List<string>();
            var localReferenceFiles = new List<string>();

            if (Directory.Exists(modRoot))
            {
                string[] allFiles = Directory.GetFiles(modRoot, "*", SearchOption.AllDirectories);
                for (int i = 0; i < allFiles.Length; i++)
                {
                    string relativePath = GetRelativeProjectPath(modRoot, allFiles[i]);
                    if (string.IsNullOrWhiteSpace(relativePath) || IsIgnoredManagedProjectPath(relativePath))
                        continue;

                    string extension = Path.GetExtension(relativePath) ?? "";
                    if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".user", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".suo", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".cache", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        compileItems.Add(relativePath);
                        continue;
                    }

                    if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) && PathHasAnySegment(relativePath, AutoManagedReferenceFolders))
                    {
                        localReferenceFiles.Add(relativePath);
                        continue;
                    }

                    if (ShouldEmbedManagedProjectFile(relativePath))
                    {
                        embeddedItems.Add(relativePath);
                        continue;
                    }

                    contentItems.Add(relativePath);
                }
            }

            compileItems.Sort(StringComparer.OrdinalIgnoreCase);
            embeddedItems.Sort(StringComparer.OrdinalIgnoreCase);
            contentItems.Sort(StringComparer.OrdinalIgnoreCase);
            localReferenceFiles.Sort(StringComparer.OrdinalIgnoreCase);

            string sourceScanText = ReadCreateProjectSourceScan(modRoot);
            bool needsModLoaderExtensions =
                UsesModLoaderExtensions(sourceScanText) ||
                existingCsprojText.IndexOf("ModLoaderExtensions.csproj", StringComparison.OrdinalIgnoreCase) >= 0;

            var groupsToInsert = new List<XElement>();

            var referenceGroup = new XElement(ns + "ItemGroup", new XAttribute("Label", "AutoReferences"));
            List<ManagedReferenceSpec> referenceSpecs = BuildAutoReferenceSpecs(sourceScanText, localReferenceFiles);
            for (int i = 0; i < referenceSpecs.Count; i++)
            {
                ManagedReferenceSpec spec = referenceSpecs[i];
                var referenceElement = new XElement(ns + "Reference", new XAttribute("Include", spec.Include));
                if (!string.IsNullOrWhiteSpace(spec.HintPath))
                    referenceElement.Add(new XElement(ns + "HintPath", spec.HintPath));
                if (spec.WriteSpecificVersionFalse)
                    referenceElement.Add(new XElement(ns + "SpecificVersion", "False"));
                if (spec.WritePrivate)
                    referenceElement.Add(new XElement(ns + "Private", spec.PrivateValue ? "True" : "False"));
                referenceGroup.Add(referenceElement);
            }
            groupsToInsert.Add(referenceGroup);

            var compileGroup = new XElement(ns + "ItemGroup", new XAttribute("Label", "AutoCompile"));
            for (int i = 0; i < compileItems.Count; i++)
                compileGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", compileItems[i])));
            groupsToInsert.Add(compileGroup);

            if (embeddedItems.Count > 0)
            {
                var embeddedGroup = new XElement(ns + "ItemGroup", new XAttribute("Label", "AutoEmbeddedResources"));
                for (int i = 0; i < embeddedItems.Count; i++)
                    embeddedGroup.Add(new XElement(ns + "EmbeddedResource", new XAttribute("Include", embeddedItems[i])));
                groupsToInsert.Add(embeddedGroup);
            }

            if (contentItems.Count > 0)
            {
                var contentGroup = new XElement(ns + "ItemGroup", new XAttribute("Label", "AutoContentFiles"));
                for (int i = 0; i < contentItems.Count; i++)
                {
                    var contentElement = new XElement(ns + "None", new XAttribute("Include", contentItems[i]));
                    contentElement.Add(new XElement(ns + "CopyToOutputDirectory", "PreserveNewest"));
                    contentGroup.Add(contentElement);
                }
                groupsToInsert.Add(contentGroup);
            }

            XElement csharpTargetsImport = project.Elements(ns + "Import")
                .FirstOrDefault(x =>
                    string.Equals((string)x.Attribute("Project"), @"$(MSBuildToolsPath)\Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase));

            if (csharpTargetsImport != null)
                csharpTargetsImport.AddBeforeSelf(groupsToInsert.ToArray());
            else
                project.Add(groupsToInsert.ToArray());

            var builder = new StringBuilder();
            if (doc.Declaration != null)
                builder.Append(doc.Declaration).Append(Environment.NewLine);
            builder.Append(doc.ToString());
            return builder.ToString();
        }

        private static string GetProjectProperty(XElement project, XNamespace ns, string propertyName)
        {
            XElement propertyGroup = project.Elements(ns + "PropertyGroup").FirstOrDefault(x => x.Attribute("Condition") == null);
            if (propertyGroup == null)
                return "";

            XElement property = propertyGroup.Element(ns + propertyName);
            return property == null ? "" : property.Value;
        }

        private static void EnsureProjectProperty(XElement project, XNamespace ns, string propertyName, string value)
        {
            XElement propertyGroup = project.Elements(ns + "PropertyGroup").FirstOrDefault(x => x.Attribute("Condition") == null);
            if (propertyGroup == null)
            {
                propertyGroup = new XElement(ns + "PropertyGroup");
                XElement firstConditionalGroup = project.Elements(ns + "PropertyGroup").FirstOrDefault();
                if (firstConditionalGroup != null)
                    firstConditionalGroup.AddBeforeSelf(propertyGroup);
                else
                    project.AddFirst(propertyGroup);
            }

            XElement property = propertyGroup.Element(ns + propertyName);
            if (property == null)
            {
                property = new XElement(ns + propertyName);
                propertyGroup.Add(property);
            }

            property.Value = value ?? "";
        }

        private static void RemoveAutoManagedProjectItems(XElement project, XNamespace ns)
        {
            List<XElement> itemGroups = project.Elements(ns + "ItemGroup").ToList();
            for (int i = 0; i < itemGroups.Count; i++)
            {
                XElement itemGroup = itemGroups[i];

                itemGroup.Elements(ns + "Compile").Remove();
                itemGroup.Elements(ns + "EmbeddedResource").Remove();
                itemGroup.Elements(ns + "Content").Remove();
                itemGroup.Elements(ns + "None").Remove();

                foreach (XElement projectReference in itemGroup.Elements(ns + "ProjectReference").ToList())
                {
                    string include = ((string)projectReference.Attribute("Include")) ?? "";
                    if (include.IndexOf("ModLoader.csproj", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        include.IndexOf("ModLoaderExtensions.csproj", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        projectReference.Remove();
                    }
                }

                foreach (XElement reference in itemGroup.Elements(ns + "Reference").ToList())
                {
                    if (IsAutoManagedReference(reference, ns))
                        reference.Remove();
                }

                if (!itemGroup.Elements().Any())
                    itemGroup.Remove();
            }
        }

        private static bool IsAutoManagedReference(XElement reference, XNamespace ns)
        {
            string include = (((string)reference.Attribute("Include")) ?? "").Trim();
            string shortName = include;
            int commaIndex = shortName.IndexOf(',');
            if (commaIndex >= 0)
                shortName = shortName.Substring(0, commaIndex).Trim();

            if (string.Equals(shortName, "0Harmony", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "CastleMinerZ", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "DNA.Common", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "DNA.Steam", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "ModLoader", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "ModLoaderExtensions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.Xna.Framework.Game", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.Xna.Framework.Graphics", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.Xna.Framework.Xact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Core", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Xml.Linq", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Data.DataSetExtensions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.CSharp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Data", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Net.Http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "System.Xml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string hintPath = reference.Element(ns + "HintPath") == null ? "" : reference.Element(ns + "HintPath").Value;
            if (hintPath.IndexOf("$(ReferenceAssembliesRoot)", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (hintPath.Length > 0 && (hintPath.IndexOf("CastleMinerZ.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hintPath.IndexOf(@"\!Mods\", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            return PathHasAnySegment(hintPath, AutoManagedReferenceFolders);
        }

        private static List<ManagedReferenceSpec> BuildAutoReferenceSpecs(string sourceScanText, List<string> localReferenceFiles)
        {
            var specs = new List<ManagedReferenceSpec>();

            AddManagedReference(specs, "CastleMinerZ",                    ResolveReferencePath("CastleMinerZ.exe"),                 false, true, false);
            AddManagedReference(specs, "DNA.Common",                      ResolveReferencePath("DNA.Common.dll"),                   false, true, false);
            AddManagedReference(specs, "Microsoft.Xna.Framework",         ResolveReferencePath("Microsoft.Xna.Framework.dll"),         false, true, false);
            AddManagedReference(specs, "Microsoft.Xna.Framework.Game",    ResolveReferencePath("Microsoft.Xna.Framework.Game.dll"),    false, true, false);
            AddManagedReference(specs, "Microsoft.Xna.Framework.Graphics",ResolveReferencePath("Microsoft.Xna.Framework.Graphics.dll"),false, true, false);
            AddManagedReference(specs, "Microsoft.Xna.Framework.Xact",    ResolveReferencePath("Microsoft.Xna.Framework.Xact.dll"),    false, true, false);
            AddManagedReference(specs, "ModLoader",                       ResolveReferencePath("ModLoader.dll"),                    false, true, false);
            AddManagedReference(specs, "ModLoaderExtensions",             ResolveReferencePath("ModLoaderExtensions.dll"),          false, true, false);
            AddManagedReference(specs, "System",     null, false, false, false);
            AddManagedReference(specs, "System.Core",null, false, false, false);

            if (ContainsAnyText(sourceScanText, "System.Windows.Forms"))
                AddManagedReference(specs, "System.Windows.Forms", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "System.Xml.Linq"))
                AddManagedReference(specs, "System.Xml.Linq", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "System.Data.DataSetExtensions"))
                AddManagedReference(specs, "System.Data.DataSetExtensions", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "Microsoft.CSharp"))
                AddManagedReference(specs, "Microsoft.CSharp", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "System.Data"))
                AddManagedReference(specs, "System.Data", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "System.Net.Http"))
                AddManagedReference(specs, "System.Net.Http", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "System.Xml"))
                AddManagedReference(specs, "System.Xml", null, false, false, false);
            if (ContainsAnyText(sourceScanText, "DNA.Steam"))
                AddManagedReference(specs, "DNA.Steam", ResolveReferencePath("DNA.Steam.dll"), false, true, false);

            // 0Harmony is embedded in ModLoader.dll in the Steam install. Only add it
            // if we actually found a standalone copy (dev repo).
            string harmony = ResolveReferencePath("0Harmony.dll");
            if (File.Exists(harmony))
                AddManagedReference(specs, "0Harmony", harmony, false, true, false);

            for (int i = 0; i < localReferenceFiles.Count; i++)
            {
                string relativePath = localReferenceFiles[i];
                string include = Path.GetFileNameWithoutExtension(relativePath);
                AddManagedReference(specs, include, relativePath, false, true, true);
            }

            return specs;
        }

        private static void AddManagedReference(List<ManagedReferenceSpec> specs, string include, string hintPath, bool writeSpecificVersionFalse, bool writePrivate, bool privateValue)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                if (string.Equals(specs[i].Include, include, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            specs.Add(new ManagedReferenceSpec
            {
                Include = include,
                HintPath = hintPath,
                WriteSpecificVersionFalse = writeSpecificVersionFalse,
                WritePrivate = writePrivate,
                PrivateValue = privateValue
            });
        }

        private static string ReadCreateProjectSourceScan(string modRoot)
        {
            if (!Directory.Exists(modRoot))
                return "";

            var builder = new StringBuilder();
            string[] sourceFiles = Directory.GetFiles(modRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string relativePath = GetRelativeProjectPath(modRoot, sourceFiles[i]);
                if (IsIgnoredManagedProjectPath(relativePath))
                    continue;

                builder.AppendLine(SafeReadText(sourceFiles[i]));
            }

            return builder.ToString();
        }

        private static bool UsesModLoaderExtensions(string sourceScanText)
        {
            return ContainsAnyText(
                sourceScanText,
                "ModLoaderExtensions",
                "RequiredDependencies(\"ModLoaderExtensions\"",
                "RequiredDependencies(\"ModLoaderExtensions\",",
                "using ModLoaderExtensions",
                "CommandDispatcher",
                "HelpRegistry",
                "ChatSystem",
                "MLEConfig",
                "ExceptionTap");
        }

        private static bool ContainsAnyText(string haystack, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(haystack))
                return false;

            for (int i = 0; i < needles.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(needles[i]) &&
                    haystack.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldEmbedManagedProjectFile(string relativePath)
        {
            string extension = Path.GetExtension(relativePath) ?? "";
            if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
                return true;

            return PathHasAnySegment(relativePath, AutoManagedAssetFolders);
        }

        private static bool IsIgnoredManagedProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return true;

            string[] parts = relativePath.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (AutoManagedIgnoredFolders.Contains(parts[i]))
                    return true;
            }

            return false;
        }

        private static bool PathHasAnySegment(string pathValue, HashSet<string> segmentSet)
        {
            if (string.IsNullOrWhiteSpace(pathValue))
                return false;

            string[] parts = pathValue.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (segmentSet.Contains(parts[i]))
                    return true;
            }

            return false;
        }

        private static string GetRelativeProjectPath(string root, string fullPath)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(fullPath);

            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length).Replace(Path.AltDirectorySeparatorChar, '\\').Replace(Path.DirectorySeparatorChar, '\\');

            return Path.GetFileName(fullPath);
        }

        private static string BuildStarterCsproj(string modName, string guidNoBraces)
        {
            string castleMinerZ  = ResolveReferencePath("CastleMinerZ.exe");
            string dnaCommon     = ResolveReferencePath("DNA.Common.dll");
            string xnaFramework  = ResolveReferencePath("Microsoft.Xna.Framework.dll");
            string xnaGame       = ResolveReferencePath("Microsoft.Xna.Framework.Game.dll");
            string xnaGraphics   = ResolveReferencePath("Microsoft.Xna.Framework.Graphics.dll");
            string xnaXact       = ResolveReferencePath("Microsoft.Xna.Framework.Xact.dll");
            string modLoader     = ResolveReferencePath("ModLoader.dll");
            string modLoaderExt  = ResolveReferencePath("ModLoaderExtensions.dll");

            return
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <ProjectGuid>{" + guidNoBraces + @"}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>" + modName + @"</RootNamespace>
    <AssemblyName>" + modName + @"</AssemblyName>
    <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <Deterministic>true</Deterministic>
    <TargetFrameworkProfile />
    <PlatformTarget>x86</PlatformTarget>
    <OutputPath>bin\$(Configuration)\</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)' == 'Debug|x86'"">
    <DebugSymbols>true</DebugSymbols>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <DebugType>full</DebugType>
    <PlatformTarget>x86</PlatformTarget>
    <LangVersion>7.3</LangVersion>
    <ErrorReport>prompt</ErrorReport>
  </PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)' == 'Release|x86'"">
    <DefineConstants>TRACE</DefineConstants>
    <Optimize>true</Optimize>
    <DebugType>none</DebugType>
    <PlatformTarget>x86</PlatformTarget>
    <LangVersion>7.3</LangVersion>
    <ErrorReport>prompt</ErrorReport>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""CastleMinerZ"">
      <HintPath>" + castleMinerZ + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""DNA.Common"">
      <HintPath>" + dnaCommon + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""Microsoft.Xna.Framework"">
      <HintPath>" + xnaFramework + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""Microsoft.Xna.Framework.Game"">
      <HintPath>" + xnaGame + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""Microsoft.Xna.Framework.Graphics"">
      <HintPath>" + xnaGraphics + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""Microsoft.Xna.Framework.Xact"">
      <HintPath>" + xnaXact + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""ModLoader"">
      <HintPath>" + modLoader + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""ModLoaderExtensions"">
      <HintPath>" + modLoaderExt + @"</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""" + modName + @".cs"" />
    <Compile Include=""Patching\GamePatches.cs"" />
    <Compile Include=""Properties\AssemblyInfo.cs"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>
";
        }

        private static string BuildStarterMainClass(string modName, string ns, string version)
        {
            return
@"using System;
using DNA.CastleMinerZ;
using DNA.Input;
using Microsoft.Xna.Framework;
using ModLoader;
using ModLoaderExt;
using static ModLoader.LogSystem;

namespace " + ns + @"
{
    [Priority(Priority.Normal)]
    public sealed class " + modName + @" : ModBase
    {
        private readonly CommandDispatcher _dispatcher;

        public " + modName + @"() : base(""" + modName + @""", new Version(""" + version + @"""))
        {
            _dispatcher = new CommandDispatcher(this);

            var game = CastleMinerZGame.Instance;
            if (game != null)
                game.Exiting += (s, e) => Shutdown();
        }

        public override void Start()
        {
            GamePatches.ApplyAllPatches();

            // Hook into the shared chat interceptor so slash-commands
            // typed in game chat are routed to this mod's dispatcher.
            ChatInterceptor.Install();
            ChatInterceptor.RegisterHandler(raw => _dispatcher.TryInvoke(raw));

            Log(""" + modName + @" loaded."");
        }

        public static void Shutdown()
        {
            GamePatches.DisableAll();
            Log(""" + modName + @" shutdown complete."");
        }

        public override void Tick(InputManager inputManager, GameTime gameTime)
        {
        }

        // ── Commands ────────────────────────────────────────────────────────

        // Type /hi in game chat. Only you see the reply.
        [Command(""/hi"")]
        private void OnHi()
        {
            SendFeedback(""Hi there! "" + Name + "" says hello."");
        }
    }
}
";
        }

        private static string BuildStarterPatches(string ns)
        {
            return
@"namespace " + ns + @"
{
    internal static class GamePatches
    {
        internal static void ApplyAllPatches()
        {
        }

        internal static void DisableAll()
        {
        }
    }
}
";
        }

        private static string BuildStarterAssemblyInfo(string modName, string version)
        {
            return
@"using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(""" + modName + @""")]
[assembly: AssemblyDescription("""")]
[assembly: AssemblyConfiguration("""")]
[assembly: AssemblyCompany("""")]
[assembly: AssemblyProduct(""" + modName + @""")]
[assembly: AssemblyCopyright(""Copyright © 2026"")]
[assembly: AssemblyTrademark("""")]
[assembly: AssemblyCulture("""")]
[assembly: ComVisible(false)]
[assembly: Guid(""" + Guid.NewGuid().ToString() + @""")]
[assembly: AssemblyVersion(""" + version + @".0"")]
[assembly: AssemblyFileVersion(""" + version + @".0"")]
";
        }

        private static string BuildStarterReadme(string modName, string author, string description, string version)
        {
            return
"# " + modName + "\n\n" +
"Author: " + author + "\n\n" +
"Version: " + version + "\n\n" +
(string.IsNullOrWhiteSpace(description) ? "Description: New CastleForge mod.\n" : ("Description: " + description + "\n")) +
"\n## Build\n\n" +
"Build with MSBuild/Visual Studio and copy the DLL to `!Mods`.\n";
        }

        private static string BuildCommunityEntryJson(string modName, string slug, string category, string author, string maintainersRaw, string summary, string gameVersion, string castleForgeVersion, string license, string repoUrl, string releasesUrl, string tagsRaw, string previewFileName)
        {
            string normalizedCategory = NormalizeCreateCategory(category);
            string categoryFolder = GetCommunityCategoryFolder(normalizedCategory);
            string safeRepo = string.IsNullOrWhiteSpace(repoUrl) ? "https://github.com/you/repo" : repoUrl.Trim();
            string safeReleases = string.IsNullOrWhiteSpace(releasesUrl) ? safeRepo.TrimEnd('/') + "/releases" : releasesUrl.Trim();
            string safeSummary = string.IsNullOrWhiteSpace(summary) ? "One-paragraph summary of what this entry does." : CollapseSingleLine(summary.Trim());
            string safeGameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "v1.9.9.8.5 Steam" : gameVersion.Trim();
            string safeCastleForgeVersion = string.IsNullOrWhiteSpace(castleForgeVersion) ? "core-v0.1.0+" : castleForgeVersion.Trim();
            string safeLicense = string.IsNullOrWhiteSpace(license) ? "MIT" : license.Trim();
            string[] maintainers = ParseList(maintainersRaw);
            if (maintainers.Length == 0)
                maintainers = new[] { string.IsNullOrWhiteSpace(author) ? "unknowghost" : author.Trim() };
            string[] tags = ParseList(tagsRaw);
            if (tags.Length == 0)
                tags = GetDefaultTags(normalizedCategory);

            return
"{\n" +
"  \"name\": \"" + EscapeJson(modName) + "\",\n" +
"  \"slug\": \"" + EscapeJson(slug) + "\",\n" +
"  \"category\": \"" + EscapeJson(normalizedCategory) + "\",\n" +
"  \"author\": \"" + EscapeJson(author) + "\",\n" +
"  \"maintainers\": " + BuildJsonArray(maintainers) + ",\n" +
"  \"summary\": \"" + EscapeJson(safeSummary) + "\",\n" +
"  \"game_version\": \"" + EscapeJson(safeGameVersion) + "\",\n" +
"  \"castleforge_version\": \"" + EscapeJson(safeCastleForgeVersion) + "\",\n" +
"  \"license\": \"" + EscapeJson(safeLicense) + "\",\n" +
"  \"source_repo\": \"" + EscapeJson(safeRepo) + "\",\n" +
"  \"releases_url\": \"" + EscapeJson(safeReleases) + "\",\n" +
"  \"readme_path\": \"" + EscapeJson(categoryFolder + "/" + modName + "/README.md") + "\",\n" +
"  \"preview_path\": \"" + EscapeJson(categoryFolder + "/" + modName + "/" + previewFileName) + "\",\n" +
"  \"tags\": " + BuildJsonArray(tags) + ",\n" +
"  \"dependencies\": [],\n" +
"  \"conflicts\": [],\n" +
"  \"official\": false\n" +
"}\n";
        }

        private static string BuildCommunityEntryReadme(string modName, string category, string author, string maintainersRaw, string version, string summary, string description, string gameVersion, string castleForgeVersion, string license, string repoUrl, string releasesUrl, string previewFileName)
        {
            string safeSummary = string.IsNullOrWhiteSpace(summary) ? "One-paragraph summary of what this entry does." : summary.Trim();
            string safeDescription = string.IsNullOrWhiteSpace(description) ? "Describe what your project does, why people would want it, and anything important players should know before installing it." : description.Trim();
            string safeGameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "v1.9.9.8.5 Steam" : gameVersion.Trim();
            string safeCastleForgeVersion = string.IsNullOrWhiteSpace(castleForgeVersion) ? "core-v0.1.0+" : castleForgeVersion.Trim();
            string safeLicense = string.IsNullOrWhiteSpace(license) ? "MIT" : license.Trim();
            string safeRepo = string.IsNullOrWhiteSpace(repoUrl) ? "https://github.com/you/repo" : repoUrl.Trim();
            string safeReleases = string.IsNullOrWhiteSpace(releasesUrl) ? safeRepo.TrimEnd('/') + "/releases" : releasesUrl.Trim();
            string maintainers = string.Join(", ", ParseList(maintainersRaw));
            if (string.IsNullOrWhiteSpace(maintainers))
                maintainers = string.IsNullOrWhiteSpace(author) ? "unknowghost" : author.Trim();

            var sb = new StringBuilder();
            sb.Append("# ").Append(modName).Append("\n\n");
            sb.Append("- Category: ").Append(GetCommunityCategoryLabel(category)).Append("\n");
            sb.Append("- Author: ").Append(string.IsNullOrWhiteSpace(author) ? "unknowghost" : author.Trim()).Append("\n");
            sb.Append("- Maintainers: ").Append(maintainers).Append("\n");
            sb.Append("- Version: ").Append(string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim()).Append("\n");
            sb.Append("- Game Version: ").Append(safeGameVersion).Append("\n");
            sb.Append("- CastleForge Version: ").Append(safeCastleForgeVersion).Append("\n");
            sb.Append("- License: ").Append(safeLicense).Append("\n\n");
            sb.Append("## Summary\n\n").Append(safeSummary).Append("\n\n");
            sb.Append("## Description\n\n").Append(safeDescription).Append("\n\n");
            sb.Append("## Links\n\n");
            sb.Append("- Source Repo: ").Append(safeRepo).Append("\n");
            sb.Append("- Releases: ").Append(safeReleases).Append("\n\n");
            sb.Append("## Install\n\n");
            sb.Append("1. Download the latest release from the releases page.\n");
            sb.Append("2. Follow the project instructions in the source repository or release notes.\n");
            sb.Append("3. If this is a gameplay mod DLL, place it in `!Mods`.\n\n");
            sb.Append("## Preview\n\n");
            sb.Append("Replace `").Append(previewFileName).Append("` with your real screenshot or GIF before opening your PR.\n");
            return sb.ToString();
        }

        private static string BuildCommunityEntryGuide(string modName, string categoryFolder)
        {
            return
"CastleForge Community Mods PR guide\n" +
"==================================\n\n" +
"1. Fork https://github.com/RussDev7/CastleForge-CommunityMods\n" +
"2. Open the folder for your content type: " + categoryFolder + "\n" +
"3. Copy this generated folder into " + categoryFolder + "\\" + modName + "\n" +
"4. Replace preview.png / preview.gif with your real media\n" +
"5. Double-check mod.json, README.md, source_repo, and releases_url\n" +
"6. Commit your changes\n" +
"7. Open a pull request back to RussDev7/CastleForge-CommunityMods\n";
        }

        private static void WritePlaceholderPreview(string path)
        {
            if (File.Exists(path))
                return;

            string ext = Path.GetExtension(path) ?? "";
            byte[] bytes;
            if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromBase64String("R0lGODdhAQABAIAAAP///////ywAAAAAAQABAAACAkQBADs=");
            }
            else
            {
                bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Zx0kAAAAASUVORK5CYII=");
            }

            File.WriteAllBytes(path, bytes);
        }

        private static string[] ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            string[] parts = raw.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(item))
                    list.Add(item);
            }
            return list.ToArray();
        }

        private static string[] GetDefaultTags(string category)
        {
            switch (NormalizeCreateCategory(category))
            {
                case "texture-pack":
                    return new[] { "community", "texture-pack" };
                case "weapon-addon":
                    return new[] { "community", "weapon-addon" };
                default:
                    return new[] { "community", "mod" };
            }
        }

        private static string BuildJsonArray(string[] values)
        {
            if (values == null || values.Length == 0)
                return "[]";

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append("\"").Append(EscapeJson(values[i])).Append("\"");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string EscapeJson(string text)
        {
            if (text == null)
                return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private void DrawPreview(SpriteBatch sb, ModCatalogEntry m, int topY)
        {
            int boxX = _rightRect.Left + 10;
            int boxY = topY;
            int boxW = _rightRect.Width - 20;
            int boxH = Math.Max(80, _rightRect.Bottom - boxY - 10);
            var box = new Rectangle(boxX, boxY, boxW, boxH);
            sb.Draw(_white, box, new Color(18, 22, 29, 220));
            DrawBorder(sb, box, new Color(62, 75, 95, 255));

            var lbl = new Vector2(box.Left + 6, box.Top + 4);
            sb.DrawString(_smallFont, "Preview", lbl + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "Preview", lbl, Color.White);

            if (_previewTexture != null && !string.IsNullOrWhiteSpace(_previewLoadedUrl))
            {
                int pad = 8;
                var area = new Rectangle(box.Left + pad, box.Top + 22, box.Width - (pad * 2), box.Height - 22 - pad);
                if (area.Width > 4 && area.Height > 4)
                {
                    float sx = area.Width / (float)_previewTexture.Width;
                    float sy = area.Height / (float)_previewTexture.Height;
                    float s = Math.Min(sx, sy);
                    int w = Math.Max(1, (int)(_previewTexture.Width * s));
                    int h = Math.Max(1, (int)(_previewTexture.Height * s));
                    var dst = new Rectangle(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
                    sb.Draw(_previewTexture, dst, Color.White);
                }
                return;
            }

            string msg = _previewLoading ? "Loading preview..." : "No preview image";
            if (!string.IsNullOrWhiteSpace(_previewFailedUrl) &&
                string.Equals(m.PreviewUrl, _previewFailedUrl, StringComparison.OrdinalIgnoreCase))
            {
                msg = "Preview failed to load.";
            }
            if (!string.IsNullOrWhiteSpace(m.PreviewUrl) &&
                m.PreviewUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                msg = "GIF preview skipped (use PNG for smoother FPS).";
            }
            if (!string.IsNullOrWhiteSpace(m.PreviewPath) && string.IsNullOrWhiteSpace(m.PreviewUrl))
                msg = "Preview path found, but URL could not be resolved.";
            var p = new Vector2(box.Left + 8, box.Top + 28);
            sb.DrawString(_smallFont, Clip(msg, box.Width - 16), p + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(msg, box.Width - 16), p, Color.LightGray);
        }

        private void EnsurePreviewRequested(ModCatalogEntry m)
        {
            if (m == null)
                return;

            string url = (m.PreviewUrl ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
                return;
            if (url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                return;

            Texture2D cached;
            if (_previewCache.TryGetValue(url, out cached) && cached != null)
            {
                if (!ReferenceEquals(_previewTexture, cached))
                {
                    _previewTexture = cached;
                    _previewLoadedUrl = url;
                    _previewFailedUrl = null;
                }
                _previewLoading = false;
                return;
            }

            byte[] cachedBytes = null;
            lock (_previewSync)
            {
                _previewByteCache.TryGetValue(url, out cachedBytes);
            }
            if (cachedBytes != null && cachedBytes.Length > 0)
            {
                _pendingPreviewBytes = cachedBytes;
                _pendingPreviewUrl = url;
                _previewRequestedUrl = url;
                _previewLoading = false;
                return;
            }

            if (string.Equals(url, _previewLoadedUrl, StringComparison.OrdinalIgnoreCase))
                return;
            if (_previewLoading && string.Equals(url, _previewRequestedUrl, StringComparison.OrdinalIgnoreCase))
                return;

            _previewRequestedUrl = url;
            _previewLoading = true;
            _pendingPreviewBytes = null;
            _pendingPreviewUrl = null;

            // Clear stale image immediately so the previous selection's preview
            // never appears while the new selection is loading.
            _previewLoadedUrl = null;
            if (_previewTexture != null)
            {
                _previewTexture = null;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    lock (_previewSync)
                    {
                        if (_previewDownloadsInFlight.Contains(url))
                            return;
                        _previewDownloadsInFlight.Add(url);
                    }

                    byte[] bytes;
                    string err;
                    if (GitHubCatalogService.DownloadBinary(url, MBConfig.HttpTimeoutSeconds, out bytes, out err))
                    {
                        lock (_previewSync)
                        {
                            _previewByteCache[url] = bytes;
                        }
                        _pendingPreviewBytes = bytes;
                        _pendingPreviewUrl = url;
                    }
                    else
                    {
                        _pendingPreviewBytes = new byte[0];
                        _pendingPreviewUrl = url;
                    }
                }
                finally
                {
                    lock (_previewSync)
                    {
                        _previewDownloadsInFlight.Remove(url);
                    }
                    _previewLoading = false;
                }
            });
        }

        private void ConsumePendingPreview(GraphicsDevice device)
        {
            var bytes = _pendingPreviewBytes;
            var pendingUrl = _pendingPreviewUrl;
            if (bytes == null)
                return;

            _pendingPreviewBytes = null;
            _pendingPreviewUrl = null;

            // If selection changed while download was in flight, drop stale image.
            if (string.IsNullOrWhiteSpace(pendingUrl) ||
                !string.Equals(pendingUrl, _previewRequestedUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (bytes.Length == 0)
            {
                _previewLoadedUrl = null;
                _previewFailedUrl = pendingUrl;
                if (_previewTexture != null)
                    _previewTexture = null;
                return;
            }

            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var tex = Texture2D.FromStream(device, ms);
                    Texture2D old;
                    if (_previewCache.TryGetValue(pendingUrl, out old) && old != null && !ReferenceEquals(old, tex))
                        old.Dispose();
                    _previewCache[pendingUrl] = tex;
                    _previewTexture = tex;
                    _previewLoadedUrl = pendingUrl;
                    _previewFailedUrl = null;
                }
            }
            catch
            {
                _previewLoadedUrl = null;
                _previewFailedUrl = pendingUrl;
            }
        }

        private void DrawStatus(SpriteBatch sb)
        {
            var p = new Vector2(_statusRect.Left + 10, _statusRect.Top + 8);
            string s = _status ?? "";
            sb.DrawString(_smallFont, Clip(s, _statusRect.Width - 18), p + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(s, _statusRect.Width - 18), p, _statusColor);
        }

        private void DrawDetailLine(SpriteBatch sb, string label, string value, ref int y)
        {
            string v = string.IsNullOrWhiteSpace(value) ? "-" : value;
            string line = label + ": " + v;
            var p = new Vector2(_rightRect.Left + 10, y);
            sb.DrawString(_smallFont, Clip(line, _rightRect.Width - 18), p + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, Clip(line, _rightRect.Width - 18), p, new Color(210, 214, 220, 255));
            y += _smallFont.LineSpacing + 4;
        }

        private void DrawWrappedText(SpriteBatch sb, string text, Vector2 pos, int width, int maxLines, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string remaining = text.Trim();
            int line = 0;
            while (remaining.Length > 0 && line < maxLines)
            {
                string outLine = remaining;
                if (_smallFont.MeasureString(outLine).X > width)
                {
                    int cut = outLine.Length;
                    while (cut > 1)
                    {
                        string t = outLine.Substring(0, cut).TrimEnd();
                        if (_smallFont.MeasureString(t).X <= width)
                        {
                            int ws = t.LastIndexOf(' ');
                            if (ws > 10)
                                t = t.Substring(0, ws).TrimEnd();
                            outLine = t;
                            break;
                        }
                        cut--;
                    }
                }

                if (line == maxLines - 1 && outLine.Length < remaining.Length)
                    outLine = Clip(outLine + "...", width);

                var p = new Vector2(pos.X, pos.Y + (line * _smallFont.LineSpacing));
                sb.DrawString(_smallFont, outLine, p + new Vector2(1, 1), Color.Black);
                sb.DrawString(_smallFont, outLine, p, color);

                if (outLine.Length >= remaining.Length)
                    break;
                remaining = remaining.Substring(outLine.Length).TrimStart();
                line++;
            }
        }

        private string Clip(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (_smallFont.MeasureString(text).X <= width) return text;
            const string ell = "...";
            float ellWidth = _smallFont.MeasureString(ell).X;
            int availableWidth = width - (int)ellWidth;
            if (availableWidth <= 0) return ell;

            // Binary search for the longest substring that fits
            int left = 0, right = text.Length;
            int bestLength = 0;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                float midWidth = _smallFont.MeasureString(text.Substring(0, mid)).X;
                if (midWidth <= availableWidth)
                {
                    bestLength = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return text.Substring(0, bestLength) + ell;
        }

        private void BeginRefresh()
        {
            if (_loading || _installing)
                return;

            _loading = true;
            _status = "Loading catalog...";
            _statusColor = Color.LightGray;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    MBConfig.LoadApply();
                    string communityErr;
                    string officialErr;
                    var communityMods = GitHubCatalogService.FetchCatalog(MBConfig.CommunityCatalogUrl, MBConfig.HttpTimeoutSeconds, out communityErr);
                    var officialMods = GitHubCatalogService.FetchCatalog(MBConfig.OfficialCatalogUrl, MBConfig.HttpTimeoutSeconds, out officialErr);
                    if ((officialMods == null || officialMods.Count == 0) && !string.IsNullOrWhiteSpace(MBConfig.OfficialReleasesUrl))
                    {
                        string releaseErr;
                        var releaseOfficial = GitHubCatalogService.FetchOfficialReleaseCatalog(MBConfig.OfficialReleasesUrl, MBConfig.HttpTimeoutSeconds, out releaseErr);
                        if (releaseOfficial != null && releaseOfficial.Count > 0)
                            officialMods = releaseOfficial;
                        else if (string.IsNullOrWhiteSpace(officialErr))
                            officialErr = releaseErr;
                    }

                    _allMods.Clear();
                    if (officialMods != null && officialMods.Count > 0)
                    {
                        for (int i = 0; i < officialMods.Count; i++)
                        {
                            var m = officialMods[i];
                            if (m != null) m.Official = true;
                        }
                        _allMods.AddRange(officialMods);
                    }
                    if (communityMods != null && communityMods.Count > 0)
                    {
                        for (int i = 0; i < communityMods.Count; i++)
                        {
                            var m = communityMods[i];
                            if (m != null) m.Official = false;
                        }
                        _allMods.AddRange(communityMods);
                    }
                    ApplySourceFilter();
                    _scroll = 0;
                    if (_allMods.Count > 0)
                    {
                        _status = "Loaded " + _allMods.Count + " mods";
                        _statusColor = Color.LightGreen;
                        BeginPreviewPrefetch();
                    }
                    else
                    {
                        string msg = "";
                        if (!string.IsNullOrWhiteSpace(communityErr))
                            msg += "Community: " + communityErr;
                        if (!string.IsNullOrWhiteSpace(officialErr))
                            msg += (msg.Length > 0 ? " | " : "") + "Official: " + officialErr;
                        _status = msg.Length > 0 ? ("Catalog load failed: " + msg) : "No mods found in catalog.";
                        _statusColor = msg.Length > 0 ? Color.OrangeRed : Color.Yellow;
                    }
                }
                finally
                {
                    _loading = false;
                }
            });
        }

        private void BeginPreviewPrefetch()
        {
            var urls = new List<string>();
            for (int i = 0; i < _mods.Count; i++)
            {
                string u = (_mods[i].PreviewUrl ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(u) && !u.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    urls.Add(u);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    string url = urls[i];
                    lock (_previewSync)
                    {
                        if (_previewByteCache.ContainsKey(url) || _previewDownloadsInFlight.Contains(url))
                            continue;
                        _previewDownloadsInFlight.Add(url);
                    }

                    try
                    {
                        byte[] bytes;
                        string err;
                        if (GitHubCatalogService.DownloadBinary(url, MBConfig.HttpTimeoutSeconds, out bytes, out err) &&
                            bytes != null && bytes.Length > 0)
                        {
                            lock (_previewSync)
                            {
                                _previewByteCache[url] = bytes;
                            }
                        }
                    }
                    finally
                    {
                        lock (_previewSync)
                        {
                            _previewDownloadsInFlight.Remove(url);
                        }
                    }
                }
            });
        }

        private void BeginInstallSelected()
        {
            if (_installing || _loading)
                return;

            if (_selectedIndex < 0 || _selectedIndex >= _mods.Count)
            {
                _status = "Pick a mod first.";
                _statusColor = Color.Yellow;
                return;
            }

            _installing = true;
            _status = "Downloading " + (_mods[_selectedIndex].Name ?? _mods[_selectedIndex].Id ?? "mod") + "...";
            _statusColor = Color.LightGray;

            var entry = _mods[_selectedIndex];
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string modsRoot = Path.GetDirectoryName(typeof(ModBrowserScreen).Assembly.Location) ?? ".";
                    var plan = BuildInstallPlan(entry, out var missing);
                    int installed = 0;
                    int skipped = 0;
                    var failures = new List<string>();

                    for (int i = 0; i < plan.Count; i++)
                    {
                        var mod = plan[i];
                        if (mod == null)
                            continue;

                        if (IsModAlreadyDownloaded(mod, modsRoot))
                        {
                            skipped++;
                            continue;
                        }

                        string detail;
                        bool ok = GitHubCatalogService.DownloadModDll(mod, modsRoot, MBConfig.HttpTimeoutSeconds, out detail);
                        if (ok)
                            installed++;
                        else
                            failures.Add((mod.Name ?? mod.Id ?? "mod") + ": " + detail);
                    }

                    if (failures.Count == 0)
                    {
                        if (installed > 0)
                            _downloadedAnyThisSession = true;
                        _status = "Downloaded " + installed + " mod(s)" +
                                  (skipped > 0 ? (" | Already have: " + skipped) : "") +
                                  (missing.Count > 0 ? (" | Missing deps: " + string.Join(", ", missing.ToArray())) : "");
                        _statusColor = missing.Count > 0 ? Color.Yellow : Color.LightGreen;
                    }
                    else
                    {
                        _status = "Install failed (" + failures.Count + "): " + string.Join(" | ", failures.ToArray());
                        _statusColor = Color.OrangeRed;
                    }
                }
                finally
                {
                    _installing = false;
                }
            });
        }

        private bool IsModAlreadyDownloaded(ModCatalogEntry mod, string modsRoot)
        {
            try
            {
                if (mod == null || string.IsNullOrWhiteSpace(modsRoot) || !Directory.Exists(modsRoot))
                    return false;

                string directUrl = (mod.DllUrl ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(directUrl))
                {
                    string byUrlName = Path.GetFileName(new Uri(directUrl).AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(byUrlName))
                    {
                        string exact = Path.Combine(modsRoot, byUrlName);
                        if (File.Exists(exact))
                            return true;
                    }
                }

                string modName = (mod.Name ?? mod.Id ?? "").Trim();
                if (string.IsNullOrWhiteSpace(modName))
                    return false;

                string s1 = SlugifyForFile(modName);
                string s2 = SlugifyForFile((mod.Id ?? "").Trim());
                var files = Directory.GetFiles(modsRoot, "*.*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string ext = Path.GetExtension(files[i]);
                    if (!string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string fn = SlugifyForFile(Path.GetFileNameWithoutExtension(files[i]));
                    if (!string.IsNullOrWhiteSpace(s1) && string.Equals(fn, s1, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (!string.IsNullOrWhiteSpace(s2) && string.Equals(fn, s2, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (!string.IsNullOrWhiteSpace(s1) && fn.Contains(s1))
                        return true;
                    if (!string.IsNullOrWhiteSpace(s2) && fn.Contains(s2))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string SlugifyForFile(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            var sb = new StringBuilder();
            bool lastDash = false;
            string v = value.Trim().ToLowerInvariant();
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastDash = false;
                }
                else if (!lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
            return sb.ToString().Trim('-');
        }

        private List<ModCatalogEntry> BuildInstallPlan(ModCatalogEntry root, out List<string> missing)
        {
            missing = new List<string>();
            var plan = new List<ModCatalogEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddInstallWithDeps(root, plan, seen, missing);
            return plan;
        }

        private void AddInstallWithDeps(ModCatalogEntry mod, List<ModCatalogEntry> plan, HashSet<string> seen, List<string> missing)
        {
            if (mod == null)
                return;

            string key = GetModKey(mod);
            if (seen.Contains(key))
                return;
            seen.Add(key);

            var deps = GetDependencies(mod);
            for (int i = 0; i < deps.Count; i++)
            {
                string dep = deps[i];
                if (string.IsNullOrWhiteSpace(dep))
                    continue;

                var depMod = FindModByDependencyKey(dep);
                if (depMod == null)
                {
                    if (!missing.Contains(dep))
                        missing.Add(dep);
                    continue;
                }

                AddInstallWithDeps(depMod, plan, seen, missing);
            }

            plan.Add(mod);
        }

        private List<string> GetDependencies(ModCatalogEntry mod)
        {
            var list = new List<string>();
            if (mod == null)
                return list;

            void addTokens(string[] arr)
            {
                if (arr == null) return;
                for (int i = 0; i < arr.Length; i++)
                {
                    string raw = (arr[i] ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    string[] split = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int s = 0; s < split.Length; s++)
                    {
                        string t = split[s].Trim();
                        if (!string.IsNullOrWhiteSpace(t) && !list.Contains(t))
                            list.Add(t);
                    }
                }
            }

            addTokens(mod.Dependencies);
            addTokens(mod.Requires);
            return list;
        }

        private ModCatalogEntry FindModByDependencyKey(string dep)
        {
            string key = (dep ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                return null;

            for (int i = 0; i < _allMods.Count; i++)
            {
                var m = _allMods[i];
                if (m == null)
                    continue;

                if (string.Equals((m.Id ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((m.Name ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return m;
            }

            return null;
        }

        private static string GetModKey(ModCatalogEntry mod)
        {
            if (mod == null) return "";
            string id = (mod.Id ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(id))
                return id.ToLowerInvariant();
            string name = (mod.Name ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name.ToLowerInvariant();
            string url = (mod.DllUrl ?? mod.ReleasesUrl ?? "").Trim();
            return string.IsNullOrWhiteSpace(url) ? Guid.NewGuid().ToString("N") : url.ToLowerInvariant();
        }

        private void DrawButton(SpriteBatch sb, Rectangle rect, string text, Color fill)
        {
            sb.Draw(_white, rect, fill);
            DrawBorder(sb, rect, new Color(120, 130, 150, 255));
            var size = _smallFont.MeasureString(text);
            var pos = new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
            sb.DrawString(_smallFont, text, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, text, pos, Color.White);
        }

        private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color)
        {
            sb.Draw(_white, new Rectangle(rect.Left, rect.Top, rect.Width, 1), color);
            sb.Draw(_white, new Rectangle(rect.Left, rect.Bottom - 1, rect.Width, 1), color);
            sb.Draw(_white, new Rectangle(rect.Left, rect.Top, 1, rect.Height), color);
            sb.Draw(_white, new Rectangle(rect.Right - 1, rect.Top, 1, rect.Height), color);
        }

        private void RebuildVisibleIndices()
        {
            _visibleIndices.Clear();
            string q = (_searchText ?? "").Trim().ToLowerInvariant();
            bool showAll = string.IsNullOrWhiteSpace(q);

            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                if (m == null)
                    continue;

                if (showAll)
                {
                    _visibleIndices.Add(i);
                    continue;
                }

                string hay =
                    ((m.Name ?? "") + " " +
                     (m.Author ?? "") + " " +
                     (m.Version ?? "") + " " +
                     (m.Description ?? "")).ToLowerInvariant();

                if (hay.Contains(q))
                    _visibleIndices.Add(i);
            }
        }

        private void ApplySourceFilter()
        {
            _mods.Clear();
            for (int i = 0; i < _allMods.Count; i++)
            {
                var m = _allMods[i];
                if (m == null)
                    continue;

                if (_sourceMode == BrowserSourceMode.Official && !m.Official)
                    continue;
                if (_sourceMode == BrowserSourceMode.Community && m.Official)
                    continue;

                _mods.Add(m);
            }

            _scroll = 0;
            RebuildVisibleIndices();
            _selectedIndex = _visibleIndices.Count > 0 ? _visibleIndices[0] : -1;
        }

        private void ClampScrollToVisible()
        {
            if (_scroll < 0)
                _scroll = 0;

            int visibleRows = GetBrowserVisibleRows(_smallFont.LineSpacing + 10);
            int maxScroll = Math.Max(0, _visibleIndices.Count - visibleRows);
            if (_scroll > maxScroll)
                _scroll = maxScroll;

            if (_selectedIndex >= 0 && !_visibleIndices.Contains(_selectedIndex))
                _selectedIndex = _visibleIndices.Count > 0 ? _visibleIndices[0] : -1;
        }

        private int GetBrowserListTop()
        {
            return _searchRect.Bottom + 8;
        }

        private int GetBrowserVisibleRows(int rowHeight)
        {
            int top = GetBrowserListTop();
            int usable = _leftRect.Bottom - top - 8;
            return Math.Max(1, usable / Math.Max(1, rowHeight));
        }

        // ────────────────────────────────────────────────────────────────────────
        // BUILD POPUP DRAW
        // ────────────────────────────────────────────────────────────────────────

        private void DrawBuildPopup(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 160));
            sb.Draw(_white, _buildPopupPanelRect, new Color(18, 23, 32, 252));
            DrawBorder(sb, _buildPopupPanelRect, new Color(86, 130, 196, 255));

            string title = _buildCompleted
                ? (_buildSucceeded ? "Build Succeeded" : "Build Failed")
                : "Compiling Mod...";
            Color titleColor = _buildCompleted
                ? (_buildSucceeded ? new Color(100, 220, 110, 255) : new Color(255, 90, 80, 255))
                : Color.White;

            var titlePos = new Vector2(_buildPopupPanelRect.Left + 16, _buildPopupPanelRect.Top + 14);
            sb.DrawString(_smallFont, title, titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, title, titlePos, titleColor);

            // Progress bar
            sb.Draw(_white, _buildPopupBarRect, new Color(22, 30, 44, 255));
            DrawBorder(sb, _buildPopupBarRect, new Color(56, 78, 110, 255));
            if (!_buildCompleted)
            {
                // Animated fill using tick count
                int tick = (Environment.TickCount / 18) % (_buildPopupBarRect.Width - 4);
                int barFill = Math.Min(_buildPopupBarRect.Width - 4, tick + 24);
                sb.Draw(_white, new Rectangle(_buildPopupBarRect.Left + 2, _buildPopupBarRect.Top + 2, barFill, _buildPopupBarRect.Height - 4), new Color(46, 110, 220, 255));
            }
            else
            {
                Color fillColor = _buildSucceeded ? new Color(46, 170, 80, 255) : new Color(180, 50, 50, 255);
                sb.Draw(_white, new Rectangle(_buildPopupBarRect.Left + 2, _buildPopupBarRect.Top + 2, _buildPopupBarRect.Width - 4, _buildPopupBarRect.Height - 4), fillColor);
            }

            // Summary message line
            string msg = _buildPopupMessage ?? "";
            var msgPos = new Vector2(_buildPopupPanelRect.Left + 16, _buildPopupBarRect.Bottom + 8);
            string msgClipped = Clip(msg, _buildPopupPanelRect.Width - 32);
            sb.DrawString(_smallFont, msgClipped, msgPos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, msgClipped, msgPos, new Color(200, 208, 220, 255));

            // Diagnostic detail lines (errors & warnings from build output)
            if (_buildCompleted && _buildDiagLines != null && _buildDiagLines.Count > 0)
            {
                int diagLineH   = _smallFont.LineSpacing + 2;
                int diagAreaTop = (int)msgPos.Y + diagLineH + 4;
                int okTop       = _buildPopupOkRect.Top;
                int maxLines    = Math.Max(1, (okTop - diagAreaTop - 6) / diagLineH);
                int showCount   = Math.Min(maxLines, _buildDiagLines.Count);

                // Thin separator
                int sepY = diagAreaTop - 3;
                sb.Draw(_white, new Rectangle(_buildPopupPanelRect.Left + 16, sepY, _buildPopupPanelRect.Width - 32, 1), new Color(60, 75, 100, 255));

                for (int i = 0; i < showCount; i++)
                {
                    string diagLine = _buildDiagLines[i];
                    bool isError    = System.Text.RegularExpressions.Regex.IsMatch(diagLine, @":\s*error\s+\w+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    bool isWarning  = System.Text.RegularExpressions.Regex.IsMatch(diagLine, @":\s*warning\s+\w+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    Color lineColor = isError ? new Color(255, 100, 80, 255) : isWarning ? new Color(255, 200, 60, 255) : new Color(170, 178, 195, 255);

                    string clipped  = Clip(diagLine, _buildPopupPanelRect.Width - 32);
                    var lp          = new Vector2(_buildPopupPanelRect.Left + 16, diagAreaTop + i * diagLineH);
                    sb.DrawString(_smallFont, clipped, lp + new Vector2(1, 1), Color.Black);
                    sb.DrawString(_smallFont, clipped, lp, lineColor);
                }

                // Overflow hint
                if (_buildDiagLines.Count > showCount)
                {
                    string more    = "...+" + (_buildDiagLines.Count - showCount) + " more (see !Logs\\ModLoader.log)";
                    var morePos    = new Vector2(_buildPopupPanelRect.Left + 16, diagAreaTop + showCount * diagLineH);
                    sb.DrawString(_smallFont, more, morePos, new Color(130, 140, 160, 255));
                }
            }

            // OK button (only when done)
            if (_buildCompleted)
                DrawButton(sb, _buildPopupOkRect, "OK", new Color(50, 65, 90, 255));
        }

        // ────────────────────────────────────────────────────────────────────────
        // MSBUILD NOT-FOUND PROMPT DRAW
        // ────────────────────────────────────────────────────────────────────────

        private void DrawMsbuildPrompt(SpriteBatch sb)
        {
            sb.Draw(_white, _panelRect, new Color(0, 0, 0, 160));
            sb.Draw(_white, _msbuildPromptPanelRect, new Color(18, 23, 32, 252));
            DrawBorder(sb, _msbuildPromptPanelRect, new Color(200, 140, 50, 255));

            var titlePos = new Vector2(_msbuildPromptPanelRect.Left + 16, _msbuildPromptPanelRect.Top + 14);
            sb.DrawString(_smallFont, "MSBuild Not Found", titlePos + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, "MSBuild Not Found", titlePos, new Color(255, 195, 60, 255));

            string line1 = "Visual Studio MSBuild was not found on this PC.";
            string line2 = "Would you like to download and install Visual Studio?";
            var p1 = new Vector2(_msbuildPromptPanelRect.Left + 16, _msbuildPromptPanelRect.Top + 46);
            var p2 = new Vector2(p1.X, p1.Y + _smallFont.LineSpacing + 4);
            sb.DrawString(_smallFont, line1, p1 + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line1, p1, new Color(210, 215, 225, 255));
            sb.DrawString(_smallFont, line2, p2 + new Vector2(1, 1), Color.Black);
            sb.DrawString(_smallFont, line2, p2, new Color(210, 215, 225, 255));

            DrawButton(sb, _msbuildPromptYesRect, "YES, DOWNLOAD", new Color(60, 120, 70, 255));
            DrawButton(sb, _msbuildPromptNoRect, "NO", new Color(80, 58, 58, 255));
        }

        // ────────────────────────────────────────────────────────────────────────
        // BUILD TRIGGER
        // ────────────────────────────────────────────────────────────────────────

        private void TryStartBuild()
        {
            if (_buildBusy)
                return;

            string modsRoot = GetCreatedModsRoot();
            if (!Directory.Exists(modsRoot))
            {
                _status = "No mods folder found.";
                _statusColor = Color.Orange;
                return;
            }

            var modDirs = Directory.GetDirectories(modsRoot);
            if (modDirs.Length == 0)
            {
                _status = "No mod projects found. Click NEW MOD SETUP first.";
                _statusColor = Color.Orange;
                return;
            }

            Array.Sort(modDirs, StringComparer.OrdinalIgnoreCase);
            _buildModSelectorOptions = modDirs.Select(d => new DirectoryInfo(d).Name).ToArray();

            if (_buildModSelectorOptions.Length > 1)
            {
                _buildModSelectorSelectedIndex = 0;
                _showBuildModSelectorPrompt = true;
                return;
            }

            ConfirmBuildModSelection();
        }

        private void MoveCompiledModsToSteamMods()
        {
            if (string.IsNullOrWhiteSpace(_currentModRootPath) || !Directory.Exists(_currentModRootPath))
            {
                _status = "No mod loaded.";
                _statusColor = Color.Orange;
                return;
            }

            try
            {
                string binReleasePath = Path.Combine(_currentModRootPath, "bin", "Release");
                if (!Directory.Exists(binReleasePath))
                {
                    _status = "No bin\\Release folder found. Build the mod first.";
                    _statusColor = Color.Orange;
                    return;
                }

                var dlls = Directory.GetFiles(binReleasePath, "*.dll");
                if (dlls.Length == 0)
                {
                    _status = "No compiled DLLs found in bin\\Release.";
                    _statusColor = Color.Orange;
                    return;
                }

                // Copy directly to game's !Mods folder to load as active mods
                string gameModsPath = Path.Combine(GetGameInstallRoot(), "!Mods");
                if (!Directory.Exists(gameModsPath))
                {
                    _status = "Game !Mods folder not found at: " + gameModsPath;
                    _statusColor = Color.OrangeRed;
                    return;
                }

                int copied = 0;
                foreach (string dllPath in dlls)
                {
                    string fileName = Path.GetFileName(dllPath);
                    string destPath = Path.Combine(gameModsPath, fileName);
                    try
                    {
                        File.Copy(dllPath, destPath, true);
                        copied++;
                    }
                    catch { }
                }

                if (copied > 0)
                {
                    _status = "Copied " + copied + " DLL(s) to !Mods. Restart or reload to activate.";
                    _statusColor = Color.LightGreen;
                }
                else
                {
                    _status = "Failed to copy DLLs to !Mods folder.";
                    _statusColor = Color.OrangeRed;
                }
            }
            catch (Exception ex)
            {
                _status = "Error copying DLLs: " + ex.Message;
                _statusColor = Color.OrangeRed;
            }
        }

        private void ConfirmBuildModSelection()
        {
            if (_buildModSelectorSelectedIndex < 0 || _buildModSelectorSelectedIndex >= _buildModSelectorOptions.Length)
                return;

            string selectedModName = _buildModSelectorOptions[_buildModSelectorSelectedIndex];
            string modRoot = Path.Combine(GetCreatedModsRoot(), selectedModName);
            string csprojPath = Path.Combine(modRoot, selectedModName + ".csproj");
            if (!File.Exists(csprojPath))
            {
                var any = Directory.GetFiles(modRoot, "*.csproj", SearchOption.TopDirectoryOnly);
                csprojPath = any.Length > 0 ? any[0] : "";
            }

            if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath))
            {
                _status = "Could not find .csproj for " + selectedModName + ".";
                _statusColor = Color.OrangeRed;
                return;
            }

            string msbuildPath = FindMSBuildPath();
            if (string.IsNullOrWhiteSpace(msbuildPath))
            {
                _showMsbuildPrompt = true;
                return;
            }

            _buildPendingProjectPath = csprojPath;
            BeginBuild(msbuildPath, csprojPath, modRoot);
        }

        private void BeginBuild(string msbuildExe, string csprojPath, string modRoot)
        {
            _buildBusy = true;
            _buildCompleted = false;
            _buildSucceeded = false;
            _buildPopupMessage = "Starting MSBuild...";
            _buildLastWarningCount = 0;
            _buildLastErrorCount = 0;
            _buildLastSummary = "";
            _showBuildPopup = true;
            _status = "Building mod...";
            _statusColor = Color.LightGray;

            string capturedMsbuild = msbuildExe;
            string capturedProj = csprojPath;
            string capturedModRoot = modRoot;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = capturedMsbuild,
                        Arguments = "\"" + capturedProj + "\" /t:Build /p:Configuration=Release /nologo /verbosity:minimal",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    _buildPopupMessage = "Running MSBuild...";

                    var outputLines = new System.Collections.Generic.List<string>();
                    var fullOutput = new StringBuilder();
                    using (var proc = Process.Start(psi))
                    {
                        string outText = proc.StandardOutput.ReadToEnd();
                        string errText = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();

                        fullOutput.Append(outText).Append(errText);

                        // Combine all output for parsing
                        string combined = outText + "\n" + errText;
                        foreach (string rawLine in combined.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                        {
                            string trimmed = rawLine.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                                outputLines.Add(trimmed);
                        }

                        ParseBuildOutput(outputLines, proc.ExitCode, out int warnings, out int errors, out bool succeeded, out string summary, out List<string> diagLines);
                        _buildLastWarningCount = warnings;
                        _buildLastErrorCount   = errors;
                        _buildSucceeded        = succeeded;
                        _buildLastSummary      = summary;
                        _buildPopupMessage     = summary;
                        _buildDiagLines        = diagLines;
                        _buildCompleted = true;
                        _status = succeeded
                            ? ("Build succeeded — Warnings: " + warnings + "  Errors: " + errors)
                            : ("Build FAILED — Errors: " + errors + "  Warnings: " + warnings);
                        _statusColor = succeeded ? Color.LightGreen : Color.OrangeRed;
                        if (succeeded)
                            _downloadedAnyThisSession = true;

                        try
                        {
                            string outputLogPath = Path.Combine(capturedModRoot, "output.log");
                            WriteFileUtf8(outputLogPath, fullOutput.ToString());
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    _buildCompleted = true;
                    _buildSucceeded = false;
                    _buildLastErrorCount = 1;
                    _buildLastSummary = "Build error: " + ex.Message;
                    _buildPopupMessage = "Build error: " + ex.Message;
                    _status = "Build failed: " + ex.Message;
                    _statusColor = Color.OrangeRed;
                }
                finally
                {
                    _buildBusy = false;
                }
            });
        }

        private static void ParseBuildOutput(System.Collections.Generic.List<string> lines,
            int exitCode, out int warnings, out int errors, out bool succeeded, out string summary,
            out List<string> diagLines)
        {
            warnings  = 0;
            errors    = 0;
            diagLines = new List<string>();

            var summaryRx = new Regex(@"(\d+)\s+Warning\(s\).*?(\d+)\s+Error\(s\)", RegexOptions.IgnoreCase);
            var errLineRx = new Regex(@":\s*error\s+\w+",   RegexOptions.IgnoreCase);
            var warnLineRx= new Regex(@":\s*warning\s+\w+", RegexOptions.IgnoreCase);

            bool foundSummary = false;
            foreach (string line in lines)
            {
                var sm = summaryRx.Match(line);
                if (sm.Success)
                {
                    warnings     = int.Parse(sm.Groups[1].Value);
                    errors       = int.Parse(sm.Groups[2].Value);
                    foundSummary = true;
                }

                // Collect error & warning lines for display (strip long path prefix for readability)
                if (errLineRx.IsMatch(line) || warnLineRx.IsMatch(line))
                {
                    // Shorten "C:\...\File.cs(12,5): error CS0246: ..." to "File.cs(12,5): error ..."
                    string display = line;
                    int parenIdx = line.IndexOf('(');
                    if (parenIdx > 0)
                    {
                        int slashIdx = line.LastIndexOfAny(new[] { '\\', '/' }, parenIdx);
                        if (slashIdx >= 0)
                            display = line.Substring(slashIdx + 1);
                    }
                    diagLines.Add(display);
                }
            }

            if (!foundSummary)
            {
                errors   = diagLines.Count(l => errLineRx.IsMatch(l));
                warnings = diagLines.Count(l => warnLineRx.IsMatch(l));
            }

            succeeded = exitCode == 0 && errors == 0;
            summary   = succeeded
                ? ("Done  —  Warnings: " + warnings + "  Errors: " + errors)
                : ("Failed  —  Errors: " + errors + "  Warnings: " + warnings);
        }

        private static string FindMSBuildPath()
        {
            // 1. Try vswhere (ships with VS 2017+)
            try
            {
                string vswhere = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft Visual Studio", "Installer", "vswhere.exe");

                if (File.Exists(vswhere))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = vswhere,
                        Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string found = (p.StandardOutput.ReadToEnd() ?? "").Trim();
                        p.WaitForExit();
                        if (!string.IsNullOrWhiteSpace(found) && File.Exists(found))
                            return found;
                    }
                }
            }
            catch { }

            // 2. Check well-known VS 2022 / 2019 / 2017 paths
            string[] programDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            string[] vsVersions  = { "2022", "2019", "2017" };
            string[] vsEditions  = { "Enterprise", "Professional", "Community", "BuildTools" };
            string[] msbuildRels = { @"MSBuild\Current\Bin\MSBuild.exe", @"MSBuild\15.0\Bin\MSBuild.exe" };

            foreach (string pf in programDirs)
            {
                if (string.IsNullOrWhiteSpace(pf)) continue;
                foreach (string ver in vsVersions)
                    foreach (string ed in vsEditions)
                        foreach (string rel in msbuildRels)
                        {
                            string candidate = Path.Combine(pf, "Microsoft Visual Studio", ver, ed, rel);
                            if (File.Exists(candidate))
                                return candidate;
                        }
            }

            // 3. Try PATH via where.exe
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "msbuild",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string found = (p.StandardOutput.ReadLine() ?? "").Trim();
                    p.WaitForExit();
                    if (!string.IsNullOrWhiteSpace(found) && File.Exists(found))
                        return found;
                }
            }
            catch { }

            return null;
        }

        private void LaunchVisualStudio()
        {
            try
            {
                // Ensure the list of created mods is refreshed
                EnsureCreateTemplatesLoadedIfAny();

                // Check if VS path is configured in config file
                string vsPath = MBConfig.VisualStudioPath;

                // If not configured, show warning once
                if (string.IsNullOrWhiteSpace(vsPath))
                {
                    if (!MBConfig.VisualStudioWarningShown)
                    {
                        System.Windows.Forms.MessageBox.Show(
                            "Please make sure you have Visual Studio path set in the ModBrowser config file before clicking on this button.\r\n\r\n" +
                            "Set the 'VisualStudioPath' setting in your ModBrowser.Config.ini file.\r\n\r\n" +
                            "Example: VisualStudioPath=C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\devenv.exe",
                            "Visual Studio Path Not Configured",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Warning);

                        MBConfig.VisualStudioWarningShown = true;
                        MBConfig.Save();
                    }
                    _status = "Visual Studio path not configured. Please set it in ModBrowser.Config.ini";
                    _statusColor = Color.Red;
                    return;
                }

                // Try configured path first, then fall back to hardcoded paths
                string[] visualStudioPaths = new[]
                {
                    vsPath,
                    @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\devenv.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\devenv.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe"
                };

                foreach (var currentVsPath in visualStudioPaths)
                {
                    if (File.Exists(currentVsPath))
                    {
                        // Get the current mod folder (reconstruct full path like LoadModAtIndex does)
                        if (_availableModFolders.Length > 0)
                        {
                            string modName = _availableModFolders[_selectedModFolderIndex];
                            string modPath = Path.Combine(GetCreatedModsRoot(), modName);
                            string solutionPath = Path.Combine(modPath, "*.sln");
                            string[] slnFiles = Directory.GetFiles(modPath, "*.sln");
                            string[] csprojFiles = Directory.GetFiles(modPath, "*.csproj");

                            if (slnFiles.Length > 0)
                            {
                                Process.Start(currentVsPath, "\"" + slnFiles[0] + "\"");
                                _status = "Opening Visual Studio...";
                                _statusColor = Color.LightGreen;
                            }
                            else if (csprojFiles.Length > 0)
                            {
                                // Fall back to opening the .csproj file directly
                                Process.Start(currentVsPath, "\"" + csprojFiles[0] + "\"");
                                _status = "Opening Visual Studio...";
                                _statusColor = Color.LightGreen;
                            }
                            else
                            {
                                _status = "No .sln or .csproj file found in mod folder.";
                                _statusColor = Color.Red;
                            }
                        }
                        else
                        {
                            _status = "No mod folder selected.";
                            _statusColor = Color.Red;
                        }
                        return;
                    }
                }

                _status = "Visual Studio not found.";
                _statusColor = Color.Red;
            }
            catch (Exception ex)
            {
                _status = "Error launching Visual Studio: " + ex.Message;
                _statusColor = Color.Red;
            }
        }
    }
}
