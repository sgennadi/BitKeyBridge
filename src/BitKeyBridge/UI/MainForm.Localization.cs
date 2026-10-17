namespace BitKeyBridge;

public sealed partial class MainForm
{
    private UiLanguage _language = UiLanguage.English;
    private readonly List<ToolStripMenuItem> _languageChoices = [];
    private ToolStripMenuItem? _toolsMenuItem;
    private ToolStripMenuItem? _languageMenu;

    private ToolStripMenuItem BuildLanguageMenu(MenuStrip menu)
    {
        _toolsMenuItem = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault();
        _languageMenu = new ToolStripMenuItem(
            UiLocalizationService.Get(_language, "Language"))
        {
            Name = "LanguageMenu",
            AccessibleName = "Select UI language"
        };
        foreach (var choice in Enum.GetValues<UiLanguage>())
        {
            var selected = choice;
            var option = new ToolStripMenuItem(
                UiLocalizationService.Get(_language, choice.ToString()))
            {
                Name = "UiLanguage_" + choice,
                Tag = choice,
                Checked = _language == choice,
                CheckOnClick = false
            };
            option.Click += (_, _) => SetNavigationLanguage(selected);
            _languageMenu.DropDownItems.Add(option);
            _languageChoices.Add(option);
        }
        return _languageMenu;
    }

    private void SetNavigationLanguage(UiLanguage requested)
    {
        if (_language == requested)
            return;
        // Preference persistence failure must not silently change recovery
        // policy or crash an active Helpdesk session.
        try
        {
            if (!_layoutSelfTest)
                UiLocalizationService.SavePreference(requested);
        }
        catch (Exception ex)
        {
            ShowAppError(
                "Saving display language failed. The previous language was kept.",
                "UiLanguagePreference", ex);
            return;
        }
        _language = requested;
        ApplyNavigationLanguage();
    }

    private void ApplyNavigationLanguage()
    {
        var names = new[]
        {
            "Start", "BitLocker", "LAPS", "Devices",
            "Helpdesk", "Diagnostics", "Settings"
        };
        for (var i = 0; i < _mainTabs.TabPages.Count && i < names.Length; i++)
            _mainTabs.TabPages[i].Text = UiLocalizationService.Get(_language, names[i]);

        var rtl = UiLocalizationService.IsRightToLeft(_language);
        // Mirror navigation chrome, not the exact-match computer IDs, account
        // names and recovery identifiers. Text inputs always remain LTR.
        _mainTabs.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        _mainTabs.RightToLeftLayout = rtl;
        if (MainMenuStrip is not null)
            MainMenuStrip.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        foreach (var query in new[] { _homeQuery, _startQuery, _lapsQuery, _unifiedQuery })
            query.RightToLeft = RightToLeft.No;
        foreach (var page in _mainTabs.TabPages.Cast<TabPage>())
        {
            foreach (var helpdeskQuery in page.Controls
                         .Find("HelpdeskQuickSearch", searchAllChildren: true)
                         .OfType<TextBox>())
                helpdeskQuery.RightToLeft = RightToLeft.No;
        }

        if (_toolsMenuItem is not null)
            _toolsMenuItem.Text = UiLocalizationService.Get(_language, "Tools");
        if (_languageMenu is not null)
            _languageMenu.Text = UiLocalizationService.Get(_language, "Language");
        foreach (var option in _languageChoices)
        {
            if (option.Tag is UiLanguage language)
            {
                option.Text = UiLocalizationService.Get(_language, language.ToString());
                option.Checked = language == _language;
            }
        }
    }
}
