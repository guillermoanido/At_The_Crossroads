# Demo Day Runbook — two PCs, school Wi-Fi

Everything needed to show ATC's LAN multiplayer in class. Follow top to bottom.

---

## The one risk you cannot fix with code

School / campus / guest Wi-Fi very often has **AP isolation** (a.k.a. client isolation) turned on:
every device can reach the internet, but devices **cannot reach each other**. No game, engine or
setting can defeat this — it is enforced by the access point.

So the plan is: **test the room's Wi-Fi in advance, and carry a phone hotspot as the fallback.**
A phone hotspot is your own private network with no isolation and both PCs on one subnet. It works
every time and needs no internet data — the PCs only talk to each other.

**Decide which network you're on before class starts, not during it.**

---

## Part 1 — Before you leave home

**1.1 Build the game.** `File ▸ Build Profiles ▸ Windows ▸ Build`. Output somewhere clean, e.g.
`D:\ATC_Build\`.

**1.2 Make the window behave.** In `Project Settings ▸ Player ▸ Resolution and Presentation`:

| Setting | Set to | Why |
|---|---|---|
| Fullscreen Mode | **Windowed** | currently borderless fullscreen; windowed makes alt-tabbing and screenshots sane |
| Default Screen Width / Height | **1280 × 720** | fits any projector |
| Resizable Window | **on** | currently off |
| Run In Background | already **on** ✅ | keep it — otherwise an unfocused window freezes |

**1.3 Copy the whole build folder to the second PC.** Both PCs must run the **same build** — a
mismatched build will connect and then desync.

**1.4 Copy `Tools/ATC-Host-Setup.ps1` next to the build** on the PC that will host.

**1.5 Rehearse the whole thing at home** on your home Wi-Fi, two PCs. If it doesn't work at home it
definitely won't work at school.

---

## Part 2 — In the room, 15 minutes early

**2.1 Both PCs join the same Wi-Fi network.** Same SSID. If the school has `Student` and
`Student-5G` as separate networks, put both PCs on the *same* one.

**2.2 On the HOST PC, run `ATC-Host-Setup.ps1` as Administrator.**
Right-click ▸ *Run with PowerShell*. It opens UDP 7777, enables ping, and prints this PC's address.

Write down the address it prints. Example: `192.168.0.104`

**2.3 The 10-second reachability test.** On the OTHER PC, open Command Prompt:

```
ping 192.168.0.104
```

- **Replies** → the network is fine. Continue to Part 3.
- **Request timed out** → **AP isolation. Switch to the phone hotspot now** (Part 4).

This single test is the whole gamble, and it takes ten seconds. Do it before you present.

---

## Part 3 — Running the match

**On the HOST PC:**
1. Launch the game → pick a deck → **Host**
2. The screen shows `HOSTING - waiting for the other player` and lists this PC's address(es)

**On the SECOND PC:**
1. Launch the game → pick a deck → **Join**
2. Type the host's address → confirm
3. Screen shows `Connecting to ...`, then `Connected`

The match starts automatically once both seats are filled and both decks have arrived.

**If it fails**, the screen now tells you why instead of hanging — read the message and work down
its numbered checklist.

---

## Part 4 — Phone hotspot fallback

1. Phone ▸ **Personal Hotspot / Mobile Hotspot** on
2. Both PCs connect to the hotspot's Wi-Fi
3. On the host PC re-run `ATC-Host-Setup.ps1` — **the address will be different** on this network
4. Redo the `ping` test, then Part 3

Mobile data is not consumed by LAN traffic between the two PCs.

---

## View flip — already handled

Older notes claimed the joining player sees their own hand at the top. **That was fixed** (commit
`aeef08e` "View Fixed"). `GameManager.DisplayPlayerForSeat` maps *your* seat to the bottom slot on
whichever machine you're on, and every render path — hands, all nine board zones, stats, turn and
phase state — goes through it. Both players should see themselves at the bottom.

Worth a two-minute confirmation with two instances on one PC before demo day, since this is the
one thing that reads as broken from across a room.

---

## Quick failure table

| Symptom | Cause | Fix |
|---|---|---|
| `ping` times out | AP isolation, or different networks | Phone hotspot |
| `No answer from ... after 10 seconds` | Host firewall, or wrong address | Run the setup script as admin; re-read the host's on-screen address |
| `... refused the connection` | Host isn't hosting yet | Press Host first, then Join |
| Host shows a `169.254.x.x` address | Adapter got no DHCP lease | Reconnect to Wi-Fi; that address is never reachable |
| Both connect, board looks wrong | Different builds on each PC | Recopy the build folder |
| Unfocused window freezes | Run In Background off | Re-enable in Player settings |
