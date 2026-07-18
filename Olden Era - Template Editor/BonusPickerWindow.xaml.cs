using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class BonusPickerWindow : Window
    {
        public BonusEntry?       Result  { get; private set; }
        public List<BonusEntry>  Results { get; private set; } = [];

        private readonly HashSet<string> _existingKeys;
        private readonly HashSet<string> _existingItemIds;
        private readonly HashSet<string> _existingSpellIds;

        public BonusPickerWindow(IEnumerable<BonusEntry>? existingBonuses = null)
        {
            InitializeComponent();
            CmbType.SelectedIndex     = 0;
            CmbReceiver.SelectedIndex = 0;

            var existing = existingBonuses?.ToList() ?? [];
            _existingKeys     = existing.Select(b => b.ToString()).ToHashSet();
            _existingItemIds  = existing
                .Where(b => b.PresetType == BonusPresetType.StartingItem)
                .Select(b => b.Param)
                .ToHashSet();
            _existingSpellIds = existing
                .Where(b => b.PresetType == BonusPresetType.Spell)
                .Select(b => b.Param)
                .ToHashSet();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private BonusPresetType SelectedType
        {
            get
            {
                if (CmbType.SelectedItem is ComboBoxItem ci && ci.Tag is string tag && int.TryParse(tag, out int t))
                    return (BonusPresetType)t;
                return BonusPresetType.TownPortalFree;
            }
        }

        private string SelectedReceiver
        {
            get
            {
                if (CmbReceiver.SelectedItem is ComboBoxItem ci && ci.Tag is string tag)
                    return tag;
                return "start_hero";
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void CmbType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (CmbType.SelectedItem == null) return; // ignore re-entrant selection during construction/Text sync
            var type = SelectedType;

            PnlSpell.Visibility      = type == BonusPresetType.Spell              ? Visibility.Visible : Visibility.Collapsed;
            PnlMultiplier.Visibility = type == BonusPresetType.UnitMultiplier     ? Visibility.Visible : Visibility.Collapsed;
            PnlMovement.Visibility   = type == BonusPresetType.MovementBonus      ? Visibility.Visible : Visibility.Collapsed;
            PnlItem.Visibility       = type == BonusPresetType.StartingItem       ? Visibility.Visible : Visibility.Collapsed;
            bool isResource = type is BonusPresetType.StartingGold
                                         or BonusPresetType.StartingGems
                                         or BonusPresetType.StartingCrystals
                                         or BonusPresetType.StartingMercury
                                         or BonusPresetType.StartingWood
                                         or BonusPresetType.StartingOre;

            PnlResources.Visibility = isResource ? Visibility.Visible : Visibility.Collapsed;
            PnlReceiver.Visibility  = isResource ? Visibility.Collapsed : Visibility.Visible;

            if (PnlResources.Visibility == Visibility.Visible)
            {
                (LblResourceAmount.Text, TxtResourceAmount.Text) = type switch
                {
                    BonusPresetType.StartingGold     => (Services.Localization.LocalizationManager.T("S.Bonus.AmtGold"),     "10000"),
                    BonusPresetType.StartingGems     => (Services.Localization.LocalizationManager.T("S.Bonus.AmtGems"),     "15"),
                    BonusPresetType.StartingCrystals => (Services.Localization.LocalizationManager.T("S.Bonus.AmtCrystals"), "15"),
                    BonusPresetType.StartingMercury  => (Services.Localization.LocalizationManager.T("S.Bonus.AmtMercury"),  "15"),
                    BonusPresetType.StartingWood     => (Services.Localization.LocalizationManager.T("S.Bonus.AmtWood"),     "20"),
                    BonusPresetType.StartingOre      => (Services.Localization.LocalizationManager.T("S.Bonus.AmtOre"),      "20"),
                    _                                => (Services.Localization.LocalizationManager.T("S.Bonus.021"),         "10000"),
                };
            }
        }

        private void BtnPickSpell_Click(object sender, RoutedEventArgs e)
        {
            var picker = new SpellPickerWindow(_existingSpellIds) { Owner = this };
            if (picker.ShowDialog() != true) return;

            if (picker.SelectedIds.Count > 0)
            {
                TxtSpell.Text = string.Join(", ", picker.SelectedIds);
                ChkMakeFree.IsChecked = picker.MakeFree;
                Results = picker.SelectedIds
                    .Select(id => new BonusEntry
                    {
                        PresetType     = BonusPresetType.Spell,
                        ReceiverFilter = SelectedReceiver,
                        Param          = id,
                        Param2         = picker.MakeFree ? "1" : "0",
                    })
                    .ToList();
            }
        }

        private void BtnPickItem_Click(object sender, RoutedEventArgs e)
        {
            var entries = KnownValues.BannableItems
                .Select(b => new BanEntry { Id = b.Id, DisplayName = b.DisplayName, Category = b.Category });
            var picker = new ItemPickerWindow(entries, _existingItemIds, Services.Localization.LocalizationManager.T("S.Bonus.ChooseItem")) { Owner = this };
            if (picker.ShowDialog() != true) return;

            if (picker.SelectedIds.Count > 0)
            {
                TxtItem.Text = string.Join(", ", picker.SelectedIds);
                Results = picker.SelectedIds
                    .Select(id => new BonusEntry
                    {
                        PresetType     = BonusPresetType.StartingItem,
                        ReceiverFilter = SelectedReceiver,
                        Param          = id,
                    })
                    .ToList();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var type     = SelectedType;
            var receiver = SelectedReceiver;
            string param  = "";
            string param2 = "0";

            switch (type)
            {
                case BonusPresetType.Spell:
                    // If the spell picker already produced one or more entries (single or multi-select),
                    // use them directly so the user's visible selection is what gets added.
                    if (Results.Count > 0 && Results.All(b => b.PresetType == BonusPresetType.Spell))
                    {
                        AddPickedResults(receiver);
                        return;
                    }
                    param = TxtSpell.Text.Trim();
                    if (string.IsNullOrEmpty(param))
                    {
                        MessageBox.Show(Services.Localization.LocalizationManager.T("S.Bonus.SelectSpellFirst"), Services.Localization.LocalizationManager.T("S.Bonus.Validation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    param2 = ChkMakeFree.IsChecked == true ? "1" : "0";
                    break;

                case BonusPresetType.UnitMultiplier:
                    param = TxtMultiplier.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "2";
                    break;

                case BonusPresetType.MovementBonus:
                    param = TxtMovement.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "0";
                    break;

                case BonusPresetType.StartingItem:
                    if (Results.Count > 0 && Results.All(b => b.PresetType == BonusPresetType.StartingItem))
                    {
                        AddPickedResults(receiver);
                        return;
                    }
                    param = TxtItem.Text.Trim();
                    if (string.IsNullOrEmpty(param))
                    {
                        MessageBox.Show(Services.Localization.LocalizationManager.T("S.Bonus.EnterArtifactId"), Services.Localization.LocalizationManager.T("S.Bonus.Validation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    break;

                default: // StartingGold, StartingGems, StartingCrystals, StartingMercury
                    param = TxtResourceAmount.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "0";
                    break;
            }

            var candidate = new BonusEntry
            {
                PresetType     = type,
                ReceiverFilter = receiver,
                Param          = param,
                Param2         = param2,
            };

            if (_existingKeys.Contains(candidate.ToString()))
            {
                MessageBox.Show(Services.Localization.LocalizationManager.T("S.Bonus.AlreadyAdded"), Services.Localization.LocalizationManager.T("S.Bonus.Duplicate"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Result       = candidate;
            Results      = [Result];
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        // Adds the entries already produced by the spell/item picker (which the user can see in
        // the TxtSpell/TxtItem box). Re-applies the current receiver and skips already-added ones.
        private void AddPickedResults(string receiver)
        {
            var toAdd = Results
                .Where(b => !_existingKeys.Contains(b.ToString()))
                .Select(b => new BonusEntry
                {
                    PresetType     = b.PresetType,
                    ReceiverFilter = receiver,
                    Param          = b.Param,
                    Param2         = b.Param2,
                })
                .ToList();

            if (toAdd.Count == 0)
            {
                MessageBox.Show(Services.Localization.LocalizationManager.T("S.Bonus.AlreadyAdded"), Services.Localization.LocalizationManager.T("S.Bonus.Duplicate"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Results      = toAdd;
            Result       = toAdd[0];
            DialogResult = true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
