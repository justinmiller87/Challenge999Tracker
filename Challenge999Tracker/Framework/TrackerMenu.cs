using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Challenge999Tracker.Framework;

/// <summary>
///   The collection-board-style checklist window: a "Totals" dashboard landing page whose rows double as
///   category links (no permanent sidebar of 30 tabs - see the Qi reference mod's Dashboard/Workspace split,
///   which this mirrors), and a scrollable, searchable, filterable per-category item list with live Have/999
///   and the workbook's true cross-sheet Required/Total.
/// </summary>
public sealed class TrackerMenu : IClickableMenu
{
  private const string TotalsTabKey = "\0totals";

  // ---- chrome layout (all offsets are from xPositionOnScreen/yPositionOnScreen) ----
  private const int TitleAreaHeight = 56;
  private const int NavRowHeight = 56;
  private const int ContentMarginTop = 8;
  private const int ContentMarginBottom = 24;
  private const int ContentMarginX = 32;
  private const int NavButtonHeight = 44;
  private const int BackButtonWidth = 160;
  private const int FilterButtonWidth = 220;

  // ---- item list (unchanged from before) ----
  private const int RowHeight = 58;
  private const int IconSize = 48;

  // ---- totals dashboard ----
  private const int TotalsRowHeight = 24;
  private const int TotalsHeaderRowHeight = 30;
  private const int TotalsFooterReserve = 56; // "N more" hint line + gap + the pinned grand-total row
  private const int TotalsGrandTotalRowHeight = 24;
  private static readonly int[] TotalsColumnWidths = { 300, 70, 90, 90, 70, 250, 250 };

  private readonly List<ProgressEntry> _allEntries;
  private readonly TotalsPage _totalsPage;
  private readonly ModConfig _config;
  private readonly Action _saveConfig;
  private readonly ITranslationHelper _translation;
  private readonly TextBox _searchBox;
  private readonly TextBox _totalsSearchBox;
  private readonly ClickableComponent _filterButton;
  private readonly ClickableComponent _backButton;

  private string _activeSheet;
  private List<ProgressEntry> _visibleEntries = new();
  private int _scrollIndex;
  private int _hoveredIndex = -1;
  private int _totalsScroll;
  private int _hoveredTotalsRow = -1;
  private string _lastSearchText = "";
  private string _lastTotalsSearchText = "";
  private Rectangle _listArea;

  /// <summary>True while the Totals landing page is showing cross-sheet search results instead of the sheet rollup - lets a player who isn't sure which category an item lives under just search for it there.</summary>
  private bool IsSearchingTotals => _activeSheet == TotalsTabKey && _totalsSearchBox.Text.Trim().Length > 0;

  public TrackerMenu(
    List<ProgressEntry> entries, TotalsPage totalsPage,
    ModConfig config, Action saveConfig, ITranslationHelper translation)
    : base(
      Game1.uiViewport.Width / 2 - Math.Min(1200, Game1.uiViewport.Width - 64) / 2,
      Game1.uiViewport.Height / 2 - Math.Min(760, Game1.uiViewport.Height - 64) / 2,
      Math.Min(1200, Game1.uiViewport.Width - 64),
      Math.Min(760, Game1.uiViewport.Height - 64),
      showUpperRightCloseButton: true)
  {
    _allEntries = entries;
    _totalsPage = totalsPage;
    _config = config;
    _saveConfig = saveConfig;
    _translation = translation;
    _activeSheet = TotalsTabKey; // the dashboard is the landing page - no permanent tab strip

    var navY = yPositionOnScreen + TitleAreaHeight;
    _backButton = new ClickableComponent(new Rectangle(xPositionOnScreen + ContentMarginX, navY, BackButtonWidth, NavButtonHeight), "back");
    _filterButton = new ClickableComponent(
      new Rectangle(xPositionOnScreen + width - FilterButtonWidth - ContentMarginX, navY, FilterButtonWidth, NavButtonHeight), "filter");
    _searchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
    {
      X = _backButton.bounds.Right + 16,
      Y = navY,
      Width = _filterButton.bounds.X - 16 - (_backButton.bounds.Right + 16),
      Height = NavButtonHeight,
    };
    var totalsSearchLabelWidth = (int)Game1.smallFont.MeasureString(_translation.Get("menu.search.totals-label")).X;
    _totalsSearchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
    {
      X = xPositionOnScreen + ContentMarginX + totalsSearchLabelWidth + 12,
      Y = navY,
      Width = width - 2 * ContentMarginX - totalsSearchLabelWidth - 12,
      Height = NavButtonHeight,
    };

    var contentTop = yPositionOnScreen + TitleAreaHeight + NavRowHeight + ContentMarginTop;
    _listArea = new Rectangle(
      xPositionOnScreen + ContentMarginX, contentTop,
      width - 2 * ContentMarginX, height - TitleAreaHeight - NavRowHeight - ContentMarginTop - ContentMarginBottom);

    RefreshVisibleEntries();
  }

