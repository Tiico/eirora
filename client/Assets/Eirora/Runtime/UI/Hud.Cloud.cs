using System.Globalization;
using Eirora.Client.App;
using Eirora.CloudSaving;
using UnityEngine.UIElements;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The cloud save in the interface (ADR-0008, TASK-023): a line in the account sheet, the choice between the
    /// phone's world and the cloud's, and the notice when the contribution part could not be read on this phone.
    /// </summary>
    public sealed partial class Hud
    {
        private CloudSaveSync? _cloud;
        private VisualElement? _choiceCard;
        private VisualElement? _noticeCard;

        /// <summary>Follows the cloud save, or stops following it with null.</summary>
        public void UseCloudSync(CloudSaveSync? cloud)
        {
            if (_cloud is not null)
            {
                _cloud.Changed -= OnCloudChanged;
            }

            _cloud = cloud;
            if (cloud is not null)
            {
                cloud.Changed += OnCloudChanged;
                OnCloudChanged();
            }
        }

        private void OnCloudChanged()
        {
            if (_cloud is not { } cloud)
            {
                return;
            }

            if (cloud.Choice is { } choice && _choiceCard is null)
            {
                ShowWorldChoice(choice);
            }
            else if (cloud.Choice is null && _choiceCard is { } settled)
            {
                _choiceCard = null;
                _middle.Remove(settled);
            }

            if (cloud.Welcome)
            {
                cloud.AcknowledgeWelcome();
                Toast("Välkommen tillbaka till din ö.");
            }

            if (cloud.KeyEpochBegan && _noticeCard is null)
            {
                ShowKeyEpochNotice(cloud);
            }

            if (_open == Sheet.Account)
            {
                ShowSheet();
            }
        }

        private string? CloudStatusLine() => _cloud is { } cloud ? Texts.CloudSync(cloud.Status, cloud.SyncedAt) : null;

        /// <summary>The phone and the account hold different worlds: the player chooses, and the other is kept aside.</summary>
        private void ShowWorldChoice(SyncResult choice)
        {
            var card = Container("card setup", _middle);
            _choiceCard = card;
            Text("setup-title", card, "Vilken ö vill du behålla?");
            string saved = choice.CloudSavedAt is { } at ? $", senast sparad {at.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : string.Empty;
            string day = choice.CloudGameDay is { } gameDay ? $" på spelardygn {gameDay.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
            Text("sheet-text", card, $"Kontot har redan en ö{day}{saved}. Den här telefonen har en annan ö.");
            if (choice.ContributionsUnreadable)
            {
                Text("sheet-text", card, "Egenvården i kontots ö går inte att läsa på den här telefonen. Hämtar du den räknas dagarna hittills som vilodagar, och bidragen räknas igen från nästa spelardygn.");
            }

            Text("sheet-text", card, "Den ö du inte väljer sparas undan på telefonen och raderas inte.");
            Button("button button-wide button-primary", card, "Hämta ön från kontot", () => Choose(WorldChoice.TakeCloud));
            Button("button button-wide", card, "Behåll telefonens ö", () => Choose(WorldChoice.KeepPhone));
        }

        private void Choose(WorldChoice choice)
        {
            if (_choiceCard is { } card)
            {
                _choiceCard = null;
                _middle.Remove(card);
            }

            _ = _cloud?.ChooseAsync(choice);
        }

        /// <summary>A new key epoch began here (D-101): what that means for the game days so far.</summary>
        private void ShowKeyEpochNotice(CloudSaveSync cloud)
        {
            var card = Container("card setup", _middle);
            _noticeCard = card;
            Text("setup-title", card, "Egenvården kunde inte läsas här");
            Text("sheet-text", card, "Din ö är hämtad, men egenvården från tidigare dagar är krypterad med en nyckel som den här telefonen saknar. Servern kan inte läsa den heller.");
            Text("sheet-text", card, "Dagarna till och med i dag räknas som vilodagar, och bidragen räknas igen från nästa spelardygn. Byn, krafterna och allt du byggt är kvar.");
            Text("sheet-text", card, "Den gamla bidragsdelen finns kvar på servern i 90 dagar, om nyckeln skulle dyka upp.");
            Button("button button-wide button-primary", card, "Jag förstår", () =>
            {
                _noticeCard = null;
                _middle.Remove(card);
                cloud.AcknowledgeKeyEpoch();
            });
        }
    }
}
