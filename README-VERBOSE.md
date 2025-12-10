# 📸 **EstlCameo**

### *Snapshot Undo & Timeline Viewer for Estlcam*

#### *A tiny companion app by the community, for the community.*

EstlCameo is a lightweight Windows tray utility that gives **https://www.estlcam.de/** users something they’ve wanted for years:

> **Automatic snapshots + timeline browsing + safe restore**
> …without modifying Estlcam itself.

EstlCameo quietly runs in the background, listens for `Ctrl+S` inside Estlcam, and creates timestamped backup copies of your `.E12` project. Think of it as **“Time Machine for Estlcam projects.”**

✔ No plugins
✔ No Estlcam mods
✔ No risky patches
✔ 100% optional & non-intrusive

EstlCameo simply watches your saved `.E12` file, captures versioned snapshots, and gives you a fast UI to explore and restore them.

---

## ⭐ **What EstlCameo Does**

### 📸 **1. Automatic Snapshots on Save (`Ctrl+S`)**

Whenever you press **Ctrl+S inside Estlcam**, EstlCameo:

* Detects the save
* Creates a timestamped snapshot of the `.E12` file
* Captures a small preview screenshot of the Estlcam window
* Shows a neat toast notification with the preview image

Snapshots are stored next to your project:

```
<your project folder>\
    .snapshots\
        <projectName>\
            20251210_093455.e12
            20251210_093455.png
            20251210_093732.e12
            20251210_093732.png
            ...
```

This is entirely **non-destructive**. Your original `.E12` is never touched.

---

### 🖼 **2. Timeline Viewer (`Ctrl+R`)**

A large full-screen viewer shows:

* Tiled snapshots (S / M / L sizes)
* Zoomable preview
* Metadata + relative timestamps
* Timeline scrubber
* Quick search by time or order

From here you can:

#### ✔ Restore a snapshot **as a copy**

You are never forced to overwrite your project — restored files look like:

```
myfile_restored_20251205_091230.e12
myfile_restored_20251205_091230_1.e12
```

The restored file is immediately opened in a **new Estlcam instance**.
(Implementation from `RestoreSnapshotAsCopy()` and `EstlcamInterop.OpenFileInNewInstance()`)

✔ Zero risk
✔ Original file stays untouched

---

### ~~♻️ **3. Mini Undo/Redo (Ctrl+Z / Ctrl+Y inside Estlcam)**~~

**NOT IMPLEMENTED:**  Need some changes to EstlCam to enable undo/redo features (e.g. Ctrl + O implemented), details in https://forum.v1e.com/t/no-undo/52182/49

~~EstlCameo intercepts hotkeys **only when Estlcam is foreground**
(verified in `KeyboardHook` + `EstlcamInterop.IsEstlcamForeground()`) and provides:~~

* ~~`Ctrl+Z` → Go to previous snapshot~~
* ~~`Ctrl+Y` → Go to next snapshot~~

**Important:** EstlCameo *never swallows `Ctrl+S`* — Estlcam always receives the save command.

---

### 🔔 **4. Toast Notifications**

Every snapshot shows a bottom-right toast with:

* Multi-line message
* Optional thumbnail preview (scaled from screenshot)
* Optional file link
* Auto-fade

Implementation in `ToastForm` ensures:

* No focus stealing (`WS_EX_NOACTIVATE`)
* Dynamic height based on message + preview
* Preview collapses cleanly when not present

---

### 🛠 **5. Smart Project Detection**

When Estlcam is foreground, EstlCameo tries to determine the active `.E12` file using:

* Estlcam window title parsing (via `ExtractFileNameFromCaption()`)
* Estlcam process enumeration
* Automatic resolution from **State CAM.txt**
  (parsing implemented in `StateCamResolver`)

If a project can’t be determined:

* A branded **Project Resolution dialog** appears
* You choose the `.E12` file once
* Snapshots begin immediately

If you switch projects in Estlcam, EstlCameo politely warns you and pauses snapshotting to avoid mixing project histories.

---

## ✔️ **Who This Is For**

* Estlcam users generating toolpaths for CNC machines
* Anyone who wants to experiment safely
* Anyone nervous about losing work
* V1E / Maslow / hobby CNC builders

