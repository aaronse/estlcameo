# EstlCameo Hotkey Requirements (v1)

This document defines the behavior contract for EstlCameo’s global hotkeys and project tracking.
It is intended to be precise enough for humans to understand and for future automated tests
(or LLMs) to be generated from.

---

## 0. Glossary

- **WorkingFilePath**  
  Absolute path to the Estlcam project file that EstlCameo is currently tracking
  for snapshots.

- **Active Estlcam Project**  
  The `.E12` filename visible in the foreground Estlcam window caption, extracted via
  `EstlcamInterop.ExtractFileNameFromCaption`.

- **Non-project file**  
  Any file whose extension is **not** `.E12` (e.g. `.dxf`, `.svg`, `.jpg`, etc.). These
  files may be imported into Estlcam but are not directly tracked by EstlCameo.

- **Attached**  
  EstlCameo’s state where:
  - `WorkingFilePath` is non-empty
  - `WorkingFilePath` points to an existing file
  - Extension of `WorkingFilePath` is `.E12` (case-insensitive)

- **Detached**  
  EstlCameo’s state where:
  - `WorkingFilePath` is null or empty, OR
  - `WorkingFilePath` does not meet the requirements for Attached (e.g. not `.E12`)

- **Foreground Estlcam window**  
  The Estlcam main window currently owning input focus, identified via
  `EstlcamInterop.TryGetForegroundEstlcamInfo`.

---

## 1. Invariants

These MUST always hold when EstlCameo is in a valid state.

- **R1**  
  EstlCameo MUST only ever track `.E12` project files. No other extensions are allowed.

- **R2**  
  If `WorkingFilePath` is non-empty, the file MUST exist on disk at the time of use.

- **R3**  
  Any non-`.E12` file selected in the “Project Selection” dialog MUST be rejected with a clear
  message and MUST NOT be stored in `WorkingFilePath`.

- **R4**  
  If `WorkingFilePath` is found to be non-empty with a non-`.E12` extension, EstlCameo MUST
  treat this as an invalid state: immediately detach (`WorkingFilePath = null`) and proceed
  as Detached.

- **R5**  
  `TryGetActiveEstlcamProjectFileName` is the authoritative way to determine if a usable
  Estlcam project is active. If it returns `false`, EstlCameo MUST behave as if there is
  **no active project**, regardless of what is in the caption.

---

## 2. Active Estlcam Project Detection

### 2.1 `TryGetActiveEstlcamProjectFileName`

- **R6**  
  `TryGetActiveEstlcamProjectFileName` MUST:
  - Return `false` if:
    - Estlcam is not the foreground window, OR
    - The caption contains no filename, OR
    - The caption filename’s extension is not `.E12`.
  - Return `true` and output the `.E12` filename if:
    - Estlcam is foreground, AND
    - Caption contains a filename with `.E12` extension.

- **R7**  
  Non-`.E12` files visible in the caption (e.g., `.dxf`, `.svg`, `.jpg`) MUST NOT be
  treated as Active Estlcam Projects. They are effectively “no active project” for
  EstlCameo’s tracking logic.

---

## 3. Hotkey Overview

- **Ctrl+S** – “Snapshot / Save-awareness” hotkey  
  - Used to:
    - Attach EstlCameo to an Estlcam `.E12` project.
    - Track user saves and align snapshot timing via `ExpectSaveFromCtrlS()`.

- **Ctrl+R** – “Review snapshots” hotkey  
  - Used to:
    - Attach EstlCameo to an Estlcam `.E12` project (when detached but resolvable).
    - Open the snapshot viewer for the currently tracked `.E12` project.

Both hotkeys are **no-op** when there is no valid `.E12` context to work with, as defined below.

---

## 4. Ctrl+S Behavior Contract

### 4.1 High-level Rules

