# Extractable Components

## MainHubShell
- Source: Views/MainHubPage.xaml
- Category: layout
- Description: panorama shell with stable-width sections and compact AppBar.
- Props: activeSection, showBanner, bannerText, isSearching, searchQuery.

## SecondaryPageHeader
- Source: shared pattern in ChatPage, ContactsPage, MomentsPage, SettingsPage.
- Category: layout
- Description: left-aligned title with optional subtitle/status and hardware Back.
- Props: title, subtitle, showStatus.

## AvatarView
- Source: Controls/AvatarView.xaml
- Category: basic
- Props: displayName, imageUrl, accentColor, size.

## SessionRow
- Source: MainHubPage session item template.
- Category: basic
- Props: displayName, preview, time, unreadCount, muted, pinned.

BottomTabBar is excluded because Hub is the WP8.1 navigation model.
