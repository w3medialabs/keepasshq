#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VaultOrganizer
{
    internal sealed partial class OrganizerWindow
    {
        internal async Task TestPasswordReveal(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose = true; var fake = new IsolatedClipboard { Value = "Fictional unrelated clipboard" }; clipboard = fake;
            var input = Descendants<SecretInput>(unlock).Single();
            password.Password = "fictional-key"; Press(input.Eye);
            check(input.IsRevealed && input.Revealed.Text == password.Password, "Password reveal requires an explicit eye action and mirrors the masked factor");
            input.Revealed.Text = "edited-fictional-key";
            check(password.Password == "edited-fictional-key", "Editing a revealed factor updates the actual unlock factor");
            Press(input.Eye);
            check(!input.IsRevealed && input.Revealed.Text.Length == 0 && password.Password == "edited-fictional-key", "Hiding removes the plaintext copy without changing the factor");
            Press(input.Eye); usePassword.IsChecked = false;
            check(password.Password.Length == 0 && input.Revealed.Text.Length == 0 && !input.IsRevealed && !input.Eye.IsEnabled, "Disabling the password factor clears both representations and disables reveal");
            usePassword.IsChecked = true; password.Password = "fictional-key"; Press(input.Eye); await Seal(false);
            check(password.Password.Length == 0 && input.Revealed.Text.Length == 0 && !input.IsRevealed, "Lock clears and hides the open-screen password even when it was revealed");
            DatabaseDialog(null, true); var confirmation = Descendants<SecretInput>(veil).ToArray();
            foreach (var field in confirmation) { field.Masked.Password = "fictional-confirmed-key"; field.SetRevealed(true); }
            CloseModal();
            check(confirmation.Length == 2 && confirmation.All(field => field.Masked.Password.Length == 0 && field.Revealed.Text.Length == 0 && !field.IsRevealed), "Closing creation clears both revealed password and confirmation controls");
            DatabaseDialog(null, false); var modal = Descendants<SecretInput>(veil).Single(); modal.Masked.Password = "fictional-modal-key"; modal.SetRevealed(true); await Seal(false);
            check(modal.Masked.Password.Length == 0 && modal.Revealed.Text.Length == 0 && !modal.IsRevealed, "Lock clears a revealed open-database dialog factor");
            Directory.CreateDirectory(directory); selectedPath = Tests.CreateFixture(directory, false); password.Password = "demo"; input.SetRevealed(true); await Open();
            check(session != null && password.Password.Length == 0 && input.Revealed.Text.Length == 0 && !input.IsRevealed, "Successful decryption clears the revealed factor and returns it to hidden mode");
            check(fake.Value == "Fictional unrelated clipboard", "Reveal, hide, close and decryption never copy a key to the clipboard");
            await CloseDatabases(); StopTimers(); SystemEvents.SessionSwitch -= OnSystemSession;
        }
    }
}
#endif
