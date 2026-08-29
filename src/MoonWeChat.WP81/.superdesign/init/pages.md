# Page Dependency Trees

## MainHubPage
- Views/MainHubPage.xaml and .xaml.cs
- Controls/AvatarView.xaml and .xaml.cs
- Styles/ControlStyles.xaml
- Styles/Themes/WpClassicTheme.xaml and Win10Theme.xaml
- Core ChatListViewModel, MomentsViewModel, ChatSession, Contact, AppServices, AppSettings

## ChatPage
- Views/ChatPage.xaml and .xaml.cs
- Controls/AvatarView.xaml
- Styles/MessageBubbleTemplates.xaml and ControlStyles.xaml
- Services/FilePickerContinuation.cs
- Core ChatViewModel, ChatMessage, MessageType, converters, template selector

## Welcome/Connect/Login
- Views/WelcomePage.xaml(.cs), ConnectPage.xaml(.cs), LoginPage.xaml(.cs)
- Services/AppNavigation.cs
- Core AppSettings, SettingsViewModel, LoginViewModel, SessionBootstrap, BackendKind

## Contacts/Moments/Settings/Placeholder
- Corresponding Views XAML and code-behind
- Controls/AvatarView where applicable
- Core Contacts snapshot, MomentsViewModel, SettingsViewModel, ThemeService

## Lifecycle
- App.xaml and App.xaml.cs
- Services/AppNavigation.cs and FilePickerContinuation.cs
- all theme/control/message dictionaries
