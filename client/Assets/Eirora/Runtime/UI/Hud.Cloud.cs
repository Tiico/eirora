using System.Globalization;
using System.Threading.Tasks;
using Eirora.Client.App;
using Eirora.CloudSaving;
using UnityEngine;
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
        private bool _showingRecoveryCode;

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
                if (choice.Outcome == SyncOutcome.KeyNeeded)
                {
                    ShowKeyNeeded(choice);
                }
                else
                {
                    ShowWorldChoice(choice);
                }
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

            if (cloud.ContributionPartLocked)
            {
                cloud.AcknowledgeLocked();
                Toast("Din ö är sparad på kontot. Kontots gamla egenvårdsdel är låst på servern i 90 dagar.");
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
            Text("sheet-text", card, $"Kontot har redan en ö{CloudWorld(choice)}. Den här telefonen har en annan ö.");
            if (choice.ContributionsUnreadable)
            {
                Text("sheet-text", card, "Egenvården i kontots ö går inte att läsa på den här telefonen. Hämtar du den räknas dagarna hittills som vilodagar, och bidragen räknas igen från nästa spelardygn.");
                Text("sheet-text", card, "Har du en återställningskod? Skriv in den, så läses egenvården här också.");
                RecoveryCodeEntry(card);
            }

            Text("sheet-text", card, "Den ö du inte väljer sparas undan på telefonen och raderas inte.");
            Button("button button-wide button-primary", card, "Hämta ön från kontot", () => Choose(WorldChoice.TakeCloud));
            Button("button button-wide", card, "Behåll telefonens ö", () => Choose(WorldChoice.KeepPhone));
        }

        /// <summary>
        /// The account has a world and this phone has none, but its self-care is sealed with a key this phone lacks
        /// (TASK-023b): the old phone or a recovery code unlocks it, or the player goes on without it (D-101).
        /// </summary>
        private void ShowKeyNeeded(SyncResult choice)
        {
            var card = Container("card setup", _middle);
            _choiceCard = card;
            Text("setup-title", card, "Din ö finns på kontot");
            Text("sheet-text", card, $"Kontot har en ö{CloudWorld(choice)}. Egenvården i den är krypterad med en nyckel som den här telefonen saknar, och servern kan inte läsa den heller.");
            Text("sheet-text", card, "Har du en återställningskod kan du skriva in den här.");
            RecoveryCodeEntry(card);
            Text("sheet-text", card, "Utan nyckeln hämtas ön ändå, men dagarna till och med i dag räknas som vilodagar och bidragen räknas igen från nästa spelardygn.");
            Button("button button-wide", card, "Fortsätt utan nyckeln", () => Choose(WorldChoice.TakeCloud));
        }

        private static string CloudWorld(SyncResult choice)
        {
            string saved = choice.CloudSavedAt is { } at ? $", senast sparad {at.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : string.Empty;
            string day = choice.CloudGameDay is { } gameDay ? $" på spelardygn {gameDay.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
            return day + saved;
        }

        /// <summary>A field for the recovery code and the button that tries it; the cloud save follows by itself.</summary>
        private void RecoveryCodeEntry(VisualElement card)
        {
            // The key must not end up in the keyboard's dictionary or its cloud suggestions: no autocorrection, and the
            // field is cleared once the code has been tried.
            var field = new TextField { multiline = false, keyboardType = TouchScreenKeyboardType.ASCIICapable };
            field.autoCorrection = false;
            field.AddToClassList("recovery-code-field");
            card.Add(field);
            Button("button button-wide button-primary", card, "Lås upp med koden", () =>
            {
                string code = field.value;
                field.value = string.Empty;
                _ = UnlockAsync(code);
            });
        }

        private async Task UnlockAsync(string code)
        {
            if (_cloud is not { } cloud)
            {
                return;
            }

            if (Texts.RecoveryCode(await cloud.EnterRecoveryCodeAsync(code)) is { } message)
            {
                Toast(message);
            }
        }

        /// <summary>Opens the account sheet on the recovery code, which is one of the key's copies (ADR-0008).</summary>
        public void OpenRecoveryCode()
        {
            if (_open != Sheet.Account)
            {
                OpenAccount();
            }

            _showingRecoveryCode = true;
            ShowSheet();
        }

        private void RecoveryCodeSheet(string code)
        {
            Text("sheet-title", _sheet, "Återställningskod");
            Text("sheet-text", _sheet, "Skriv ner koden och förvara den säkert. Med den läser en ny telefon din egenvård på kontot, och den som har koden kan läsa den också.");
            var groups = code.Split('-');
            var lines = new string[groups.Length / 2];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = groups[2 * i] + "-" + groups[(2 * i) + 1];
            }

            Text("sheet-text recovery-code", _sheet, string.Join("\n", lines));
            Text("sheet-text", _sheet, "Koden byts när en ny nyckelepok börjar; visa den igen då.");
            Button("button button-wide", _sheet, "Dölj", () =>
            {
                _showingRecoveryCode = false;
                ShowSheet();
            });
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

        /// <summary>The cloud's world was taken without its contribution part (D-101): what that means for the game days so far.</summary>
        private void ShowKeyEpochNotice(CloudSaveSync cloud)
        {
            var card = Container("card setup", _middle);
            _noticeCard = card;
            Text("setup-title", card, "Egenvården kunde inte läsas här");
            Text("sheet-text", card, cloud.CloudSaveUnreadable
                ? "Din ö är hämtad, men egenvården från tidigare dagar gick inte att läsa med den här telefonens nyckel. Servern kan inte läsa den heller."
                : "Din ö är hämtad, men egenvården från tidigare dagar är krypterad med en nyckel som den här telefonen saknar. Servern kan inte läsa den heller.");
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
