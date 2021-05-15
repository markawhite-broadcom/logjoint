﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;
using ObjCRuntime;

namespace LogJoint.UI
{
	public partial class MainWindow : AppKit.NSWindow
	{
		#region Constructors

		// Called when created from unmanaged code
		public MainWindow (IntPtr handle) : base (handle)
		{
			Initialize ();
		}
		
		// Called when created directly from a XIB file
		[Export ("initWithCoder:")]
		public MainWindow (NSCoder coder) : base (coder)
		{
			Initialize ();
		}
		
		// Shared initialization code
		void Initialize ()
		{
		}

		#endregion

		public void SetOwner(MainWindowAdapter owner)
		{
			this.owner = owner;
		}

		[Export("draggingEntered:")]
		NSDragOperation DraggingEntered(NSDraggingInfo info)
		{
			if (owner.DraggingEntered(info))
				return NSDragOperation.Copy;
			return NSDragOperation.None;
		}

		[Export("performDragOperation:")]
		bool PerformDragOperation(NSDraggingInfo info)
		{
			owner.PerformDragOperation(info);
			return true;
		}

		MainWindowAdapter owner;
	}
}