  private ChecklistVisibility GetVisibility(string sheet) =>
    _config.ChecklistVisibilityBySheet.TryGetValue(sheet, out var raw) && Enum.TryParse<ChecklistVisibility>(raw, out var v)
      ? v
      : ChecklistVisibility.All;

  private void SetVisibility(string sheet, ChecklistVisibility visibility)
  {
    _config.ChecklistVisibilityBySheet[sheet] = visibility.ToString();
    _saveConfig();
  }

  private void RefreshVisibleEntries()
  {
    if (_activeSheet == TotalsTabKey)
    {
      _visibleEntries = new List<ProgressEntry>();
      _scrollIndex = 0;
      _lastSearchText = _searchBox?.Text ?? "";
      return;
    }

    var visibility = GetVisibility(_activeSheet);
    var search = _searchBox.Text.Trim();
    _visibleEntries = _allEntries
      .Where(e => e.Sheet == _activeSheet)
      .Where(e => visibility.Matches(e))
      .Where(e => search.Length == 0 || e.Item.Contains(search, StringComparison.OrdinalIgnoreCase))
      .ToList();
    _scrollIndex = 0;
    _lastSearchText = _searchBox.Text;
  }

  /// <summary>Populates <see cref="_visibleEntries" /> with every item across every sheet matching the Totals page's search box, so a player unsure which category something lives under can just search for it there - reuses the same item-row renderer/scroll/hover as a normal sheet view.</summary>
  private void RefreshTotalsSearchEntries()
  {
    var search = _totalsSearchBox.Text.Trim();
    _visibleEntries = search.Length == 0
      ? new List<ProgressEntry>()
      : _allEntries
        .Where(e => e.Item.Contains(search, StringComparison.OrdinalIgnoreCase))
        .OrderBy(e => e.Sheet, StringComparer.Ordinal)
        .ThenBy(e => e.Item, StringComparer.Ordinal)
        .ToList();
    _scrollIndex = 0;
    _lastTotalsSearchText = _totalsSearchBox.Text;
  }

  private int VisibleRowCount => Math.Max(1, _listArea.Height / RowHeight);

  /// <summary>How many Totals rows fit between the header row and the reserved footer band - see the class remarks for the arithmetic this depends on.</summary>
  private int TotalsRowsPerPage => Math.Max(1, (_listArea.Height - TotalsHeaderRowHeight - TotalsFooterReserve) / TotalsRowHeight);

  private int MaxTotalsScroll => Math.Max(0, _totalsPage.Sheets.Count - TotalsRowsPerPage);

  public override void update(GameTime time)
  {
    base.update(time);
    _searchBox.Update();
    if (_searchBox.Text != _lastSearchText)
    {
      RefreshVisibleEntries();
    }

    _totalsSearchBox.Update();
    if (_activeSheet == TotalsTabKey && _totalsSearchBox.Text != _lastTotalsSearchText)
    {
      RefreshTotalsSearchEntries();
    }
  }

