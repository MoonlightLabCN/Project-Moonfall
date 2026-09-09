# Shared Layouts

## Application shell
- App.xaml/App.xaml.cs own one root Frame.
- Hardware Back traverses the frame stack; no application-drawn back button.
- WpClassicTheme is statically merged. Persisted theme applies only after Window.Current.Activate().
- Status bar follows theme. File picker continuation is routed from App.OnActivated.

## MainHubPage
- WP8.1 panorama shell for conversations, contacts, discover, and profile.
- Every HubSection is 352 logical pixels on the 480px phone.
- Bottom AppBar contains refresh, search, and settings; scroll content reserves 72px.
- A page-level query filters session and contact collections live.

## Secondary pages
Use a 19px leading title inset, semantic page-title style, scroll/list region, visible status rows, and optional bottom AppBar. Hardware Back is the only back affordance. ChatPage keeps its composer in-layout and resizes the message viewport from InputPane occlusion.
