# Shared UI Components

Framework: Windows Phone 8.1 WinRT XAML with C# code-behind. No web framework or third-party component library.

## AvatarView
- Path: Controls/AvatarView.xaml
- Purpose: square contact/session avatar with remote image and deterministic initial fallback.
- Properties: DisplayName, AccentColorHex, ImageUrl, Size.

## Native control primitives
- IconButtonStyle: transparent icon command, minimum hit area 48x48.
- FlatListViewItemStyle: edge-to-edge recyclable list row.
- ChatInputTextBoxStyle: multiline message composer.
- SendButtonStyle: accent command.
- MorePanelButtonStyle: attachment grid command.
- Semantic text styles: page title, section title, list title, body, caption, microcopy.

BottomTabBar is excluded from the new component set. WP8.1 Hub navigation replaces it.
