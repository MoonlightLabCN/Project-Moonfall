using System;
using System.Collections.ObjectModel;
using System.Linq;
using MoonWeChat.Models;
using MoonWeChat.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
namespace MoonWeChat.Views
{
 public sealed partial class ContactsPage : Page
 {
  public ObservableCollection<Contact> Contacts { get; } = new ObservableCollection<Contact>();
  public ContactsPage() { InitializeComponent(); }
  protected override void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); ReloadLocal(); }
  private void ReloadLocal() { AlignContacts(AppServices.Data.GetContacts().OrderBy(c => c.PinyinIndex).ThenBy(c => c.DisplayName).ToList()); }
  private void AlignContacts(System.Collections.Generic.IList<Contact> ordered) { for (var i = 0; i < ordered.Count; i++) { if (i < Contacts.Count && ReferenceEquals(Contacts[i], ordered[i])) continue; var existing = Contacts.IndexOf(ordered[i]); if (existing >= 0) Contacts.Move(existing, i); else Contacts.Insert(i, ordered[i]); } while (Contacts.Count > ordered.Count) Contacts.RemoveAt(Contacts.Count - 1); }
  private async void OnSearchClick(object sender, RoutedEventArgs e) { try { var box = new TextBox { PlaceholderText = "搜索联系人" }; var dialog = new ContentDialog { Title = "搜索联系人", Content = box, PrimaryButtonText = "搜索", SecondaryButtonText = "取消" }; if (await dialog.ShowAsync() != ContentDialogResult.Primary) return; var q = box.Text == null ? string.Empty : box.Text.Trim(); var matches = AppServices.Data.GetContacts().Where(c => string.IsNullOrWhiteSpace(q) || (c.DisplayName ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (c.WxId ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(c => c.PinyinIndex).ThenBy(c => c.DisplayName).ToList(); AlignContacts(matches); } catch (Exception ex) { await ShowErrorAsync("搜索联系人失败：" + ex.Message); } }
  private static async System.Threading.Tasks.Task ShowErrorAsync(string message) { await new ContentDialog { Title = "提示", Content = message, CloseButtonText = "确定" }.ShowAsync(); }
 }
}
