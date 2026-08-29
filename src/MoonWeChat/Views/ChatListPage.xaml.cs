using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MoonWeChat.Models;
using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ChatListPage : Page
    {
        public ChatListViewModel ViewModel { get; } = new ChatListViewModel();
        public ObservableCollection<ChatSession> VisibleSessions { get; } = new ObservableCollection<ChatSession>();
        public ObservableCollection<Contact> VisibleContacts { get; } = new ObservableCollection<Contact>();

        public ChatListPage() { InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Reload();
            ApplyFilter();
            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn) _ = RefreshSafeAsync();
        }

        private async System.Threading.Tasks.Task RefreshSafeAsync()
        {
            try { await ViewModel.AutoRefreshRemoteAsync(); ApplyFilter(); }
            catch (Exception ex) { ViewModel.BannerText = "刷新失败：" + ex.Message; }
        }

        private void ApplyFilter()
        {
            var query = SearchBox == null ? string.Empty : SearchBox.Text;
            var sessions = new List<ChatSession>();
            foreach (var session in ViewModel.Sessions)
            {
                if (string.IsNullOrWhiteSpace(query) || Contains(session.DisplayName, query) || Contains(session.PreviewText, query)) sessions.Add(session);
            }
            Align(VisibleSessions, sessions);

            var contacts = new List<Contact>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var contact in AppServices.Data.GetContacts())
                {
                    if (Contains(contact.DisplayName, query) || Contains(contact.WxId, query)) contacts.Add(contact);
                }
            }
            Align(VisibleContacts, contacts);
            if (ContactResults != null) ContactResults.Visibility = contacts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool Contains(string value, string query) { return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static void Align<T>(ObservableCollection<T> target, IList<T> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (i < target.Count && ReferenceEquals(target[i], source[i])) continue;
                var existing = target.IndexOf(source[i]);
                if (existing >= 0) target.Move(existing, i); else target.Insert(i, source[i]);
            }
            while (target.Count > source.Count) target.RemoveAt(target.Count - 1);
        }

        private void OnSearchClick(object sender, RoutedEventArgs e) { SearchBox.Visibility = SearchBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (SearchBox.Visibility == Visibility.Visible) SearchBox.Focus(FocusState.Programmatic); else { SearchBox.Text = string.Empty; ApplyFilter(); } }
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) { ApplyFilter(); }
        private void OnBannerTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e) { Frame.Navigate(typeof(LoginPage)); }
        private void OnSessionClicked(object sender, ItemClickEventArgs e) { var session = e.ClickedItem as ChatSession; if (session != null) Frame.Navigate(typeof(ChatPage), session.Id); }
        private void OnContactClicked(object sender, ItemClickEventArgs e) { var contact = e.ClickedItem as Contact; if (contact != null) Frame.Navigate(typeof(ChatPage), contact.WxId); }
        private void OnSettingsClick(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(SettingsPage)); }
        private void OnLoginClick(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(LoginPage)); }
        private void OnConnectClick(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(ConnectPage)); }
        private async void OnRefreshClick(object sender, RoutedEventArgs e) { try { await ViewModel.RefreshRemoteAsync(); ApplyFilter(); } catch (Exception ex) { ViewModel.BannerText = "刷新失败：" + ex.Message; } }
    }
}
