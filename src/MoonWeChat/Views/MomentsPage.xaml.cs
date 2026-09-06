using System;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
namespace MoonWeChat.Views
{
 public sealed partial class MomentsPage : Page
 {
  public MomentsViewModel ViewModel { get; } = new MomentsViewModel();
  public MomentsPage() { InitializeComponent(); }
  protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); try { await ViewModel.LoadAsync(); } catch (Exception ex) { ViewModel.StatusText = "加载朋友圈失败：" + ex.Message; } }
  private async void OnRefreshClick(object sender, RoutedEventArgs e) { try { await ViewModel.LoadAsync(true); } catch (Exception ex) { ViewModel.StatusText = "刷新朋友圈失败：" + ex.Message; } }
  private async void OnPublishClick(object sender, RoutedEventArgs e) { try { ViewModel.Draft = DraftBox.Text; await ViewModel.PublishAsync(); DraftBox.Text = ViewModel.Draft ?? string.Empty; } catch (Exception ex) { ViewModel.StatusText = "发布失败：" + ex.Message; } }
  private void OnDraftChanged(object sender, TextChangedEventArgs e) { ViewModel.Draft = DraftBox.Text; }
 }
}
