using System;
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
    /// phone's world and the cloud's, the notice when the contribution part could not be read on this phone, and the
    /// key transfer between two phones (TASK-023b): the new one shows a QR code, the old one scans it and sends the key
    /// once the player has confirmed it.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>How often the new phone asks the server whether the key has come while its code is on screen.</summary>
        private const long TransferPollMs = 2000;

        /// <summary>How often the old phone looks for the code in the camera's picture.</summary>
        private const long ScanMs = 250;

        private CloudSaveSync? _cloud;
        private IQrScanner? _scanner;
        private VisualElement? _choiceCard;
        private SyncResult? _shownChoice;
        private VisualElement? _noticeCard;
        private bool _showingRecoveryCode;
        private bool _sendingKey;
        private KeyTransferTicket? _ticketToSend;
        private bool _sendInFlight;
        private bool _polling;
        private string? _rejectedCode;
        private IVisualElementScheduledItem? _transferPoll;
        private IVisualElementScheduledItem? _scanTick;
        private Action? _pollTransfer;
        private Action? _leaveTransferCode;
        private Action? _scan;
        private Action? _confirmSend;

        /// <summary>Follows the cloud save, or stops following it with null; the scanner is the camera for sending the key to a new phone.</summary>
        public void UseCloudSync(CloudSaveSync? cloud, IQrScanner? scanner = null)
        {
            if (_cloud is not null)
            {
                _cloud.Changed -= OnCloudChanged;
            }

            StopKeySending();
            _cloud = cloud;
            _scanner = scanner;
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

            if (cloud.Choice is { } choice)
            {
                // The question is rebuilt when it changes in kind, as when the key arrives and the cloud's self-care
                // becomes readable; the same question stays, with the QR code it may be showing.
                if (_shownChoice is null || choice.Outcome != _shownChoice.Outcome || choice.ContributionsUnreadable != _shownChoice.ContributionsUnreadable)
                {
                    RemoveChoiceCard();
                    ShowChoice(choice);
                }
            }
            else
            {
                RemoveChoiceCard();
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

        private void ShowChoice(SyncResult choice)
        {
            var card = Container("card setup", _middle);
            _choiceCard = card;
            _shownChoice = choice;
            ChoiceContent(card, choice);
        }

        private void RemoveChoiceCard()
        {
            if (_choiceCard is not { } card)
            {
                return;
            }

            _choiceCard = null;
            _shownChoice = null;
            _leaveTransferCode = null;
            StopPollingTransfer();
            ReleaseCode(card.Q<Image>(className: "qr-code"));
            _middle.Remove(card);
        }

        private void ChoiceContent(VisualElement card, SyncResult choice)
        {
            if (choice.Outcome == SyncOutcome.KeyNeeded)
            {
                KeyNeededContent(card, choice);
            }
            else
            {
                WorldChoiceContent(card, choice);
            }
        }

        /// <summary>The phone and the account hold different worlds: the player chooses, and the other is kept aside.</summary>
        private void WorldChoiceContent(VisualElement card, SyncResult choice)
        {
            Text("setup-title", card, "Vilken ö vill du behålla?");
            Text("sheet-text", card, $"Kontot har redan en ö{CloudWorld(choice)}. Den här telefonen har en annan ö.");
            if (choice.ContributionsUnreadable)
            {
                Text("sheet-text", card, "Egenvården i kontots ö går inte att läsa på den här telefonen. Hämtar du den räknas dagarna hittills som vilodagar, och bidragen räknas igen från nästa spelardygn.");
                KeyOffer(card, choice);
            }

            Text("sheet-text", card, "Den ö du inte väljer sparas undan på telefonen och raderas inte.");
            Button("button button-wide button-primary", card, "Hämta ön från kontot", () => Choose(WorldChoice.TakeCloud));
            Button("button button-wide", card, "Behåll telefonens ö", () => Choose(WorldChoice.KeepPhone));
        }

        /// <summary>
        /// The account has a world and this phone has none, but its self-care is sealed with a key this phone lacks
        /// (TASK-023b): the old phone or a recovery code unlocks it, or the player goes on without it (D-101).
        /// </summary>
        private void KeyNeededContent(VisualElement card, SyncResult choice)
        {
            Text("setup-title", card, "Din ö finns på kontot");
            Text("sheet-text", card, $"Kontot har en ö{CloudWorld(choice)}. Egenvården i den är krypterad med en nyckel som den här telefonen saknar, och servern kan inte läsa den heller.");
            KeyOffer(card, choice);
            Text("sheet-text", card, "Utan nyckeln hämtas ön ändå, men dagarna till och med i dag räknas som vilodagar och bidragen räknas igen från nästa spelardygn.");
            Button("button button-wide", card, "Fortsätt utan nyckeln", () => Choose(WorldChoice.TakeCloud));
        }

        /// <summary>The two ways the key gets here: the old phone through a QR code, or the recovery code typed in.</summary>
        private void KeyOffer(VisualElement card, SyncResult choice)
        {
            Text("sheet-text", card, "Har du din gamla telefon kvar skickar den nyckeln hit: visa en QR-kod här och skanna den där.");
            Button("button button-wide button-primary", card, "Visa QR-kod för din gamla telefon", () => ShowTransferCode(card, choice));
            Text("sheet-text", card, "Har du en återställningskod kan du skriva in den här.");
            RecoveryCodeEntry(card);
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

        /// <summary>
        /// The new phone's side of the key transfer (TASK-023b): the ticket as a QR code for the old phone to scan, its
        /// check code for the player to compare, and a poll for the key every <see cref="TransferPollMs"/> while it is
        /// on screen. The ticket's secret is in the code and nowhere else, so the code is shown only here, and only
        /// until the player leaves.
        /// </summary>
        private void ShowTransferCode(VisualElement card, SyncResult choice)
        {
            if (_cloud is not { } cloud)
            {
                return;
            }

            card.Clear();
            Text("setup-title", card, "Skanna med din gamla telefon");
            Text("sheet-text", card, "Öppna Konto på den gamla telefonen, välj Skicka nyckeln till en ny telefon och rikta kameran mot koden.");
            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("qr-code");
            card.Add(image);
            var check = Text("sheet-text check-code", card, string.Empty);
            var status = Text("sheet-text transfer-status", card, "Hämtar en kod …");
            var again = Button("button button-wide button-primary", card, "Visa en ny kod", () => ShowTransferCode(card, choice));
            again.style.display = DisplayStyle.None;
            _leaveTransferCode = () =>
            {
                StopPollingTransfer();
                _leaveTransferCode = null;
                _ = cloud.CancelKeyTransferAsync();
                ReleaseCode(image);
                card.Clear();
                ChoiceContent(card, choice);
            };
            Button("button button-wide", card, "Avbryt", () => _leaveTransferCode?.Invoke());

            StopPollingTransfer();
            _ = BeginTransferAsync(cloud, card, image, check, status, again);
        }

        private void StopPollingTransfer()
        {
            _transferPoll?.Pause();
            _transferPoll = null;
            _pollTransfer = null;
        }

        /// <summary>Takes the code off the screen and frees its texture.</summary>
        private static void ReleaseCode(Image? image)
        {
            if (image?.image is Texture2D code)
            {
                image.image = null;
                UnityEngine.Object.DestroyImmediate(code);
            }
        }

        /// <summary>For the tests, which have no panel to click in: what the button under the key question does.</summary>
        internal void ShowTransferCode()
        {
            if (_choiceCard is { } card && _cloud?.Choice is { } choice)
            {
                ShowTransferCode(card, choice);
            }
        }

        /// <summary>For the tests: the "Avbryt" under the code.</summary>
        internal void LeaveTransferCode() => _leaveTransferCode?.Invoke();

        /// <summary>For the tests: what the code's card does every <see cref="TransferPollMs"/>.</summary>
        internal void PollTransferNow() => _pollTransfer?.Invoke();

        /// <summary>For the tests: what the scanner sheet does every <see cref="ScanMs"/>.</summary>
        internal void ScanNow() => _scan?.Invoke();

        /// <summary>For the tests: the "Skicka nyckeln" button once a code has been read.</summary>
        internal void ConfirmSend() => _confirmSend?.Invoke();

        private async Task BeginTransferAsync(CloudSaveSync cloud, VisualElement card, Image image, Label check, Label status, Button again)
        {
            var begun = await cloud.BeginKeyTransferAsync();
            if (_choiceCard != card)
            {
                return; // The card went away while the server was asked.
            }

            if (begun == KeyTransferOutcome.Waiting && cloud.TransferTicket is { } ticket)
            {
                image.image = QrImage.Of(QrCodes.Encode(ticket));
                check.text = $"Kontrollkod {cloud.TransferCheckCode}. Den gamla telefonen visar samma kod innan den skickar.";
                status.text = Texts.KeyTransfer(begun, cloud.TransferExpiresAt);
                _pollTransfer = () =>
                {
                    if (!_polling)
                    {
                        _ = PollTransferAsync(cloud, card, image, check, status, again); // One question to the server at a time.
                    }
                };
                _transferPoll = card.schedule.Execute(() => _pollTransfer?.Invoke()).Every(TransferPollMs);
                return;
            }

            status.text = Texts.KeyTransfer(begun, null);
            again.style.display = DisplayStyle.Flex;
        }

        private async Task PollTransferAsync(CloudSaveSync cloud, VisualElement card, Image image, Label check, Label status, Button again)
        {
            KeyTransferOutcome outcome;
            _polling = true;
            try
            {
                outcome = await cloud.PollKeyTransferAsync();
            }
            finally
            {
                _polling = false;
            }

            if (outcome == KeyTransferOutcome.Received)
            {
                // The question went away with the key, or became the plain choice between two readable islands.
                Toast(cloud.Choice is null ? "Nyckeln kom fram. Din ö hämtas från kontot." : "Nyckeln kom fram. Egenvården i kontots ö går nu att läsa här.");
                return;
            }

            if (_choiceCard != card)
            {
                return;
            }

            status.text = Texts.KeyTransfer(outcome, cloud.TransferExpiresAt);
            if (cloud.TransferTicket is null)
            {
                // The transfer is over without the key: the code is useless now and must not be scanned.
                StopPollingTransfer();
                ReleaseCode(image);
                check.text = string.Empty;
                again.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>Opens the account sheet on the camera, for sending the key to a new phone (TASK-023b).</summary>
        public void OpenKeySending()
        {
            if (_open != Sheet.Account)
            {
                OpenAccount();
            }

            _sendingKey = true;
            _ticketToSend = null;
            ShowSheet();
        }

        /// <summary>
        /// The old phone's side of the key transfer: the camera's picture, read for the new phone's code every
        /// <see cref="ScanMs"/>. A ticket that was read is put to the player with its check code, and the key is sent
        /// only when they confirm: anyone who can create a transfer for the account could otherwise collect the key by
        /// getting a code of their own in front of the camera. The camera runs only while the picture is on screen.
        /// </summary>
        private void KeySendingSheet(CloudSaveSync cloud, IQrScanner scanner)
        {
            Text("sheet-title", _sheet, "Skicka nyckeln");
            if (_ticketToSend is { } ticket)
            {
                StopKeySending();
                Text("sheet-text", _sheet, "Skicka nyckeln till telefonen som visar koden?");
                Text("sheet-text check-code", _sheet, $"Kontrollkod {ticket.CheckCode}");
                Text("sheet-text", _sheet, "Samma kontrollkod står under QR-koden på den nya telefonen. Stämmer den inte, skicka inte: nyckeln låser upp din egenvård för den som får den.");
                if (_sendInFlight)
                {
                    Text("sheet-text", _sheet, "Skickar …");
                }
                else
                {
                    _confirmSend = () => _ = SendKeyAsync(cloud, ticket);
                    Button("button button-wide button-primary", _sheet, "Skicka nyckeln", () => _confirmSend?.Invoke());
                    Button("button button-wide", _sheet, "Skanna igen", () =>
                    {
                        _ticketToSend = null;
                        ShowSheet();
                    });
                }

                Button("button button-wide", _sheet, "Avbryt", LeaveKeySending);
                return;
            }

            Text("sheet-text", _sheet, "Rikta kameran mot QR-koden som den nya telefonen visar. Du får bekräfta innan nyckeln skickas.");
            var frame = Container("camera-frame", _sheet);
            var preview = new Image { scaleMode = ScaleMode.ScaleAndCrop };
            preview.AddToClassList("camera-preview");
            frame.Add(preview);
            var status = Text("sheet-text scanner-status", _sheet, string.Empty);
            Button("button button-wide", _sheet, "Avbryt", LeaveKeySending);

            scanner.Start();
            _scanTick?.Pause();
            _scan = () => Scan(scanner, preview, status);
            _scanTick = _sheet.schedule.Execute(() => _scan?.Invoke()).Every(ScanMs);
            _scan();
        }

        private void LeaveKeySending()
        {
            _sendingKey = false;
            _ticketToSend = null;
            ShowSheet();
        }

        private void Scan(IQrScanner scanner, Image preview, Label status)
        {
            status.text = Texts.Scanner(scanner.Status);
            if (scanner.Status != ScannerStatus.Scanning)
            {
                return;
            }

            if (preview.image != scanner.Preview)
            {
                // The frame is square and clips, so the turned picture stays in its slot whatever the camera's angle.
                preview.image = scanner.Preview;
                preview.style.rotate = new Rotate(new Angle(scanner.PreviewRotation));
                preview.style.scale = new Scale(new Vector2(1, scanner.PreviewMirrored ? -1 : 1));
            }

            if (scanner.Read() is not { } text)
            {
                return;
            }

            if (KeyTransferTicket.TryParse(text) is not { } ticket)
            {
                if (_rejectedCode != text)
                {
                    _rejectedCode = text; // Said once per code in view; the player goes on looking for the right one.
                    Toast(Texts.KeyDelivery(KeyDeliveryOutcome.BadTicket));
                }

                return;
            }

            _ticketToSend = ticket;
            ShowSheet(); // The question to the player; the camera stops with it.
        }

        private async Task SendKeyAsync(CloudSaveSync cloud, KeyTransferTicket ticket)
        {
            KeyDeliveryOutcome outcome;
            _sendInFlight = true;
            _confirmSend = null;
            ShowSheet();
            try
            {
                outcome = await cloud.SendKeyAsync(ticket);
            }
            finally
            {
                _sendInFlight = false;
            }

            if (!_sendingKey)
            {
                return;
            }

            Toast(Texts.KeyDelivery(outcome));
            LeaveKeySending();
        }

        /// <summary>Closes the camera; called whenever the sheet is rebuilt as something other than the picture.</summary>
        private void StopKeySending()
        {
            _scanTick?.Pause();
            _scanTick = null;
            _scan = null;
            _rejectedCode = null;
            _scanner?.Stop();
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
            RemoveChoiceCard();
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
            Text("sheet-text", card, "Den gamla bidragsdelen sparas krypterad på servern i 90 dagar och raderas sedan.");
            Button("button button-wide button-primary", card, "Jag förstår", () =>
            {
                _noticeCard = null;
                _middle.Remove(card);
                cloud.AcknowledgeKeyEpoch();
            });
        }
    }
}
