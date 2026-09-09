# Routes

- Launch: `AppNavigation.ResolveLaunchPage()` -> `WelcomePage`, `LoginPage`, or `ShellPage`.
- `ShellPage` hosts `ChatListPage`, `ContactsPage`, `MomentsPage`, and `SettingsPage`.
- `ChatPage` is pushed into the shell content frame with a session id parameter.
- `ConnectPage`, `LoginPage`, and `PlaceholderPage` are standalone wizard/detail pages.