- **R8**  
  If there is **no active `.E12` project** (i.e., `TryGetActiveEstlcamProjectFileName`
  returns `false`), Ctrl+S MUST be a **silent no-op**:
  - No dialogs.
  - No toasts.
  - No change to `WorkingFilePath`.

  This includes:
  - No Estlcam window is in the foreground.
  - Estlcam has a blank workspace (no project loaded).
  - Estlcam has only an import file open (e.g. `.dxf`, `.svg`, `.jpg`).

- **R9**  
  If there *is* an active `.E12` project and EstlCameo is **Attached**:
  - Ctrl+S MUST:
    - Call `snapshot.ExpectSaveFromCtrlS()`.
    - NOT show dialogs or project selection UI.
    - NOT change `WorkingFilePath`.

- **R10**  
  If there *is* an active `.E12` project and EstlCameo is **Detached**:
  - Ctrl+S MUST attempt to attach to that project:
    1. Attempt to resolve the full path via
       `TryResolveProjectForForegroundEstlcam(promptIfUnknown: false)`.
    2. If resolved:
       - Validate as `.E12` and existing file (R1, R2).
       - On success:
         - Set `WorkingFilePath = path`.
         - Immediately call `snapshot.CreateSnapshotNow("First attach via Ctrl+S")`
           (or equivalent first-attach snapshot).
         - Show a toast:

           > “EstlCameo: Now tracking this project \<filename\>”

    3. If not resolved:
       - Show the **Project Selection** dialog.
       - If user cancels:
         - Show a short toast explaining that no project was selected and no snapshot
           was created.
         - Leave `WorkingFilePath` empty.
       - If user selects a file:
         - Enforce `.E12` extension + file existence (R1–R3).
         - On valid `.E12`, behave as in step 2 (attach + first snapshot + toast).

### 4.2 Project Switch Detection (Ctrl+S)

- **R11**  
  When EstlCameo is **Attached** and `TryGetActiveEstlcamProjectFileName` returns a
  different `.E12` basename than `WorkingFilePath`:

  - EstlCameo MUST:
    - Show a toast of the form:

      > “EstlCameo: I think you switched projects…  
      > I was creating snapshots for: \<oldBase\>  
      > Estlcam now shows: \<newBase\>  
      > I’ve paused snapshots for the old file.  
      > Press Ctrl+S again on this new project to start tracking it.”

    - Immediately detach (`WorkingFilePath = null`).
    - NOT create a snapshot.
    - NOT call `snapshot.ExpectSaveFromCtrlS()`.

- **R12**  
  Non-`.E12` active files MUST NOT be treated as project switches. If the caption shows
  a non-`.E12` file, switch detection MUST NOT trigger (R6, R7).

---

## 5. Ctrl+R (Review Snapshots) Behavior Contract

### 5.1 High-level Rules

- **R13**  
  If EstlCameo is **Attached** (with a valid `.E12` `WorkingFilePath`):

  - Ctrl+R MUST:
    - Open the snapshot viewer for `WorkingFilePath`, creating it if needed.
    - If the viewer already exists, bring it to front and focus.
    - NOT change `WorkingFilePath`.

- **R14**  
  If EstlCameo is **Detached** and there is **no active `.E12` project**
  (`TryGetActiveEstlcamProjectFileName` returns `false`):

  - Ctrl+R MUST be a **silent no-op**:
    - No dialogs.
    - No toasts.
    - No path changes.

- **R15**  
  If EstlCameo is **Detached** and there IS an active `.E12` project:

  - Ctrl+R MUST:
    1. Attempt to auto-resolve the project path via
       `TryResolveProjectForForegroundEstlcam(promptIfUnknown: false)`.
    2. If resolved:
       - Validate as `.E12` and existing file.
       - Set `WorkingFilePath = path`.
       - Show the snapshot viewer for that project.
       - Optionally show a toast:

         > “EstlCameo: Now tracking this project \<filename\>”

    3. If not resolved:
       - Show the **Project Selection** dialog.
       - On cancel:
         - Show a short toast that no project was selected.
         - Do not open the viewer.
       - On valid `.E12` selection:
         - Attach (`WorkingFilePath = path`) and open the viewer for that project.

