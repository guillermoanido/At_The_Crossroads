# ATC LAN Multiplayer — Editor Setup Checklist

Code-verified steps to run **At The Crossroads** in Online (Mirror LAN) mode. Architecture is
server-authoritative: the **host** owns all game state; clients send intents via `[Command]`s.

> Scope: this gets two machines connected, seated, and the match started under host authority.
> Board/hand state sync and hidden hands are a later step — until then the client's board stays
> mostly empty; verify this setup via the Console `[Net]` logs and the per-seat Pass button.

Fields/objects referenced below are from `Assets/Scenes/ATC.unity`. Anything the code now enforces
itself is called out so you don't have to set it twice.

---

## 1. Scene & GameManager

**1.1 — Open the right scene.** Open `Assets/Scenes/ATC.unity`. It's the only scene with the
GameManager, GameStack, Player 1 and Player 2 objects the networking layer binds to. Do **not**
build the online setup in `Main Menu.unity` or `DeckBuilding.unity`.
*Why:* `ATCNetworkManager.OnServerAddPlayer` / `NetworkPlayerSeat.OnStartServer` call
`GameManager.Instance.PlayerForSeat(seat)` and `StartGame()`, which only exist in this scene.

**1.2 — Confirm seat bindings.** On GameManager, verify Player 1 and Player 2 are both assigned.
*Seat is fixed by connection order:* first connection = seat 0 = Player 1 = host; second = seat 1 =
Player 2 = client. You can't pick sides in the HUD. A null Player means every `Cmd` is silently
ignored (the `BoundPlayer == ActivePlayer/ControllingPlayer` checks fail).

**1.3 — Turn Online Mode ON.** Check **Online Mode** on GameManager, then save the scene.
*Gotcha:* the scene YAML has **no `onlineMode` line**, so it was saved before the field existed and
currently uses the script default = **false (OFF)**. It will look unchecked and you **must** tick it.
Leaving it off makes `GameManager.Start()` auto-start a solo match on load and hides the HUD.
Online Mode gates four things: gate `StartGame()` to the network, route input through `[Command]`s,
switch the Pass button to the server-driven path, and disable the AI.

---

## 2. Player Prefab

**2.1 — Verify the seat asset.** In the Project window, select
`Assets/Prefabs/NetworkPlayerSeat.prefab` and confirm it has **NetworkIdentity** + **NetworkPlayerSeat**.
Leave it as a Project asset only — **do not** place an instance in the scene hierarchy.
*Gotcha:* a copy left in the scene gets a baked sceneId and Mirror will spawn/duplicate it. The
player object is spawned per-connection from the Project asset.

---

## 3. NetworkManager object (one GameObject, several components)

**3.1 — Create it.** `GameObject → Create Empty`, name it `NetworkManager`.
*Gotcha:* there must be exactly **one** NetworkManager in the loaded scene set (it's a
`DontDestroyOnLoad` singleton). For LAN testing, load `ATC.unity` directly.

**3.2 — Add `ATCNetworkManager`.** `Add Component → ATC Network Manager` (the Mirror
`NetworkManager` subclass — adds the inherited Transport / Player Prefab / Max Connections fields).
Add **ATCNetworkManager**, not the stock Mirror NetworkManager, or seats never get assigned.

**3.3 — Add `KcpTransport`.** `Add Component → Kcp Transport`. Confirm **Port = 7777** (UDP).
Both host and client must use the same port.

**3.4 — Assign the Transport field.** Drag the KcpTransport **component** into the ATCNetworkManager
**Transport** field. If left empty but present on the same object, Mirror auto-uses it and logs the
`No Transport assigned … Using KCP … found on same object` warning — assigning it explicitly silences that.

**3.5 — Add `NetworkManagerHUD`.** `Add Component → Network Manager HUD` (the Host/Server/Client
buttons). `ATCNetworkManager.Start()` enables it only when Online Mode is on.

**3.6 — Assign the Player Prefab.** Drag `Assets/Prefabs/NetworkPlayerSeat.prefab` (the Project
asset) into the **Player Prefab** field. If empty you get *"The PlayerPrefab is empty on the
NetworkManager."* Assign the **Project asset**, not a scene instance.