  /// <summary>
  ///   The base game's menu-button keybind (E/Esc by default) closes whatever menu is open as soon as it's
  ///   pressed - it doesn't check whether a TextBox currently has keyboard focus first. That means typing a
  ///   search term containing "e" would otherwise slam the tracker shut mid-keystroke. Swallow it here while
  ///   either search box is focused, except Escape, which should still close the menu as expected.
  /// </summary>
  public override void receiveKeyPress(Keys key)
  {
    if ((_searchBox.Selected || _totalsSearchBox.Selected) && key != Keys.Escape)
    {
      return;
    }

    base.receiveKeyPress(key);
  }

  public override void receiveLeftClick(int x, int y, bool playSound = true)
  {
    base.receiveLeftClick(x, y, playSound);

    if (_activeSheet == TotalsTabKey)
    {
      if (IsSearchingTotals)
      {
        if (_listArea.Contains(x, y))
        {
          var resultIndex = _scrollIndex + (y - _listArea.Y) / RowHeight;
          if (resultIndex >= 0 && resultIndex < _visibleEntries.Count)
          {
            var entry = _visibleEntries[resultIndex];
            _activeSheet = entry.Sheet;
            _searchBox.Text = entry.Item;
            RefreshVisibleEntries();
            Game1.playSound("shwip");
          }
        }

        return;
      }

      var sheetIndex = TotalsRowIndexAt(x, y);
      if (sheetIndex is >= 0 && sheetIndex < _totalsPage.Sheets.Count)
      {
        _activeSheet = _totalsPage.Sheets[sheetIndex.Value].Sheet;
        RefreshVisibleEntries();
        Game1.playSound("shwip");
      }

      return;
    }

    if (_backButton.bounds.Contains(x, y))
    {
      _activeSheet = TotalsTabKey;
      if (IsSearchingTotals)
      {
        RefreshTotalsSearchEntries();
      }
      else
      {
        RefreshVisibleEntries();
      }

      Game1.playSound("shwip");
      return;
    }

    if (_filterButton.bounds.Contains(x, y))
    {
      SetVisibility(_activeSheet, GetVisibility(_activeSheet).Next());
      RefreshVisibleEntries();
      Game1.playSound("shwip");
    }
  }

  /// <summary>Maps a click/hover point to a Totals sheet-row index (accounting for scroll), or null if it's outside the scrollable row band.</summary>
  private int? TotalsRowIndexAt(int x, int y)
  {
    var rowsTop = _listArea.Y + TotalsHeaderRowHeight;
    var rowsBottom = rowsTop + TotalsRowsPerPage * TotalsRowHeight;
    if (x < _listArea.X || x > _listArea.Right || y < rowsTop || y >= rowsBottom)
    {
      return null;
    }

    return _totalsScroll + (y - rowsTop) / TotalsRowHeight;
  }

  public override void receiveScrollWheelAction(int direction)
  {
    base.receiveScrollWheelAction(direction);
    if (_activeSheet == TotalsTabKey && !IsSearchingTotals)
    {
      _totalsScroll = Math.Clamp(_totalsScroll - Math.Sign(direction), 0, MaxTotalsScroll);
      return;
    }

    var maxScroll = Math.Max(0, _visibleEntries.Count - VisibleRowCount);
    _scrollIndex = Math.Clamp(_scrollIndex - Math.Sign(direction), 0, maxScroll);
  }

  public override void performHoverAction(int x, int y)
  {
    base.performHoverAction(x, y);
    _hoveredIndex = -1;
    _hoveredTotalsRow = -1;

    if (_activeSheet == TotalsTabKey && !IsSearchingTotals)
    {
      var sheetIndex = TotalsRowIndexAt(x, y);
      if (sheetIndex is >= 0 && sheetIndex < _totalsPage.Sheets.Count)
      {
        _hoveredTotalsRow = sheetIndex.Value;
      }

      return;
    }

    if (!_listArea.Contains(x, y))
    {
      return;
    }

    var index = _scrollIndex + (y - _listArea.Y) / RowHeight;
    if (index >= 0 && index < _visibleEntries.Count)
    {
      _hoveredIndex = index;
    }
  }

