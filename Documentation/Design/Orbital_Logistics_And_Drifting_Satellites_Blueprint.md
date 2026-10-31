# Orbital Logistics & Drifting Satellite Infrastructure Framework

**Author:** Project Vigilance Core Team  
**Status:** Approved Specification & Implementation Blueprint  
**Target Engine:** RimWorld 1.7 (Waylith Engine / .NET 10 / C# 13)  
**Target Mod:** Project Vigilance (Geoscape)

---

## 1. Executive Summary & Design Pillars

In conventional RimWorld gameplay and mods, orbital entities (such as trade ships, orbital platforms, and satellites) are implemented either as invisible off-map timers or static world objects fixed to a single coordinate or repeating loop.

This framework introduces a **complete orbital ecosystem** designed specifically for **orbital-only factions** (e.g. `TradersGuild` / `Merchant's Bazaar`, orbital cartels, mechanoid orbital clusters, and future spacefarers) and planetary defenders:

1. **Orbital Stations as Persistent Hubs:** Orbital stations act as persistent faction operational centres and hubs, analogous to settlements for surface factions. They serve as deployment, replenishment, and command nodes.
2. **Drifting Orbital Mechanics Engine:** Satellites move on physically consistent, closed-form Keplerian orbits with nodal precession ($\dot{\Omega}$) and incommensurate orbital periods. Fixed repeating ground tracks are completely replaced by smoothly shifting ground coverage paths. A colony never experiences passes on a rigid repeating clock.
3. **Satellites as Mobile Faction Assets:** Satellites act as the mobile actors of orbital factions, analogous to caravans for ground factions. Satellites transition through distinct operational phases: entering station service areas for maintenance and resupply, then departing on autonomous orbital sweeps.
4. **Distinct Functional Roles:** Satellites feature specialized mission profiles:
   - **Trade Satellites:** Open dynamic trading windows with surface colonies equipped with a powered Comms Console (`ICommunicable`, `ITrader`).
   - **Reconnaissance Satellites:** Sweep across the globe (`ISensorProvider`), uncovering Fog of War and revealing hidden incursions and sites in the Haze.
   - **Military Satellites:** Provide tactical telemetry and orbital fire-control support.
   - **Navigation Satellites:** Project positioning telemetry beams accelerating overland travel for friendly caravans.
   - **Communication Satellites:** Relay sensor feeds from remote listening posts across oceans and impassable mountain chains.
5. **Future-Proof Vessel Extensibility:** An abstract contract (`IOrbitalEntity`) allows future orbital vessels (gravships, shuttles, interceptors, UFOs) to navigate between orbital stations and drifting orbits without requiring refactoring.

```
                  ┌──────────────────────────────────────────────┐
                  │          Persistent Orbital Station          │
                  │   (Faction Hub / Deployment & Maintenance)   │
                  └──────────────┬───────────────────────────────┘
                                 │
                 Service Area    │    Depart on Autonomous Sweep
                 Rendezvous      │    (Drifting Non-Repeating Track)
                                 ▼
                  ┌──────────────────────────────────────────────┐
                  │            Drifting Satellite                │
                  │  [Trade | Recon | Military | Nav | Comms]    │
                  └──────────────┬───────────────────────────────┘
                                 │
     ┌───────────────────────────┼───────────────────────────┐
     ▼                           ▼                           ▼
┌──────────────┐          ┌──────────────┐          ┌──────────────┐
│ Comms Trade  │          │ Sensor Swath │          │ Caravan Nav  │
│ Window Over  │          │ Disperses    │          │ Speed Boost  │
│ Colony Base  │          │ Haze of War  │          │ (+15% Speed) │
└──────────────┘          └──────────────┘          └──────────────┘
```

---

## 2. Drifting Orbital Mechanics Mathematical Model

### 2.1 The Problem with Fixed Repeating Ground Tracks
Standard repeating orbits with integer orbital frequencies (e.g. 10 passes per day) cause satellites to pass over the exact same set of hex tiles every day at the exact same minute. This feels artificial, removes player anticipation, and makes orbital coverage predictable and static.

### 2.2 Analytical Closed-Form State Formulation
To ensure zero heap allocation and $O(1)$ evaluation per satellite per tick, the orbital state is solved analytically from game ticks without numerical integration.

Let:
- $t$: Current game tick ($60{,}000\text{ ticks} = 1\text{ in-game day}$).
- $t_0$: Orbital epoch tick (tick of insertion/launch).
- $\Delta t = t - t_0$: Elapsed ticks since epoch.
- $T_{\text{orbit}}$: Orbital period in ticks (e.g. $3{,}370\text{ ticks}$ for Low Orbit; $14{,}200\text{ ticks}$ for Medium Orbit).
- $n = \frac{360^\circ}{T_{\text{orbit}}}$: Mean orbital motion in degrees per tick.
- $i$: Orbital inclination in degrees relative to the planetary equator.
- $\Omega_0$: Initial Right Ascension of the Ascending Node (RAAN) at $t_0$.
- $\dot{\Omega}$: Nodal precession rate in degrees per day (due to planetary oblateness $J_2$, e.g. $-3.2^\circ/\text{day}$).
- $M_0$: Mean anomaly at epoch in degrees.
- $\delta_{\text{drift}}$: Minor orbital drift factor in degrees per day.
- $\omega_{\text{planet}} = \frac{360^\circ}{60{,}000} = 0.006^\circ/\text{tick}$: Planetary rotation rate.

#### Step 1: In-Plane Argument of Latitude
Assuming near-circular operational orbits ($e \approx 0$):
$$u(t) = (M_0 + n \cdot \Delta t) \bmod 360^\circ$$

#### Step 2: Nodal Precession & Plane Rotation
The orbital plane precesses around the planet's polar axis:
$$\Omega(t) = \left(\Omega_0 + (\dot{\Omega} + \delta_{\text{drift}}) \cdot \frac{\Delta t}{60{,}000}\right) \bmod 360^\circ$$

#### Step 3: Sub-Satellite Latitude $\phi(t)$
Using spherical trigonometry:
$$\sin(\phi) = \sin(i) \cdot \sin(u(t)) \implies \phi(t) = \arcsin(\sin(i) \cdot \sin(u(t)))$$

#### Step 4: Sub-Satellite Longitude $\lambda(t)$ in Rotating Planetary Frame
In the inertial frame, the right ascension $\alpha(t)$ is:
$$\alpha(t) = \Omega(t) + \text{atan2}\left(\sin(u)\cos(i), \cos(u)\right)$$
Accounting for planetary rotation beneath the orbit:
$$\lambda(t) = \left(\alpha(t) - \omega_{\text{planet}} \cdot \Delta t\right) \bmod 360^\circ$$
Normalised to $[-180^\circ, +180^\circ]$.

#### Step 5: Sub-Satellite Unit Vector $\hat{P}$
$$\hat{P} = \begin{pmatrix} \cos(\phi)\cos(\lambda) \\ \sin(\phi) \\ \cos(\phi)\sin(\lambda) \end{pmatrix}$$
The sub-satellite surface tile is retrieved via:
$$T_{\text{sub}} = \text{Find.WorldGrid.GetTileIDByDir}(\hat{P})$$

#### Step 6: 3D Orbital World Position
$$\vec{P}_{\text{orbit}} = \hat{P} \cdot (R_{\text{planet}} + h_{\text{altitude}})$$
where $h_{\text{altitude}}$ provides vertical rendering separation above the planetary sphere.

---

## 3. Persistent Orbital Stations & Logistics Network

### 3.1 Hub Role
- Persistent faction operational centre (`WorldObject_OrbitalStation`).
- Maintains a list of deployed child satellites (`List<int> satelliteIds`).
- Serves as the primary contact point for orbital factions (`TradersGuild` and future factions).

### 3.2 Service Area Lifecycle
Stations maintain an established orbital service area defined by an angular radius $\theta_{\text{service}}$ (typically $30^\circ$ to $45^\circ$).
- **Service Area Entry:** When a satellite's angular distance to its parent station drops below $\theta_{\text{service}}$, it enters the **Station Service Area**.
  - Telemetry sync and orbital correction.
  - Replenishment of trade cargo / sensor calibration.
  - Inspect string indicates: `In Service Area (Linked: Station Alpha)`.
- **Autonomous Mission Sweep:** As orbital drift and motion carry the satellite outside $\theta_{\text{service}}$, it switches to autonomous sweep mode.
  - Inspect string indicates: `Autonomous Mission (Drifting Ground Track)`.
- **Satellite Replenishment:** If a satellite is lost or decommissioned, the parent station begins a manufacturing and launch countdown (e.g. 3-5 game days) before placing a replacement into orbit.

---

## 4. Distinct Satellite Roles

| Role | Orbit Type | Altitude | Period | Coverage / Footprint | Gameplay Effect |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Trade** | Mid-Inclination ($35^\circ-45^\circ$) | 600 km | ~4,200 ticks | 16 tiles | Implements `ICommunicable`, `ITrader`. Dynamic orbital comms trading window when overhead colony with powered Comms Console. |
| **Reconnaissance** | Polar / Sun-Sync ($85^\circ-98^\circ$) | 450 km | ~3,400 ticks | 14 tiles | Implements `ISensorProvider`. Sweeps all latitudes, revealing Fog/Haze of War and detecting hidden incursions/sites. |
| **Military** | High-Inclination ($55^\circ-65^\circ$) | 800 km | ~5,100 ticks | 18 tiles | Tactical surveillance. Hostile incursions inside footprint are analysed instantly; provides fire control telemetry. |
| **Navigation** | Semi-Synchronous ($50^\circ-60^\circ$) | 12,000 km | ~14,000 ticks | 24 tiles | Telemetry positioning beam. Caravans under footprint travel 15% faster. |
| **Communication** | High-Elliptical / Relay ($25^\circ-35^\circ$) | 35,000 km | ~28,000 ticks | 32 tiles | Uplink relay linking unmanned listening posts across terrain masking and oceans to colony sensor network. |

---

## 5. Extensibility Framework for Future Orbital Vessels

The interface `IOrbitalEntity` unifies all orbital actors:
```csharp
public interface IOrbitalEntity
{
    string OrbitalCallsign { get; }
    Faction Faction { get; }
    OrbitalParameters Orbit { get; }
    PlanetTile CurrentSubSatelliteTile { get; }
    Vector3 CurrentOrbitalPosition { get; }
    bool InServiceArea { get; }
    void OnEnterServiceArea(WorldObject_OrbitalStation station);
    void OnLeaveServiceArea(WorldObject_OrbitalStation station);
}
```
Future vessels (e.g., player gravships, shuttles, UFO interceptors) inherit from this interface, allowing them to enter station service zones, dock, or navigate orbital transit corridors without altering satellite or station logic.

---

## 6. Verification & Test Plan

1. **Analytical Ground Track Drift Test:** Verify sub-satellite tile shifts across consecutive orbits and days (non-repeating coverage).
2. **Station Service Area Transition Test:** Verify satellites enter and exit the service area as their orbital position changes.
3. **Comms Trade Overpass Test:** Verify `Building_CommsConsole` lists the trade satellite only during its overpass window.
4. **Navigation Telemetry Boost Test:** Verify caravans under a navigation satellite footprint receive the speed multiplier.
5. **Reconnaissance Sensor Sweep Test:** Verify `SensoryManager` registers recon satellites and reveals fog beneath their path.
6. **Save/Load Serialization Test:** Verify complete state preservation across `Scribe` save cycles.
