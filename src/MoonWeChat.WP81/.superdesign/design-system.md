# MoonWeChat WP8.1 Design System

## Product
MoonWeChat is a thin Windows Phone client for a desktop WeChat HTTP gateway. It targets Lumia 920T and 950 XL at 480 logical pixels wide. Jobs: scan conversations, find contacts, send/retry messages, see connection health, and configure the gateway without ambiguity.

## Information architecture
Root is a WP8.1 panorama Hub with four equal 352px sections: 微信, 通讯录, 发现, 我. The next section edge is stable. Live query filters conversations and contacts. Secondary pages use hardware Back and bottom AppBar. Welcome -> Connect -> Login is progressive remote setup; sample mode enters the same Hub.

## Visual language
Default WP Classic is Metro: black field, high contrast white type, teal #00ABA9 action color, square imagery and flat separators. No rounded cards, gradients, shadows, acrylic, ornamental illustration, or bottom-tab imitation. Win10 is a restrained light alternate with white/gray surfaces and #07C160 green.

Use Segoe WP/Segoe UI only. Titles are left-aligned and large. Body is compact and scan-first. All type comes from semantic resources; never embed literal sizes in page XAML. Touch targets are at least 48x48. Commands belong in the AppBar.

## Layout
- Viewport: 480x800 and 480x853.
- Safe leading inset: 19px.
- Hub sections: fixed 352px width.
- Lists: edge-to-edge, 68-76px session rows, 56-64px contact rows.
- AppBar reserve: 72px bottom Padding on every scrollable AppBar page.
- Composer: fixed bottom; message viewport shrinks by InputPane occlusion; root visual never translates.
- Use Margin for gaps.

## Feedback
Connection health is a compact tappable banner. Busy state disables triggering command and shows status. Every async failure is visible through inline status or MessageDialog. File/image picking reports cancellation, unsupported type, oversize, read failure and send failure. Destructive local-message clearing is absent. Search updates source collections without Clear plus Add rebuilds.

## Page direction
- Welcome: brand signal first, two clear paths, thin-client explanation.
- Connect: backend selector, URL, normalized endpoint hint, test/continue.
- Login: QR primary, visible status, advanced token/proxy collapsed.
- MainHub: dense panorama, live search, connection banner, stable reveal.
- Chat: large left title, no drawn Back, compact messages, quote/status bars, resilient composer.
- Contacts: live query, incremental alignment, alphabetical scan.
- Moments: feed dominates; AppBar refresh/publish.
- Settings: grouped unframed sections, save/test/session commands, code-initialized theme radios.
- Placeholder: honest title and status, scroll reserve.

## Hard constraints
WP8.1 WinRT, C# 7.3. No RelativePanel, NavigationView, acrylic, CornerRadius theme assumptions, ContentDialog, SystemNavigationManager, modern spacing properties, or awaited picker. Use HardwareButtons, InputPane, MessageDialog and continuation activation. ToggleButton.IsChecked is never set in XAML. Theme applies only after Window.Current.Activate().

Use ONLY fonts, colors, spacing and component styles defined here and in init/theme.md. Do not introduce unlisted fonts, colors, rounded visual language, gradients or decorative styles.
