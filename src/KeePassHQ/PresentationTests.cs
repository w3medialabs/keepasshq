#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace VaultOrganizer
{
    internal sealed class SyntheticInputSource : PresentationSource
    {
        public override Visual RootVisual { get; set; }
        public override bool IsDisposed { get { return false; } }
        protected override CompositionTarget GetCompositionTargetCore() { return null; }
    }
    internal sealed partial class OrganizerWindow
    {
        private static void PressEnter(UIElement target)
        { target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new SyntheticInputSource(), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent }); }
        private static void LayoutSynthetic(FrameworkElement root, double width, double height)
        { root.Width=width;root.Height=height;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout(); }
        private static void DragScrollbar(ScrollBar bar,double x,double y)
        {
            var thumb=bar.Track.Thumb;thumb.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});
            thumb.RaiseEvent(new DragDeltaEventArgs(x,y){RoutedEvent=Thumb.DragDeltaEvent});
            thumb.RaiseEvent(new DragCompletedEventArgs(x,y,false){RoutedEvent=Thumb.DragCompletedEvent});
        }
        internal async Task TestUiCompleteness(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose=true; var fake=new IsolatedClipboard();clipboard=fake;
            var field=Descendants<SecretInput>(unlock).Single();
            password.Password="fictional-exit-key";field.SetRevealed(true);fake.Write("fictional-copied-key");OwnClipboard(fake.Value);
            DatabaseDialog(null,true);var secret=Descendants<SecretInput>(veil).First();secret.Masked.Password="fictional-modal-key";secret.SetRevealed(true);
            var close=new CancelEventArgs();OnClosing(this,close);
            check(!close.Cancel&&fake.Value==null&&clipboardHash==null,"Closing before database unlock clears app-owned clipboard text");
            check(field.Revealed.Text.Length==0&&password.Password.Length==0&&secret.Revealed.Text.Length==0&&secret.Masked.Password.Length==0,"Closing before database unlock clears both open-screen and modal factor representations");
            fake.Write("fictional-owned");OwnClipboard(fake.Value);fake.Value="fictional-replacement";OnClosing(this,new CancelEventArgs());
            check(fake.Value=="fictional-replacement"&&clipboardHash==null,"Empty-session exit preserves clipboard text replaced by another application");
            Directory.CreateDirectory(directory);selectedPath=Tests.CreateFixture(directory,false);password.Password="demo";field.SetRevealed(true);PressEnter(field.Revealed);
            await WaitFor(()=>!opening);
            check(session!=null&&!locked,"Enter from the revealed open-screen field performs the same unlock as the masked field");
            SwitchMode(true);var imports=session.Database.RootGroup.Groups.First(group=>group.Name=="Imported notes");
            foreach(var pane in panes){pane.Path.Add(imports);pane.Expanded=imports.Entries.First().Uuid.ToHexString();}RenderAll();
            LayoutSynthetic((FrameworkElement)Content,980,640);await Task.Delay(20);((FrameworkElement)Content).UpdateLayout();
            var horizontal=(ScrollBar)panes[0].Scroll.Template.FindName("PART_HorizontalScrollBar",panes[0].Scroll);
            DragScrollbar(horizontal,45,0);
            await Task.Delay(20);((FrameworkElement)Content).UpdateLayout();
            check(panes[0].Scroll.HorizontalOffset>0&&Math.Abs(horizontal.Value-panes[0].Scroll.HorizontalOffset)<0.1,"Styled horizontal scrollbar routes thumb movement to the browser viewport (offset="+panes[0].Scroll.HorizontalOffset+", range="+panes[0].Scroll.ScrollableWidth+", bar="+horizontal.Value+")");
            var column=panes[0].Columns.Children.OfType<Grid>().Last();var vertical=column.Children.OfType<ScrollViewer>().Single();
            var bar=(ScrollBar)vertical.Template.FindName("PART_VerticalScrollBar",vertical);
            DragScrollbar(bar,0,45);
            await Task.Delay(20);((FrameworkElement)Content).UpdateLayout();
            check(vertical.VerticalOffset>0&&bar.ActualWidth<=7.1&&horizontal.ActualHeight<=7.1,"Styled vertical scrollbar scrolls expanded cards and both scrollbar dimensions remain narrow");
            double previousOffset=vertical.VerticalOffset;ScrollBar.PageUpCommand.Execute(null,bar);await Task.Delay(20);((FrameworkElement)Content).UpdateLayout();
            check(vertical.VerticalOffset<previousOffset,"Styled scrollbar page commands remain routed to the owning viewport");
            ShowSettings();LayoutSynthetic((FrameworkElement)Content,980,640);
            var combo=Descendants<ComboBox>(veil).First();combo.ApplyTemplate();
            var popup=(Popup)combo.Template.FindName("PART_Popup",combo);var options=(FrameworkElement)popup.Child;
            options.Measure(new Size(combo.ActualWidth,Double.PositiveInfinity));options.Arrange(new Rect(new Point(),options.DesiredSize));options.UpdateLayout();
            var option=combo.ItemContainerGenerator.ContainerFromIndex(0) as ComboBoxItem;
            check(option!=null,"Styled dropdown generates real option containers within its popup template");
            option.IsSelected=true;
            check((int)combo.SelectedItem==15&&!String.IsNullOrWhiteSpace(Convert.ToString(option.ToolTip)),"Styled dropdown options select the underlying setting and have descriptive help");
            CloseModal();await CloseDatabases();
            DatabaseDialog(null,false);Descendants<TextBox>(veil).First().Text=selectedPath;
            var openField=Descendants<SecretInput>(veil).Single();openField.Masked.Password="demo";openField.SetRevealed(true);PressEnter(openField.Revealed);await WaitFor(()=>!opening);
            check(session!=null&&!locked&&openField.Masked.Password.Length==0&&openField.Revealed.Text.Length==0,"Enter submits the revealed open-database modal and clears its factor");
            await CloseDatabases();DatabaseDialog(null,true);var fields=Descendants<SecretInput>(veil).ToArray();
            string created=Path.Combine(directory,"keyboard-created.kdbx");Descendants<TextBox>(veil).First().Text=created;
            fields[0].Masked.Password="fictional-confirmed-key";fields[1].Masked.Password="mismatch";fields[1].SetRevealed(true);PressEnter(fields[1].Revealed);
            check(!File.Exists(created)&&veil.Visibility==Visibility.Visible,"Enter in revealed confirmation retains no-overwrite/password-confirmation validation");
            fields[1].Revealed.Text="fictional-confirmed-key";PressEnter(fields[1].Revealed);await WaitFor(()=>!opening);
            check(session!=null&&File.Exists(created)&&fields.All(input=>input.Masked.Password.Length==0&&input.Revealed.Text.Length==0),"Enter creates the confirmed fictional database and clears both factor controls");
            await CloseDatabases();StopTimers();SystemEvents.SessionSwitch-=OnSystemSession;
        }
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
