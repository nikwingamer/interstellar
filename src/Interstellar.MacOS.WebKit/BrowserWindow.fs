﻿namespace Interstellar.MacOS.WebKit.Internal
open System
open AppKit
open CoreGraphics
open Foundation
open Interstellar
open Interstellar.MacOS.WebKit
open WebKit

type NiblessViewController(view: NSView) =
    inherit NSViewController()

    override this.LoadView () =
        base.View <- view

type BrowserWindow(config: BrowserWindowConfig<NSWindow>) as this =
    inherit NSWindowController("BrowserWindow")
    
    let browser = new Browser(config)

    let closedEvt = new Event<_>()
    let shownEvt = new Event<_>()
    let disposedEvt = new Event<_>()

    do
        let wkBrowserController = {
            new NiblessViewController(browser.WebKitBrowser) with
                // This KeyDown override prevents the macOS beep (error) sound on key presses
                override this.KeyDown (event: NSEvent) = ()
                override this.ViewDidAppear () =
                    base.ViewDidAppear ()
                    shownEvt.Trigger ()
        }
        this.Window <-
            new NSWindow(new CGRect (0., 0., 1000., 500.),
                         NSWindowStyle.Titled ||| NSWindowStyle.Closable ||| NSWindowStyle.Miniaturizable ||| NSWindowStyle.Resizable,
                         NSBackingStore.Buffered, false)
        this.Window.WillClose.Add (fun x -> closedEvt.Trigger ())
        this.Window.ContentView <- browser.WebKitBrowser
        this.Window.ContentViewController <- wkBrowserController
        this.Window.Center ()
        this.Window.AwakeFromNib ()
    
    member this.WKBrowserView = browser.WebKitBrowser
    member this.WKBrowser = browser

    override this.LoadWindow () =
        base.LoadWindow ()

    interface IBrowserWindow<NSWindow> with
        member this.Browser = upcast browser
        member this.Close () = (this :> NSWindowController).Close ()
        [<CLIEvent>]
        member val Closed = closedEvt.Publish
        member this.IsShowing =
            let w = (this :> NSWindowController).Window
            w.IsVisible || w.IsMiniaturized
        member this.Show () = async {
            (this :> NSWindowController).ShowWindow this
        }
        member this.NativeWindow = this.Window
        member this.Platform = BrowserWindowPlatform.MacOS
        [<CLIEvent>]
        member val Shown = shownEvt.Publish
        member this.Size
            with get () =
                let size = this.Window.Frame.Size
                float size.Width, float size.Height
            and set (width, height) =
                let oldFrame = this.Window.Frame
                // Cocoa uses a bottom-left origin, so we have to move the bottom-left corner in order to keep the top-right
                // corner in place
                let rect = new CGRect(float oldFrame.X, float oldFrame.Y - (height - float oldFrame.Height), width, height)
                this.Window.SetFrame (rect, true, true)
        member this.Title
            with get () = base.Window.Title
            and set x = base.Window.Title <- x