  public override void draw(SpriteBatch b)
  {
    drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

    // Title sits inside the box's own top edge, not floating above the border.
    Utility.drawTextWithShadow(b, _translation.Get("menu.title"), Game1.dialogueFont,
      new Vector2(xPositionOnScreen + ContentMarginX, yPositionOnScreen + 12), Game1.textColor);

    if (_activeSheet == TotalsTabKey)
    {
      Utility.drawTextWithShadow(b, _translation.Get("menu.search.totals-label"), Game1.smallFont,
        new Vector2(xPositionOnScreen + ContentMarginX, _totalsSearchBox.Y + 12), Game1.textColor);
      _totalsSearchBox.Draw(b);
      if (IsSearchingTotals)
      {
        DrawRows(b, showSheet: true);
        DrawHoveredEntry(b);
      }
      else
      {
        DrawTotalsPage(b);
      }
    }
    else
    {
      DrawBackButton(b);
      _searchBox.Draw(b);
      DrawFilterButton(b);
      DrawRows(b);
      DrawHoveredEntry(b);
    }

    base.draw(b);
    drawMouse(b);
  }

  private void DrawBackButton(SpriteBatch b)
  {
    drawTextureBox(b, _backButton.bounds.X, _backButton.bounds.Y, _backButton.bounds.Width, _backButton.bounds.Height, Color.White);
    var label = "<< " + _translation.Get("menu.totals-tab");
    var textSize = Game1.smallFont.MeasureString(label);
    Utility.drawTextWithShadow(b, label, Game1.smallFont,
      new Vector2(_backButton.bounds.Center.X - textSize.X / 2, _backButton.bounds.Center.Y - textSize.Y / 2), Game1.textColor);
  }

  private void DrawFilterButton(SpriteBatch b)
  {
    var label = GetVisibility(_activeSheet) switch
    {
      ChecklistVisibility.Incomplete => _translation.Get("menu.filter.incomplete"),
      ChecklistVisibility.Complete => _translation.Get("menu.filter.complete"),
      _ => _translation.Get("menu.filter.all"),
    };
    drawTextureBox(b, _filterButton.bounds.X, _filterButton.bounds.Y, _filterButton.bounds.Width, _filterButton.bounds.Height, Color.White);
    var textSize = Game1.smallFont.MeasureString(label.ToString());
    Utility.drawTextWithShadow(b, label, Game1.smallFont,
      new Vector2(_filterButton.bounds.Center.X - textSize.X / 2, _filterButton.bounds.Center.Y - textSize.Y / 2), Game1.textColor);
  }

  private void DrawHoveredEntry(SpriteBatch b)
  {
    if (_hoveredIndex >= 0 && _hoveredIndex < _visibleEntries.Count)
    {
      DrawHover(b, _visibleEntries[_hoveredIndex]);
    }
  }

