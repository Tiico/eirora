using System.Linq;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The account sheet (ADR-0008): signing in with Apple or Google, linking the other and removing it, signing out and deleting the
    /// account. Reachable from the bar at any time, also during rest (RST-07), and never needed to play (ACC-04).
    /// </summary>
    public sealed partial class Hud
    {
        private AccountSession? _account;
        private bool _confirmingDeletion;

        /// <summary>Offers the account, or stops offering it with null.</summary>
        public void UseAccount(AccountSession? account)
        {
            if (_account is not null)
            {
                _account.Changed -= OnAccountChanged;
            }

            _account = account;
            if (account is not null)
            {
                account.Changed += OnAccountChanged;
            }

            if (_world is not null)
            {
                ShowBar(_world);
            }
        }

        public void OpenAccount()
        {
            Open(Sheet.Account);
            if (_open == Sheet.Account && _account is { IsSignedIn: true } account)
            {
                Run(account.LoadAsync(), done: null);
            }
        }

        /// <summary>Opens the confirmation before the account is deleted.</summary>
        public void OpenAccountDeletion()
        {
            if (_open != Sheet.Account)
            {
                OpenAccount();
            }

            _confirmingDeletion = true;
            ShowSheet();
        }

        private void OnAccountChanged()
        {
            if (_open == Sheet.Account)
            {
                ShowSheet();
            }
        }

        private void AccountSheet()
        {
            if (_account is not { } account)
            {
                return;
            }

            if (_confirmingDeletion && account.IsSignedIn)
            {
                AccountDeletion(account);
                return;
            }

            if (_showingRecoveryCode && account.IsSignedIn && _cloud?.RecoveryCode is { } shown)
            {
                RecoveryCodeSheet(shown);
                return;
            }

            if (_sendingKey && account.IsSignedIn && _cloud is { RecoveryCode: not null } sending && _scanner is { } scanner)
            {
                KeySendingSheet(sending, scanner);
                return;
            }

            _sendingKey = false;

            Text("sheet-title", _sheet, "Konto");
            if (!account.IsSignedIn)
            {
                Text("sheet-subtitle", _sheet, "Du spelar utan konto");
                Text("sheet-text", _sheet, "Du behöver inget konto för att spela, och din värld finns alltid kvar på telefonen. Med ett konto sparas den också på kontot och följer med till en ny telefon.");
                Text("sheet-text", _sheet, "Eirora sparar inget namn och ingen e-post, bara att du loggat in med Apple eller Google.");
                foreach (var provider in account.Providers)
                {
                    AddAction($"Logga in med {Texts.Provider(provider)}", "Kontot skapas första gången", !account.IsBusy, () => Run(account.SignInAsync(provider), "Du är inloggad."));
                }

                return;
            }

            var linked = account.Account?.Logins.Select(l => l.Provider).ToList();
            Text("sheet-subtitle", _sheet, linked is null
                ? "Du är inloggad"
                : "Inloggad med " + string.Join(" och ", linked.Select(Texts.Provider)));
            Text("sheet-text", _sheet, "Kontot och allt du gör här fungerar även när byn vilar. Din värld på telefonen påverkas aldrig.");
            if (CloudStatusLine() is { } cloud)
            {
                Text("sheet-text cloud-status", _sheet, cloud);
            }

            if (linked is not null)
            {
                var unlinked = account.Providers.Where(p => !linked.Contains(p)).ToList();
                if (unlinked.Count > 0 || linked.Count > 1)
                {
                    // Linking and removing need a fresh sign-in with a login of the account, so that a stolen token cannot (threat model).
                    Text("sheet-text", _sheet, "Du bekräftar en ändring av inloggningarna genom att logga in en gång till med en av kontots inloggningar.");
                }

                foreach (var provider in unlinked)
                {
                    string? confirm = account.ConfirmationProvider;
                    AddAction(
                        $"Koppla {Texts.Provider(provider)}",
                        confirm is null
                            ? "Telefonen kan inte logga in med kontots inloggning"
                            : $"Bekräfta med {Texts.Provider(confirm)}, logga sedan in med {Texts.Provider(provider)}",
                        !account.IsBusy && confirm is not null,
                        () => Run(account.LinkAsync(provider), "Inloggningen är kopplad."));
                }

                if (linked.Count > 1)
                {
                    foreach (var provider in linked)
                    {
                        string other = Texts.Provider(linked.First(p => p != provider));
                        string? confirm = account.RemovalProvider(provider);
                        AddAction(
                            $"Ta bort {Texts.Provider(provider)}-inloggningen",
                            confirm is null
                                ? "Telefonen kan inte logga in med kontots inloggning"
                                : $"Bekräfta med {Texts.Provider(confirm)}. Kontot nås sedan bara med {other}",
                            !account.IsBusy && confirm is not null,
                            () => Run(account.RemoveLoginAsync(provider), "Inloggningen är borttagen."));
                    }
                }
            }

            if (_cloud?.RecoveryCode is not null)
            {
                if (_cloud.KeyReach == KeyBackupReach.ThisPhone)
                {
                    // Without a screen lock neither iCloud Keychain nor Block Store carries the key on (ADR-0008).
                    Text("sheet-text", _sheet, "Telefonen har inget skärmlås, så nyckeln till din egenvård följer inte med till en ny telefon av sig själv. Skriv ner återställningskoden, eller skicka nyckeln med QR-koden när du byter telefon.");
                }

                AddAction("Skicka nyckeln till en ny telefon", _scanner is null ? "Den här telefonen kan inte skanna koden" : "Skanna QR-koden som den nya telefonen visar", _scanner is not null, OpenKeySending);
                AddAction("Visa återställningskod", "Nyckeln som låser upp din egenvård på en ny telefon", true, OpenRecoveryCode);
            }

            AddAction("Logga ut", "Din värld finns kvar på telefonen", !account.IsBusy, () => Run(account.SignOutAsync(), "Du är utloggad."));
            AddAction("Radera kontot", "Allt som servern sparat om dig tas bort", !account.IsBusy, () =>
            {
                _confirmingDeletion = true;
                ShowSheet();
            });
        }

        /// <summary>Deleting needs a fresh sign-in, so that only the player can do it (threat model).</summary>
        private void AccountDeletion(AccountSession account)
        {
            Text("sheet-title", _sheet, "Radera kontot?");
            Text("sheet-text", _sheet, "Kontot, dess inloggningar och allt servern sparat om dig tas bort för gott. Det går inte att ångra.");
            Text("sheet-text", _sheet, "Din värld på telefonen påverkas inte, och du kan fortsätta spela utan konto.");
            string? provider = account.DeletionProvider;
            if (account.Account is null)
            {
                Text("sheet-text", _sheet, "Kontot går inte att nå just nu. Försök igen när telefonen är uppkopplad.");
            }
            else if (provider is not null)
            {
                Text("sheet-text", _sheet, $"Bekräfta genom att logga in med {Texts.Provider(provider)} en gång till.");
            }
            else
            {
                Text("sheet-text", _sheet, "Kontot har en Apple-inloggning, och den här telefonen kan inte logga in med Apple. Radera kontot på webbsidan i stället:");
                Text("sheet-text link", _sheet, account.DeletionPage);
            }

            var choices = Container("bar", _sheet);
            if (account.Account is not null && provider is not null)
            {
                Button("button button-primary", choices, $"Radera med {Texts.Provider(provider)}", () => Run(account.DeleteAsync(), done: null))
                    .SetEnabled(!account.IsBusy);
            }

            Button("button", choices, "Avbryt", () =>
            {
                _confirmingDeletion = false;
                ShowSheet();
            });
        }

        /// <summary>Runs an account action and says how it went; the sheet follows the account's changes.</summary>
        private async void Run(Task<AccountOutcome> action, string? done)
        {
            ShowSheet(); // Shows the actions as busy.
            var outcome = await action;
            if (outcome == AccountOutcome.Deleted)
            {
                _confirmingDeletion = false;
            }

            string? message = outcome == AccountOutcome.Done ? done : Texts.Account(outcome);
            if (message is not null)
            {
                Toast(message);
            }

            if (_open == Sheet.Account)
            {
                ShowSheet();
            }
        }

        /// <summary>A person, head and shoulders, for the account button.</summary>
        private sealed class AccountIcon : VisualElement
        {
            public AccountIcon(float size = 54f)
            {
                style.width = size;
                style.height = size;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float w = contentRect.width, h = contentRect.height;
                p.fillColor = new Color(43 / 255f, 42 / 255f, 51 / 255f);
                p.BeginPath();
                p.Arc(new Vector2(w * 0.5f, h * 0.3f), w * 0.2f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.ClosePath();
                p.Fill();
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.1f, h * 0.96f));
                p.Arc(new Vector2(w * 0.5f, h * 0.96f), w * 0.4f, Angle.Degrees(180f), Angle.Degrees(360f));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
