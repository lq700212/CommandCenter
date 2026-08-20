# Host Computer (PC) Software Operation Manual

> **Audience**: Field operators and team leaders.
> **What this covers**: Power-on, reading the interface, daily operation, and what to do when something goes wrong. It does not explain internal principles.
> **For engineers**: The companion technical document is `docs/CommandCenter.md` (communication protocol and configuration details).

---

## 1. Power-On and Startup

1. Turn on the computer and wait for Windows to finish starting.
2. Double-click the **CommandCenter** (host inspection program) icon on the desktop.
3. Wait 2~3 seconds. When the **main window fills the screen** and rows of inspection windows appear, the program has started successfully.
4. Normal production **does not require login**. Only maintenance personnel need to enter an administrator account when changing parameters by clicking "System Settings" in the top-right corner.

> If startup fails: check the bottom-right corner of the screen for error messages; make sure the `Config` folder has not been modified. If it still won't start, call an engineer. **Do not delete files yourself.**

---

## 2. Getting to Know the Main Interface

```
┌──────────────────────────────────────────────────────────────────┐
│ Model:[U171 ▾]  Serial No.:[SN12345][Manual Entry] Total:0 OK:0 NG:0 [System Settings][中文] ●PLC ●Scanner ●Cam1 ●Cam2│
├──────────────────────────────────────────────────────────────────┤
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                    │
│  │  W1  │ │  W2  │ │  W3  │ │  W4  │ │  W5  │   ← Inspection window matrix │
│  │image │ │image │ │image │ │image │ │image │     one cell = one inspection point │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘                    │
├──────────────────────────────────────────────────────────────────┤
│ Status: Waiting for inspection…                                   │
└──────────────────────────────────────────────────────────────────┘
```

Explanations:

| Location | Name | What it is / How to read it |
| --- | --- | --- |
| Top-left | **Model** | The product model currently being produced. Click the dropdown to switch directly. After switching, the cameras automatically switch to the matching program; **no restart needed** |
| Center | **Serial No.** | The barcode of the current workpiece. It fills in automatically when the scanner reads it (read-only display) |
| Button | **【Manual Entry】** | Used to manually enter or modify a barcode when the scanner fails to read it |
| Title bar | **Total / OK / NG** | Inspection point count for **the current single workpiece**: OK = green, NG = red. It resets automatically to zero each time a new workpiece arrives |
| Top-right | **● Dot lights** | Four lights: PLC, scanner, camera 1, camera 2. **Green = normal, red = problem** (PLC yellow = waiting for the master station, which is normal) |
| Top-right | **Language switch** | The **中文/English** button at the far right of the title bar (the Chinese UI shows English, the English UI shows 中文). Click it to switch between Chinese and English instantly; it saves automatically and persists after restart |
| Center | **Window matrix** | Each cell is the camera image of one inspection point. The latest image refreshes automatically after each shot; **a green border in the bottom-right of a cell = this point is OK, red border = NG** |
| Bottom | **Status bar** | Shows what the system is doing right now: waiting for a scan, taking a photo, switching model, etc. |

> **Reminder**: Every time a new workpiece arrives, all windows are cleared to dark gray and the counter resets to zero. This is normal and means the current workpiece has started inspection from scratch.

---

## 3. Daily Standard Procedure

1. Power on and start the program.
2. Confirm the four dot lights in the top-right corner: **PLC, scanner, camera 1, camera 2 must all be green** (yellow PLC can wait). If any light is red, do not start work; see Section 8.
3. Confirm the **Model** in the top-left corner is the model for today. If not, click the dropdown to switch, and wait until the status bar shows "Model switch complete".
4. Place the workpiece → the scanner reads the barcode automatically → the cameras take photos automatically → each window shows its image → each point is automatically judged OK/NG.
5. During normal production **no operation is needed**; just watch the window images and counters.

---

## 4. Serial Number (SN) — Very Important

- **Normal**: When the scanner reads a barcode, it appears automatically after "Serial No.:". Nothing to do.
- **Scanner failed to read, or read the wrong code**: Click the **【Manual Entry】** button next to it → an input box pops up → enter or modify the barcode → **press Enter to confirm** (press Esc to cancel).
- When entering, note: **you cannot confirm an empty value**; if you don't actually want that code, click Cancel.

> ⚠️ The serial number determines which folder photos are archived to (`Date/Barcode/...`), so **if the barcode is wrong, the photos are stored in the wrong place**. Each time you change workpieces, check whether the serial number matches the workpiece.

---

## 5. When the Scanner Has a Problem (Key Section)

### Symptom
A popup appears: **"Scanner error. Please check the scanner or perform manual entry."**

### Cause
The scanner failed to read a code this time (dirty barcode / too far away / loose cable / scanner broken).

