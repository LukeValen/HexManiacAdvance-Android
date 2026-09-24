using Android.Content;
using Android.Views;
using Android.Widget;

namespace HexManiac.Mobile;

public sealed class SafeAreaScrollView : ScrollView {
   public SafeAreaScrollView(Context context) : base(context) { }

   public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets) {
      if (insets == null) return insets;

      if (OperatingSystem.IsAndroidVersionAtLeast(30)) {
         var safe = insets.GetInsets(
            WindowInsets.Type.SystemBars() |
            WindowInsets.Type.DisplayCutout() |
            WindowInsets.Type.Ime());
         SetPadding(safe.Left, safe.Top, safe.Right, safe.Bottom);
      } else {
         SetPadding(
            insets.SystemWindowInsetLeft,
            insets.SystemWindowInsetTop,
            insets.SystemWindowInsetRight,
            insets.SystemWindowInsetBottom);
      }

      return insets;
   }
}