  /// <summary>Draws the icon/name/Have/Total item rows. <paramref name="showSheet" /> is set only for the Totals page's cross-sheet search results, where the entries aren't all from one category, so each row also names which sheet it lives under.</summary>
  private void DrawRows(SpriteBatch b, bool showSheet = false)
  {
    if (_visibleEntries.Count == 0)
    {
      var emptyText = showSheet ? _translation.Get("menu.search.no-matches").ToString() : "No items match.";
      Utility.drawTextWithShadow(b, emptyText, Game1.smallFont, new Vector2(_listArea.X, _listArea.Y), Game1.textColor);
      return;
    }

    var y = _listArea.Y;
    for (var row = 0; row < VisibleRowCount; row++)
    {
      var index = _scrollIndex + row;
      if (index >= _visibleEntries.Count)
      {
        break;
      }

      var entry = _visibleEntries[index];
      var rowBounds = new Rectangle(_listArea.X, y, _listArea.Width, RowHeight - 4);
      if (index == _hoveredIndex)
      {
        b.Draw(Game1.staminaRect, rowBounds, Color.White * 0.25f);
      }

      DrawIcon(b, entry, new Vector2(rowBounds.X + 2, rowBounds.Y + 2));

      var rowColor = entry.IsComplete ? Color.DarkGreen : Game1.textColor;
      var itemX = rowBounds.X + IconSize + 16;
      Utility.drawTextWithShadow(b, entry.Item, Game1.smallFont, new Vector2(itemX, rowBounds.Y + 8), rowColor);
      if (showSheet)
      {
        var itemWidth = Game1.smallFont.MeasureString(entry.Item).X;
        Utility.drawTextWithShadow(b, $"({entry.Sheet})", Game1.smallFont, new Vector2(itemX + itemWidth + 10, rowBounds.Y + 8), Color.Gray);
      }

      // Three columns, anchored by their LEFT edge (haveColumnX/statusColumnX) but each value is drawn
      // right-aligned to end 16px before the next column's start - otherwise a long Required (anything in the
      // thousands-to-millions, e.g. Resources/Stone at 2,250,248 is the dataset's max) overflows a fixed-width
      // slot and runs into the next column's text. Tightened 40px/20px from the previous fixed anchors to
      // close up the dead whitespace after the item name, per the user's request; see the class remarks for
      // the worst-case width check this was verified against.
      var haveColumnX = rowBounds.Right - 340;
      var statusColumnX = rowBounds.Right - 160;

      var totalText = $"Total {entry.Total:N0} / {entry.Required:N0}";
      var totalWidth = Game1.smallFont.MeasureString(totalText).X;
      Utility.drawTextWithShadow(b, totalText, Game1.smallFont, new Vector2(haveColumnX - 16 - totalWidth, rowBounds.Y + 8), rowColor);

      var haveText = entry.Have is null ? _translation.Get("menu.unknown-short").ToString() : $"{entry.Have:N0} / 999";
      var haveWidth = Game1.smallFont.MeasureString(haveText).X;
      Utility.drawTextWithShadow(b, haveText, Game1.smallFont, new Vector2(statusColumnX - 16 - haveWidth, rowBounds.Y + 8), rowColor);

      var statusText = entry.Have is null ? "" : entry.IsComplete ? "done!" : $"{entry.ToGo:N0} to go";
      Utility.drawTextWithShadow(b, statusText, Game1.smallFont, new Vector2(statusColumnX, rowBounds.Y + 8), rowColor);

      y += RowHeight;
    }
  }

  private static void DrawIcon(SpriteBatch b, ProgressEntry entry, Vector2 position)
  {
    var box = new Rectangle((int)position.X, (int)position.Y, IconSize, IconSize);
    drawTextureBox(b, box.X, box.Y, box.Width, box.Height, Color.White);
    if (entry.IconQualifiedId is null)
    {
      return;
    }

    var data = ItemRegistry.GetDataOrErrorItem(entry.IconQualifiedId);
    var tint = entry.IsComplete ? Color.White : Color.Gray;
    b.Draw(data.GetTexture(), new Rectangle(box.X + 6, box.Y + 6, IconSize - 12, IconSize - 12), data.GetSourceRect(), tint);
  }

  private void DrawHover(SpriteBatch b, ProgressEntry entry)
  {
    var lines = new List<string>();
    lines.Add(entry.Have is null
      ? _translation.Get("menu.hover.unknown").ToString()
      : _translation.Get("menu.hover.breakdown", new { normal = entry.Normal, silver = entry.Silver, gold = entry.Gold, iridium = entry.Iridium }).ToString());

    if (entry.Matched.Length > 0)
    {
      lines.Add(_translation.Get("menu.hover.matched", new { names = entry.Matched }).ToString());
    }

    if (entry.Breakdown.Count > 1)
    {
      lines.Add("");
      lines.Add(_translation.Get("menu.hover.required-breakdown").ToString());
      lines.AddRange(entry.Breakdown.Select(t => $"{t.Contribution:N0} ({t.Label})"));
    }

    drawHoverText(b, string.Join("\n", lines), Game1.smallFont);
  }

