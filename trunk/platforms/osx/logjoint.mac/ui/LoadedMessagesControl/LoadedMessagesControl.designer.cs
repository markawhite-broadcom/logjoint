// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LogJoint.UI
{
	[Register ("LoadedMessagesControlAdapter")]
	partial class LoadedMessagesControlAdapter
	{
		[Outlet]
		AppKit.NSPopUpButton coloringButton { get; set; }

		[Outlet]
		AppKit.NSTextField coloringLabel { get; set; }

		[Outlet]
		AppKit.NSView logViewerPlaceholder { get; set; }

		[Outlet]
		AppKit.NSProgressIndicator navigationProgressIndicator { get; set; }

		[Outlet]
		AppKit.NSButton rawViewButton { get; set; }

		[Outlet]
		AppKit.NSButton toggleBookmarkButton { get; set; }

		[Outlet]
		AppKit.NSButton viewTailButton { get; set; }

		[Action ("OnColoringButtonClicked:")]
		partial void OnColoringButtonClicked (Foundation.NSObject sender);

		[Action ("OnRawViewButtonClicked:")]
		partial void OnRawViewButtonClicked (Foundation.NSObject sender);

		[Action ("OnToggleBookmarkButtonClicked:")]
		partial void OnToggleBookmarkButtonClicked (Foundation.NSObject sender);

		[Action ("OnViewTailButtonClicked:")]
		partial void OnViewTailButtonClicked (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (coloringButton != null) {
				coloringButton.Dispose ();
				coloringButton = null;
			}

			if (logViewerPlaceholder != null) {
				logViewerPlaceholder.Dispose ();
				logViewerPlaceholder = null;
			}

			if (navigationProgressIndicator != null) {
				navigationProgressIndicator.Dispose ();
				navigationProgressIndicator = null;
			}

			if (rawViewButton != null) {
				rawViewButton.Dispose ();
				rawViewButton = null;
			}

			if (toggleBookmarkButton != null) {
				toggleBookmarkButton.Dispose ();
				toggleBookmarkButton = null;
			}

			if (viewTailButton != null) {
				viewTailButton.Dispose ();
				viewTailButton = null;
			}

			if (coloringLabel != null) {
				coloringLabel.Dispose ();
				coloringLabel = null;
			}
		}
	}

	[Register ("LoadedMessagesControl")]
	partial class LoadedMessagesControl
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