### What to do (in order)
1. **Look at the scanner**: Is the laser flashing? Is the cable loose? Straighten the barcode and bring it closer, then try again.
2. **Once the scanner recovers**: the popup closes automatically. Nothing to do.
3. **If it can't be fixed right away**: Click **【Manual Entry】**, manually type in this workpiece's barcode, and production continues.
4. **Clicking 【Later】**: only means "don't bother me for now". **This workpiece still owes a barcode**, and the program will keep waiting — you must fill it in later via **【Manual Entry】**, otherwise the process will be stuck.
5. The popup has a **☐ Don't remind me today** checkbox: when checked, no more popups today (the workpiece still needs manual entry; it just stops nagging you). Suitable for cases like "the scanner is being repaired, use manual entry to get through today".

> ⚠️ **The popup is not nothing happening — it means this workpiece's code was not read.** Don't keep clicking 【Later】 as if it didn't happen — **if you don't do the manual entry, this workpiece cannot pass**.

---

## 6. Zooming In on a Cell's Image

- **Double-click** any window → it enlarges to full screen for a closer look (the image keeps refreshing in real time while enlarged).
- **Double-click again / press Esc** → restores to its original position.
- While in full screen, to look at another cell: simply double-click that other window to switch.

---

## 7. Where Photos Are Stored (Finding Images/Records)

- The program automatically saves each inspection photo to the computer's image directory. The structure is:

```
E:\Images (image root directory)
 └─ 2026.08.20 (date, dot-separated)
     └─ SN12345 (barcode number)
         └─ Upper camera (which camera took it)
             ├─ OK (this point passed inspection)
             └─ NG (this point failed inspection)
                 └─ 001_20260817_164022_461.jpeg (photo)
```

- To look up the photos of a specific workpiece: ask the team leader/engineer to open the corresponding folder.
- **Photos are kept for 30 days by default and then auto-cleaned**. Copy important ones out early.

---

## 8. What to Do When a Connection Light Is Red (On-Site Quick Reference)

> **Reading the colors (since V2.15.11)**: Connection lights have only two states — **green = normal, red = problem** (the PLC additionally has **yellow = waiting for the master station**). **There is no gray**: lights show the real state right from startup; if no scanner is enabled, the **scanner light disappears** (that means there is no scanner, not that it is broken).

| Symptom | What it means | What the operator should do |
| --- | --- | --- |
| **PLC light red** | PLC communication is down | Don't start work; call an engineer |
| **PLC light yellow** | Waiting for the PLC master station to connect | Normal waiting; nothing to do |
| **Scanner light red** | Scanner not connected | Check the scanner's cable/power; hold it up with 【Manual Entry】 per Section 5 |
| **Camera light red** | Camera communication is down | Call an engineer (check the camera network cable / image-push configuration) |
| **Window never shows an image** | The camera took the shot but the image didn't arrive | Call an engineer to look at it; don't keep triggering repeatedly |
| **A single cell never takes a photo** | That cell is an "empty window" (no inspection point assigned) | Normal; nothing to do |

---

## 9. Prohibited Operations (Red Lines — Must Be Observed)

1. **Don't click "System Settings"** — it requires an administrator password and is for maintenance personnel to change parameters. Operators should not enter it.
2. **Don't touch other windows on the computer, don't turn off the firewall, don't unplug network cables**.
3. **Don't manually delete/modify files in the image folders**.
4. When a popup or anomaly appears, **follow the prompt first; if you can't resolve it, call an engineer**; don't restart the computer directly (it will lose the current workpiece's data).
5. **Don't change the product model on your own**; model changes should follow the team leader's arrangement.

---

## 10. Shift Handover / End of Day

- The program can stay running; it doesn't need to be closed.
- When you need to shut down, just click Close in the top-right corner normally.
- If there were any issues that day such as scanner errors or camera errors, **tell the next shift/engineer verbally or write it in the handover log**, to make troubleshooting easier.

---

## Frequently Asked Questions — Quick Answers

- **Q**: Why are all the windows dark gray?
  **A**: A new workpiece has started and the windows were just cleared. The image will appear automatically once the camera finishes shooting. If no image ever appears, follow Section 8.

- **Q**: Why does the counter stay frozen?
  **A**: Check whether the status bar is stuck at some step; if there is a popup, handle it first; if it still won't move, call an engineer.

- **Q**: What if I selected the wrong model?
  **A**: Click the model dropdown in the top-left corner, select the correct model, and wait for the status bar to show "Model switch complete". No restart needed.

- **Q**: Why does production still not move after I dismissed a popup?
  **A**: Most likely this workpiece's barcode has not been entered yet (see Section 5, item 4). Click **【Manual Entry】** to fill in the code and it will be fine.
