# Theme Tokens

## Compact summary
Default WP Classic: black surfaces, white primary text, translucent white secondary text, teal #00ABA9 accent, red unread/error, amber connection banner. Win10 is a restrained light option with white/gray surfaces, near-black text, and #07C160 green.

System Segoe WP/Segoe UI is used through semantic styles. Scale: Hero 42, PageTitle 34, Section 26, ListTitle 18, Body 15, Caption 13, Micro 11. No page uses literal font sizes.

Layout scale: 4, 8, 12, 16, 19, 24, 32. Touch targets at least 48x48. Cards are square and flat. Hub section width 352. AppBar reserve 72.

Required brushes: AccentBrush, PrimaryTextBrush, SecondaryTextBrush, WarningTextBrush, ChromeBackgroundBrush, ListBackgroundBrush, CardBackgroundBrush, CardBorderBrush, DividerBrush, InputBarBackgroundBrush, InputBoxBackgroundBrush, MineBubbleBrush, TheirBubbleBrush, OnMineBubbleTextBrush, UnreadBadgeBrush, PillBackgroundBrush, ChatWallpaperBrush, BannerBackgroundBrush, BannerBorderBrush, BannerTextBrush, SelectionBrush, OverlayBrush, SuccessTextBrush.

Required sizes: ChromeBarHeight, PageTitleFontSize, HeroFontSize, SectionTitleFontSize, ListTitleFontSize, BodyFontSize, CaptionFontSize, MicroFontSize, TouchTargetSize, AppBarReserve, HubSectionWidth.

Platform: WP8.1 WinRT only. No acrylic, RelativePanel, NavigationView, Spacing, ColumnSpacing, ContentDialog or SystemNavigationManager. Use Grid, Hub, ListView, CommandBar, MessageDialog, InputPane, HardwareButtons and picker continuation.
