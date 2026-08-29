using System;
using MoonWeChat.Models;
using MoonWeChat.ViewModels;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ChatListPage : Page
    {
        public ChatListViewModel ViewModel { get; } = new ChatListViewModel();
        public ChatListPage() { InitializeComponent(); DataContext = this; }
        protected override void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); ViewModel.Reload(); }
        private void OnItemClick(object sender, ItemClickEventArgs e) { var session = e.ClickedItem as ChatSession; if (session != null) Frame.Navigate(typeof(ChatPage), session.Id); }
        private async void OnRefreshClick(object sender, RoutedEventArgs e) { try { await ViewModel.RefreshRemoteAsync(); } catch (Exception ex) { await new MessageDialog("刷新失败：" + ex.Message).ShowAsync(); } }
    }
}
