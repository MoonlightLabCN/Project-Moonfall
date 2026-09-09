# Navigation Map

There are no URL routes. A native WinRT Frame navigates between page types.

- WelcomePage: first-launch choice between sample and remote setup.
- ConnectPage: backend kind, server URL, connectivity test.
- LoginPage: token, QR and login polling.
- MainHubPage: root panorama with 微信, 通讯录, 发现, 我.
- ChatListPage: functional compatibility session list and smoke-required XBF.
- ChatPage: message history, quote, retry and attachments.
- ContactsPage: full searchable directory.
- MomentsPage: timeline, publish, like and comment.
- SettingsPage: backend credentials, sample/live, theme and session.
- PlaceholderPage: explicit not-yet-implemented state.

AppNavigation.ResolveLaunchPage selects Welcome, Connect/Login or MainHub from onboarding/session state. HardwareButtons.BackPressed handles transient page state then Frame.GoBack().