### 5.2 Project Switch Detection (Ctrl+R)

- **R16**  
  If EstlCameo is **Attached** and `TryGetActiveEstlcamProjectFileName` returns a
  different `.E12` basename than `WorkingFilePath`:

  - Ctrl+R MUST:
    - Show a toast of the form:

      > “EstlCameo: I think you switched projects…  
      > I was creating snapshots for: \<oldBase\>  
      > Estlcam now shows: \<newBase\>  
      > I’ve paused snapshots for the old file.  
      > Press Ctrl+S on this new project to start tracking it,  
      > then press Ctrl+R to review its snapshots.”

    - Immediately detach (`WorkingFilePath = null`).
    - NOT open the snapshot viewer.

- **R17**  
  As with Ctrl+S, non-`.E12` active files MUST NOT be treated as project switches.

---

## 6. Project Selection Dialog Contract

- **R18**  
  The **Project Selection** dialog is the only mechanism by which the user manually
  associates a file with EstlCameo when auto-detection fails.

- **R19**  
  On a successful user selection, the dialog MUST only return a path that:
  - Exists on disk.
  - Has `.E12` extension (case-insensitive).

- **R20**  
  If the user cancels the Project Selection dialog:
  - `WorkingFilePath` MUST remain unchanged.
  - The caller MUST provide a short toast explaining that no project was selected.

- **R21**  
  The file picker SHOULD default to filtering `.E12` files to guide users toward valid
  Estlcam projects.

---

## 7. UX Noise & No-op Rules

The following inputs MUST NOT produce any visible behavior (no dialogs, no toasts, no
state changes):

- **R22**  
  Ctrl+S when:
  - No Estlcam window is in the foreground, OR
  - Estlcam workspace is blank (no project), OR
  - Only a non-`.E12` file (e.g. `.dxf`, `.svg`, `.jpg`) is open.

- **R23**  
  Ctrl+R when:
  - EstlCameo is Detached, AND
  - There is no active `.E12` project in the Estlcam caption.

---

## 8. Scenario Matrix (Summary)

This table summarizes common scenarios and the expected behavior identifiers. Details
for each expected behavior are implied by the requirements above and can be used as
test-case anchors.

| ID   | Attached? | Estlcam State                          | Active File | Hotkey | Expected Behavior |
|------|-----------|-----------------------------------------|-------------|--------|-------------------|
| S001 | No        | No Estlcam foreground window           | N/A         | Ctrl+S | R8 (no-op)        |
| S002 | No        | Blank workspace                        | (none)      | Ctrl+S | R8 (no-op)        |
| S003 | No        | Import-only file open (e.g. `.dxf`)    | .dxf        | Ctrl+S | R8 (no-op)        |
| S004 | No        | `.E12` active, path resolvable         | foo.e12     | Ctrl+S | R10 (attach+snap) |
| S005 | No        | `.E12` active, path NOT resolvable     | foo.e12     | Ctrl+S | R10 (prompt)      |
| S006 | Yes       | Same `.E12` active                     | foo.e12     | Ctrl+S | R9 (expect-save)  |
| S007 | Yes       | Different `.E12` active                | bar.e12     | Ctrl+S | R11 (switch)      |
| S008 | No        | `.E12` active, path resolvable         | foo.e12     | Ctrl+R | R15 (attach+view) |
| S009 | Yes       | Same `.E12` active                     | foo.e12     | Ctrl+R | R13 (view)        |
| S010 | Yes       | Different `.E12` active                | bar.e12     | Ctrl+R | R16 (switch)      |
| S011 | No        | Blank workspace                        | (none)      | Ctrl+R | R14 (no-op)       |
| S012 | No        | Import-only file open (e.g. `.dxf`)    | .dxf        | Ctrl+R | R14 (no-op)       |

Future test code can map:

- Scenario `ID` → test name.
- `Attached?`, `Estlcam State`, `Active File`, `Hotkey` → fixture setup + input.
- `Expected Behavior` → assertions derived from the corresponding requirement rules.

---