  // ------------------------------------------------------------------ Totals dashboard
  //
  // Row-fit arithmetic (see the PR/commit notes for the full derivation): for the default 1200x760 window,
  // _listArea is 1136x616 starting 120px below the box top. With TotalsHeaderRowHeight=30 and
  // TotalsFooterReserve=56, TotalsRowsPerPage = (616-30-56)/24 = 22, and the 22nd row's bottom edge lands at
  // listArea.Y+30+22*24 = listArea.Y+678, which is <= listArea.Y+616-56 = listArea.Y+680 - it fits with 2px to
  // spare, and the pinned grand-total row's bottom edge lands exactly on listArea.Bottom. 30 sheets - 22
  // visible = 8 rows of scroll range.

  private void DrawTotalsPage(SpriteBatch b)
  {
    var x0 = _listArea.X;
    var headerY = _listArea.Y;
    DrawTotalsRow(b, x0, headerY, "Sheet", "Done", "%", "Share", "Items", "Required", "Total", Game1.textColor);

    var rowsTop = _listArea.Y + TotalsHeaderRowHeight;
    var rowsPerPage = TotalsRowsPerPage;
    var y = rowsTop;
    for (var i = 0; i < rowsPerPage; i++)
    {
      var sheetIndex = _totalsScroll + i;
      if (sheetIndex >= _totalsPage.Sheets.Count)
      {
        break;
      }

      var sheet = _totalsPage.Sheets[sheetIndex];
      if (sheetIndex == _hoveredTotalsRow)
      {
        b.Draw(Game1.staminaRect, new Rectangle(x0, y, _listArea.Width, TotalsRowHeight), Color.White * 0.25f);
      }

      DrawTotalsRow(
        b, x0, y, sheet.Sheet, sheet.Done ? "Yes" : "No", $"{sheet.PercentDone:0.#}%", $"{sheet.ShareOfChallenge:0.#}%",
        sheet.ItemCount.ToString(), $"{sheet.RequiredSum:N0}", $"{sheet.TotalSum:N0}",
        sheet.Done ? Color.DarkGreen : Game1.textColor);
      y += TotalsRowHeight;
    }

    var grandTotalY = _listArea.Bottom - TotalsGrandTotalRowHeight;
    if (_totalsPage.Sheets.Count > rowsPerPage)
    {
      var shown = Math.Min(rowsPerPage, _totalsPage.Sheets.Count - _totalsScroll);
      var hint = $"Showing {_totalsScroll + 1}-{_totalsScroll + shown} of {_totalsPage.Sheets.Count} - scroll for more";
      Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(x0, grandTotalY - 28), Color.Gray);
    }

    var grand = _totalsPage.GrandTotal;
    DrawTotalsRow(
      b, x0, grandTotalY, grand.Sheet, grand.Done ? "Yes" : "No", $"{grand.PercentDone:0.#}%", "100%",
      grand.ItemCount.ToString(), $"{grand.RequiredSum:N0}", $"{grand.TotalSum:N0}",
      grand.Done ? Color.DarkGreen : Color.DarkRed);
  }

  private static void DrawTotalsRow(SpriteBatch b, int x0, int y, string sheet, string done, string percent, string share, string items, string required, string total, Color color)
  {
    var x = x0;
    var values = new[] { sheet, done, percent, share, items, required, total };
    for (var i = 0; i < values.Length; i++)
    {
      var w = TotalsColumnWidths[i];
      if (i == 0)
      {
        Utility.drawTextWithShadow(b, values[i], Game1.smallFont, new Vector2(x, y), color);
      }
      else
      {
        // Right-align numeric/status columns so e.g. "56" and "7,778,963" line up as an actual table.
        var textWidth = Game1.smallFont.MeasureString(values[i]).X;
        Utility.drawTextWithShadow(b, values[i], Game1.smallFont, new Vector2(x + w - textWidth - 8, y), color);
      }

      x += w;
    }
  }
}