**3.7 — Max Connections.** Now **enforced in code** (`ATCNetworkManager.Start()` sets it to 2), so a
3rd client is rejected at transport-connect time. Setting it to `2` in the Inspector too is optional
and just keeps the Inspector honest.

---

## 4. Input routing (this is what makes client input reach the host)

**4.1 — Add `MatchInput`.** Add a **MatchInput** component to a scene GameObject — a dedicated empty
named `MatchInput` is cleanest. It's a singleton and is not in the scene yet, so you must add one.
*Gotcha:* add exactly one; a second instance `Destroy(gameObject)`s itself in Awake.

**4.2 — Re-point the Pass button.** Select the Pass button. In its `Button → OnClick` list,
**replace** the current `GameStack.Pass()` entry with a single call to **`MatchInput.RequestPass()`**.
Remove the old entry — don't add a second, or Pass fires twice.

**4.3 — Re-point the phase-advance button.** Select the phase-advance button and **replace** its
direct `GameManager.AdvancePhase()` OnClick with **`MatchInput.RequestAdvancePhase()`**.
*Why 4.2/4.3 matter:* online, calling `GameStack.Pass()` / `GameManager.AdvancePhase()` directly runs
local-only logic and desyncs. `MatchInput` sends `CmdPass()` / `CmdAdvancePhase()` from the local
seat, which the host validates against `ControllingPlayer` / `ActivePlayer` before running.

**4.4 — Keep `GameStack.passButton` assigned.** Confirm GameStack still points its `passButton`
field at the Pass button object. That reference only shows/hides the button (via
`ShowPassButton`); it does not invoke Pass.

**4.5 — SimpleAI: nothing to do.** `SimpleAI.Update()` early-returns while Online Mode is on, so the
AI is inert automatically. (It resumes if you turn Online Mode back off.)

---

## 5. Firewall & run

**5.1 — Allow UDP 7777 inbound on the HOST.** Windows Defender Firewall → allow inbound UDP 7777 for
the Unity Editor / built player. The first Host press usually pops a prompt — approve **Private
networks**. Only the host needs this; both machines must be on the same LAN/subnet.

**5.2 — Host, then Client.** On the host: enter Play, click **Host (Server + Client)** (fills seat 0 =
Player 1). On the second machine: enter Play, type the host's LAN IPv4 (`ipconfig` → IPv4 Address,
e.g. `192.168.x.x`) into the HUD address box (replacing `localhost`), click **Client** (fills seat 1).
When `numPlayers` hits 2 you'll see `[Net] Both seats filled — starting the match on the server.`
*Gotcha:* `localhost` only works for two instances on **one** machine; a real second machine must use
the host's LAN IP.

**Success in the Console:**
```
[Net] Connection 0 took seat 0 (Player 1 / host).
[Net] Connection 1 took seat 1 (Player 2 / client).
[Net] Both seats filled — starting the match on the server.
```

---

## Common errors

| Error | Cause | Fix |
|-------|-------|-----|
| `The PlayerPrefab is empty on the NetworkManager.` | Player Prefab field unassigned | Drag `NetworkPlayerSeat.prefab` (Project asset) into **Player Prefab**. |
| `The PlayerPrefab does not have a NetworkIdentity.` | Assigned prefab lacks NetworkIdentity | Add **Network Identity** to the prefab (the shipped one already has it — you assigned a different/older prefab). |
| `No Transport on Network Manager…` (or the `Using KCP … found on same object` warning) | Transport field null | Add **Kcp Transport** and drag it into the **Transport** field. |
| Match starts but nothing is controllable (no error) | Player Prefab has NetworkIdentity but no **NetworkPlayerSeat** | Use the real `NetworkPlayerSeat.prefab`. The code now `LogError`s + disconnects instead of failing silently. |

---

## Current boundary

After this setup: two machines connect, get seated, and the host starts + runs the match; turn /
priority / Pass / phase-advance are networked. **Not yet:** playing/activating cards over the wire
(needs stable per-card network IDs) and board/hand rendering on the client + hidden hands. Those are
the next chunk.