If you've ever said:

> “I wish Estlcam had Undo…”

This is for you.

---

# 🚀 Getting Started

## **1. Download**

> Grab the latest release from the GitHub Releases page.
> (Every tag builds automatically via GitHub Actions.)

---

## **2. Run `EstlCameo.exe`**

You’ll see a tray icon appear:

* Right-click → Snapshot Viewer, Open Log Folder, Exit
* Press **Ctrl+S in Estlcam** → snapshots begin
* Press **Ctrl+R** → open timeline viewer

You don’t need to configure anything.

---

## **3. That’s it. Enjoy snapshot-powered confidence.**

---

# 🧩 Architecture (Hybrid Developer Section)

EstlCameo is intentionally small and auditable.
Architecture components:

---

## **📦 1. SnapshotManager**

()

* Tracks active `.E12` file
* Creates snapshot copies with timestamp naming
* Debounces repeated saves
* Owns the snapshot directory
* Takes preview screenshots
* Fires toasts
* Provides Undo/Redo navigation
* Provides safe “restore as copy” behavior

Snapshot creation is retry-friendly (10 attempts × 200ms) to avoid file-locking issues from Estlcam.

---

## **⌨️ 2. KeyboardHook**

()

* System-level WH_KEYBOARD_LL hook
* Only swallows Z/Y/R when:

  * Ctrl is held
  * Estlcam is foreground
* Always passes hotkeys to other apps
* Never blocks `Ctrl+S`

---

## **🔍 3. StateCamResolver**

()

* Parses Estlcam’s internal `State CAM.txt`
* Extracts MRU `.E12` list
* Extracts `Dir Projects=`
* Resolves filenames from window caption to full paths

This dramatically improves first-time auto-detection.

---

## **🪟 4. SnapshotViewerForm**

()

* S/M/L tile sizes
* Timeline scrubber
* Auto-aspect-ratio thumbnails
* Metadata panel
* Safe restore button

---

## **🔔 5. ToastForm**

()

* Multi-line messages
* Collapsible preview image
* No-focus-steal window
* Fade-out animations

---

## **🤝 6. TrayForm**

()

* Owns the keyboard hook
* Owns the SnapshotManager
* Hosts the tray icon
* Routes commands
* Handles first-attach and project switching UX
* Provides log folder shortcut

---

## **🪟 7. EstlcamInterop**

()

* Detects Estlcam window/process
* Extracts project filename from window title
* Opens restored files in new Estlcam instances
* Re-open support (Ctrl+O path planned)

---

# 🧱 Build Instructions (Developers)

Requirements:

* Windows
* .NET 8 SDK
* Visual Studio or `dotnet` CLI

Build:

```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -o publish
```

---

# 🤝 Contributing

PRs are welcome — especially:

* Bug fixes
* Snapshot safety improvements
* UI polish
* Better project auto-detection
* Documentation

Large feature requests will likely be deferred.

---

# 🛠 Maintenance Expectations

EstlCameo is being released **as-is** as we shift primary focus to the upcoming **Maker Galaxy Expert Support System**.

We will:

* Fix major bugs
* Review lightweight PRs
* Keep compatibility with Estlcam when possible

We will **not** be rapidly expanding feature scope at this time.


---


# ❤️ Donations (Optional)

EstlCameo is free and will remain free.
If you find it useful and want to support continued work on community tooling:

> **Donations are appreciated but absolutely not expected.**

Supporting Estlcam’s developer and the CNC communities is always the priority.

Thank you!  (https://github.com/sponsors/aaronse https://buymeacoffee.com/azab2c patreon.com/azab2c)

---


# 🐾 Credits

Created by **AzaB2C** with input and feedback from the Estlcam & V1 Engineering communities.

Special thanks to:

* Christian (Estlcam author)
* V1E, Maslow & hobby CNC builders
* Testers supporting early builds

 
---


## License
This project is licensed under the Creative Commons Attribution–NonCommercial 4.0 International License (CC BY-NC 4.0).

You may use, share, and modify the software for personal and non-commercial purposes.  
Commercial use is not permitted without written permission.

See the full LICENSE file for details.
